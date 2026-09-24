using System.Collections;
using System.Collections.Generic;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace GregModMoreModules
{
    /// <summary>
    /// Mass-insert catalog modules into every matching SFP port in the loaded
    /// scene. Fill mode only touches empty cages; replace mode also swaps
    /// occupied cages when the port still advertises the same sfpType
    /// ("matching connector") and no cable is attached.
    /// Always goes through CableLink.InsertSFP so identity rewrite + hooks fire.
    /// </summary>
    internal static class MassInsert
    {
        private const int MaxInsertsPerRun = 400;
        private static bool _running;

        internal static bool IsRunning => _running;

        internal static void Start(bool replaceOccupied)
        {
            if (_running)
            {
                MelonLogger.Warning("[RealisticModules] Mass insert already running.");
                return;
            }
            if (!Core.IsEnabled || !Core.IsSetupComplete)
            {
                Notify("RealisticModules is disabled or not set up yet.");
                return;
            }
            MelonCoroutines.Start(Run(replaceOccupied));
        }

        private static IEnumerator Run(bool replaceOccupied)
        {
            _running = true;
            int inserted = 0;
            int replaced = 0;
            int skippedCabled = 0;
            int skippedMismatch = 0;
            int noTemplate = 0;

            try
            {
                var mgm = MainGameManager.instance;
                if (mgm == null)
                {
                    Notify("No MainGameManager — open a game scene first.");
                    yield break;
                }

                var ports = CollectSfpPorts();
                if (ports.Count == 0)
                {
                    Notify("No SFP ports found in the loaded scene.");
                    yield break;
                }

                var byType = BuildModulesByPortType();
                if (byType.Count == 0)
                {
                    Notify("Module registry empty — catalog not set up.");
                    yield break;
                }

                // Round-robin per port type so mixed speeds fill evenly.
                var cursor = new Dictionary<int, int>();

                foreach (var link in ports)
                {
                    if (inserted + replaced >= MaxInsertsPerRun)
                    {
                        MelonLogger.Warning($"[RealisticModules] Mass insert hit safety cap ({MaxInsertsPerRun}).");
                        break;
                    }

                    if (link == null) continue;
                    bool didReplace = false;

                    int portType;
                    try { portType = link.sfpTypeSupported; } catch { continue; }
                    if (portType < 0) continue;

                    bool hasModule = false;
                    int cableIds = 0;
                    try { hasModule = link.insertedSFP != null || link.sfpTypeInserted != 0; } catch { }
                    try { cableIds = link.cableIDsOnLink; } catch { }

                    if (cableIds != 0)
                    {
                        // Never yank a live cable path.
                        skippedCabled++;
                        continue;
                    }

                    if (hasModule && !replaceOccupied)
                        continue;

                    if (!byType.TryGetValue(portType, out var candidates) || candidates.Count == 0)
                    {
                        if (hasModule) skippedMismatch++;
                        continue;
                    }

                    if (!cursor.TryGetValue(portType, out int idx))
                        idx = 0;
                    var entry = candidates[idx % candidates.Count];
                    cursor[portType] = idx + 1;

                    if (hasModule)
                    {
                        // Connector match already guaranteed by portType == ModuleSfpType.
                        try { link.RemoveSFP(); }
                        catch (System.Exception ex)
                        {
                            MelonLogger.Warning("[RealisticModules] RemoveSFP failed: " + ex.GetBaseException().Message);
                            continue;
                        }
                        didReplace = true;
                        // Give the remove postfix / visual teardown one frame.
                        yield return null;
                    }

                    var module = CreateModuleInstance(mgm, entry);
                    if (module == null)
                    {
                        noTemplate++;
                        continue;
                    }

                    bool ok;
                    try
                    {
                        link.InsertSFP(entry.SpeedInternal, entry.ModuleSfpType, module);
                        ok = true;
                    }
                    catch (System.Exception ex)
                    {
                        MelonLogger.Warning("[RealisticModules] InsertSFP failed: " + ex.GetBaseException().Message);
                        Object.Destroy(module.gameObject);
                        ok = false;
                    }

                    if (ok)
                    {
                        inserted++;
                        if (didReplace) replaced++;
                        yield return null;
                    }
                }
            }
            finally
            {
                _running = false;
            }

            string msg = $"Mass insert done: {inserted} filled" +
                         (replaceOccupied ? $", {replaced} replaced" : "") +
                         (skippedCabled > 0 ? $", {skippedCabled} cabled skipped" : "") +
                         (skippedMismatch > 0 ? $", {skippedMismatch} no matching type" : "") +
                         (noTemplate > 0 ? $", {noTemplate} missing templates" : "") +
                         ".";
            MelonLogger.Msg("[RealisticModules] " + msg);
            Notify(msg);
        }

        private static List<CableLink> CollectSfpPorts()
        {
            var result = new List<CableLink>();
            var seen = new HashSet<long>();
            try
            {
                var all = Resources.FindObjectsOfTypeAll<CableLink>();
                if (all == null) return result;
                foreach (var link in all)
                {
                    if (link == null) continue;
                    try
                    {
                        var go = link.gameObject;
                        if (go == null || !go.scene.IsValid() || !go.scene.isLoaded) continue;
                        if (!link.isSFPPort) continue;
                        long id = go.GetInstanceID();
                        if (!seen.Add(id)) continue;
                        result.Add(link);
                    }
                    catch { /* best-effort */ }
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning("[RealisticModules] Port scan failed: " + ex.GetBaseException().Message);
            }
            return result;
        }

        // port sfpType → catalog entries with ModuleSfpType == that type.
        // Stable first, then Legacy; skips experimental unless offered.
        private static Dictionary<int, List<ModuleRegistry.Entry>> BuildModulesByPortType()
        {
            var map = new Dictionary<int, List<ModuleRegistry.Entry>>();
            foreach (var pair in ModuleRegistry.Entries)
            {
                var entry = pair.Value;
                var def = entry.Definition;
                if (def.Lifecycle == ModuleLifecycle.Experimental && !Core.OfferExperimentalModules)
                    continue;
                if (entry.ModuleSfpType < 0) continue;

                if (!map.TryGetValue(entry.ModuleSfpType, out var list))
                {
                    list = new List<ModuleRegistry.Entry>();
                    map[entry.ModuleSfpType] = list;
                }
                list.Add(entry);
            }

            foreach (var list in map.Values)
            {
                list.Sort((a, b) =>
                {
                    int la = Rank(a.Definition.Lifecycle);
                    int lb = Rank(b.Definition.Lifecycle);
                    if (la != lb) return la - lb;
                    return string.CompareOrdinal(a.Definition.DisplayName, b.Definition.DisplayName);
                });
            }
            return map;

            static int Rank(ModuleLifecycle life) =>
                life == ModuleLifecycle.Stable ? 0 :
                life == ModuleLifecycle.Legacy ? 1 : 2;
        }

        private static SFPModule CreateModuleInstance(MainGameManager mgm, ModuleRegistry.Entry entry)
        {
            var prefabs = mgm.sfpPrefabs;
            int id = entry.Definition.PrefabId;
            if (prefabs == null || id < 0 || id >= prefabs.Length) return null;
            var template = prefabs[id];
            if (template == null) return null;

            var clone = Object.Instantiate(template);
            clone.name = $"SFPModule_custom_{id}";
            clone.SetActive(true);

            var usable = clone.GetComponent<UsableObject>();
            if (usable != null) usable.prefabID = id;

            var sfp = clone.GetComponent<SFPModule>();
            if (sfp != null)
            {
                sfp.speed = entry.SpeedInternal;
                sfp.sfpType = entry.ModuleSfpType;
                sfp.isInTheBox = false;
            }

            ModuleRegistry.RememberLiveModule(clone, id);
            Core.ApplyModuleTint(clone, id);
            return sfp;
        }

        private static void Notify(string message)
        {
            if (!GregHost.HasCore) return;
            NotifyCore(message);
        }

        // Separate method: the JIT resolves gregCore types only when this
        // runs, which is exclusively behind the HasCore probe above. Calling
        // GregNotificationManager from inside Notify would crash at JIT time
        // when gregCore.dll is absent (guard runs too late).
        private static void NotifyCore(string message)
        {
            try
            {
                gregCore.UI.GregNotificationManager.Show(message, 5f);
            }
            catch { /* best-effort */ }
        }
    }
}
