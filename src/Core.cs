using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using Math = System.Math;
using System.Collections;
using System.Collections.Generic;

[assembly: MelonInfo(typeof(GregModMoreModules.Core), "gregMod.RealisticModules", "1.3.0", "TeamGreg Modding (leoms1408 / mleem97)")]
[assembly: MelonGame("Waseku", "Data Center")]

namespace GregModMoreModules
{
    public class Core : MelonMod
    {
        // Sprite from the vanilla QSFP+ shop entry — reused as icon for all custom modules.
        internal static Sprite BaseQsfpSprite;

        // sfpType of the vanilla QSFP+ module (form-factor; determines port compatibility).
        internal static int BaseQsfpSfpType = -1;

        // prefabID of the vanilla QSFP+ module — used as clone source in BuildModulePrefab.
        internal static int BaseQsfpPrefabID = -1;

        // Index into mgm.sfpsBoxedPrefab of the vanilla QSFP+/Fibre 40G box —
        // clone source for BuildBoxPrefab. Separate from BaseQsfpPrefabID because
        // module prefabIDs and box array indices are different ID spaces.
        internal static int BaseBoxPrefabIndex = -1;

        // Explicit save IDs from ModuleCatalog (110+/210+). Tray packages use
        // a separate range so bulk IDs stay stable for existing saves.
        internal const int MOD_ID_BASE  = ModuleCatalog.FirstNewPrefabId;
        internal const int BULK_ID_BASE = ModuleCatalog.FirstNewBulkId;
        internal const int TRAY_ID_BASE = 310;

        // Piece counts ("trays") per module — in addition to the 5x box and the 32x bulk.
        internal const int TraySizeCount = 4;
        internal static readonly int[] TraySizes = { 16, 32, 64, 128 };

        // Inactive holder for prefab templates — parenting templates here makes their
        // activeInHierarchy = false, so the game's UsableObject tracker ignores them.
        internal static GameObject TemplateHolder { get; private set; }
        internal static CompatibilityMode CompatibilityMode { get; private set; } = CompatibilityMode.SimplifiedCompatibility;
        internal static bool OfferExperimentalModules { get; private set; }
        internal static bool IsSetupComplete { get; private set; }
        private static readonly Dictionary<int, int> ExtendedShopRowsByParent = new Dictionary<int, int>();

        public override void OnInitializeMelon()
        {
            ModConfig.Load();
            RefreshCompatibilityMode();
            OfferExperimentalModules = ModConfig.OfferExperimentalModules;
            MelonLogger.Msg($"Compatibility mode: {CompatibilityMode}");
            MelonLogger.Msg($"Experimental modules: {(OfferExperimentalModules ? "offered" : "hidden")}");
            MelonLogger.Msg($"Mod active: {(ModConfig.Enabled ? "yes" : "no")}");

            if (GregHost.HasCore)
                RegisterCoreExtras();
            else
                MelonLogger.Msg("[RealisticModules] gregCore not present — F1 menu skipped.");
        }

        // Separate method: JIT will not load gregCore types unless called.
        private static void RegisterCoreExtras()
        {
            GregCoreIntegration.Register();
        }

        internal static bool IsEnabled => ModConfig.Enabled;

        internal static void RefreshCompatibilityMode()
        {
            CompatibilityMode = ModConfig.StrictCompatibility
                ? CompatibilityMode.StrictCompatibility
                : CompatibilityMode.SimplifiedCompatibility;
        }

        // Soft disable while the game is already running: registry/shop inject
        // stop immediately. Extended sfpPrefabs stay until the next Awake.
        internal static void DisableAtRuntime()
        {
            IsSetupComplete = false;
            ModuleRegistry.Clear();
        }

        // True when the slot already holds a template we created earlier
        // (re-enable without a full rebuild).
        internal static bool IsOwnTemplate(GameObject go, int prefabId)
        {
            if (go == null) return false;
            string n = go.name;
            if (string.IsNullOrEmpty(n)) return false;
            return n == $"SFPModule_template_{prefabId}" ||
                   n == $"SFPModule_custom_{prefabId}";
        }

        // -----------------------------------------------------------------------
        // Diagnostic: dumps the full vanilla SFP module and SFP box prefab
        // catalogs so each custom tier can be mapped to its real vanilla type.
        // -----------------------------------------------------------------------
        private static void DumpVanillaCatalog(MainGameManager mgm)
        {
            MelonLogger.Msg("=== Vanilla SFP module catalog ===");
            var sfpPrefabs = mgm.sfpPrefabs;
            if (sfpPrefabs != null)
            {
                for (int i = 0; i < sfpPrefabs.Length; i++)
                {
                    var go = sfpPrefabs[i];
                    if (go == null) { MelonLogger.Msg($"[{i}] null"); continue; }
                    var sfpMod = go.GetComponent<SFPModule>();
                    var usable = go.GetComponent<UsableObject>();
                    float speed = sfpMod != null ? sfpMod.speed : -1f;
                    int   st    = sfpMod != null ? sfpMod.sfpType : -1;
                    int   pid   = usable != null ? usable.prefabID : -1;
                    string cell = speed >= 0f ? $"{speed * 5f:0}G" : "?";
                    MelonLogger.Msg($"[{i}] prefabID={pid} sfpType={st} speed={cell} name={go.name}");
                }
            }
            else
            {
                MelonLogger.Msg("(sfpPrefabs is null)");
            }

            MelonLogger.Msg("=== Vanilla SFPBox catalog ===");
            var boxes = mgm.sfpsBoxedPrefab;
            if (boxes != null)
            {
                for (int i = 0; i < boxes.Length; i++)
                {
                    var go = boxes[i];
                    if (go == null) { MelonLogger.Msg($"[{i}] null"); continue; }
                    var sfpBox = go.GetComponent<SFPBox>();
                    int bt = sfpBox != null ? sfpBox.sfpBoxType : -1;
                    MelonLogger.Msg($"[{i}] boxType={bt} name={go.name}");
                }
            }
            else
            {
                MelonLogger.Msg("(sfpsBoxedPrefab is null)");
            }
        }

        // -----------------------------------------------------------------------
        // Scans vanilla sfpPrefabs for the real QSFP+ module (highest-speed
        // vanilla-named entry) and the matching Fibre 40G box, stores them as
        // clone sources, then extends sfpPrefabs with one slot per custom module.
        //
        // Base selection only considers vanilla-named entries (SFP_*). Without
        // that filter a pre-extended array is picked as "highest speed".
        // -----------------------------------------------------------------------
        internal static void SetupRegistry(MainGameManager mgm)
        {
            IsSetupComplete = false;
            ModuleRegistry.Clear();
            BaseQsfpPrefabID = -1;
            BaseQsfpSfpType = -1;
            BaseBoxPrefabIndex = -1;

            if (!ModConfig.Enabled)
            {
                MelonLogger.Msg("RealisticModules disabled via config — skipping setup.");
                return;
            }

            if (!ModuleValidation.ValidateCatalog())
            {
                MelonLogger.Error("Realistic module catalog validation failed — mod disabled.");
                return;
            }

            var sfpPrefabs = mgm.sfpPrefabs;
            if (sfpPrefabs == null || sfpPrefabs.Length == 0)
            {
                MelonLogger.Warning("sfpPrefabs is empty — skipping setup.");
                return;
            }

            MelonLogger.Msg($"sfpPrefabs length: {sfpPrefabs.Length}");

            float highestVanillaSpeed = -1f;
            int vanillaCount = 0;

            for (int i = 0; i < sfpPrefabs.Length && i < MOD_ID_BASE; i++)
            {
                var go = sfpPrefabs[i];
                if (go == null) continue;

                // Vanilla catalog only: SFP_RJ45 / SFP_fabric* / SFP_QSFP.
                // Reject CustomSFP_*, SFPModule_custom_*, SFPModule_template_*.
                if (!IsVanillaSfpName(go.name)) continue;

                vanillaCount = i + 1;

                var sfpMod    = go.GetComponent<SFPModule>();
                var usableObj = go.GetComponent<UsableObject>();
                float speed   = sfpMod    != null ? sfpMod.speed       : -1f;
                int   sfpType = sfpMod    != null ? sfpMod.sfpType     : -1;
                int   pid     = usableObj != null ? usableObj.prefabID : -1;

                if (speed > highestVanillaSpeed)
                {
                    highestVanillaSpeed = speed;
                    BaseQsfpSfpType     = sfpType;
                    BaseQsfpPrefabID    = pid;
                }
            }

            // Fallback when the name filter matched nothing (unusual layouts).
            if (BaseQsfpPrefabID < 0)
            {
                int nativeCount = FindNativePrefabCount(sfpPrefabs);
                for (int i = 0; i < nativeCount; i++)
                {
                    var go = sfpPrefabs[i];
                    if (go == null) continue;
                    var sfpMod = go.GetComponent<SFPModule>();
                    var usableObj = go.GetComponent<UsableObject>();
                    float speed = sfpMod != null ? sfpMod.speed : -1f;
                    if (speed > highestVanillaSpeed)
                    {
                        highestVanillaSpeed = speed;
                        BaseQsfpSfpType = sfpMod != null ? sfpMod.sfpType : -1;
                        BaseQsfpPrefabID = usableObj != null ? usableObj.prefabID : -1;
                    }
                    vanillaCount = i + 1;
                }
            }

            if (BaseQsfpPrefabID < 0)
            {
                MelonLogger.Error("Could not identify base QSFP+ prefab.");
                return;
            }

            MelonLogger.Msg($"Base QSFP+: prefabID={BaseQsfpPrefabID}, " +
                            $"sfpType={BaseQsfpSfpType}, {highestVanillaSpeed * 5f} Gbps, " +
                            $"vanillaCount={vanillaCount}");

            BaseBoxPrefabIndex = FindBaseBoxIndex(mgm);
            MelonLogger.Msg($"Base box index: {BaseBoxPrefabIndex}" +
                            (BaseBoxPrefabIndex >= 0 && mgm.sfpsBoxedPrefab != null &&
                             BaseBoxPrefabIndex < mgm.sfpsBoxedPrefab.Length &&
                             mgm.sfpsBoxedPrefab[BaseBoxPrefabIndex] != null
                                ? $" ({mgm.sfpsBoxedPrefab[BaseBoxPrefabIndex].name})"
                                : " (missing)"));

            DumpVanillaCatalog(mgm);

            // Create/recreate the inactive holder that hides templates from the world system.
            if (TemplateHolder != null)
                Object.Destroy(TemplateHolder);
            TemplateHolder = new GameObject("gregModRealisticModules_TemplateHolder");
            TemplateHolder.SetActive(false);
            Object.DontDestroyOnLoad(TemplateHolder);

            if (vanillaCount > MOD_ID_BASE)
            {
                MelonLogger.Error($"vanillaCount={vanillaCount} exceeds " +
                                  $"MOD_ID_BASE={MOD_ID_BASE}! prefabID collision risk — mod disabled.");
                return;
            }

            foreach (var def in ModuleCatalog.Legacy)
            {
                if (IsPrefabSlotAvailable(sfpPrefabs, def.PrefabId))
                    ModuleRegistry.Register(def.PrefabId,
                        new ModuleRegistry.Entry(def, BaseQsfpSfpType, def.BasePrefabID, 5, def.BaseBoxIndex));
                else
                    MelonLogger.Msg($"Legacy prefabID={def.PrefabId} retained by the game; alias skipped.");
            }

            // Older MoreModules builds used 1000–1006. Keep aliases loadable.
            for (int i = 0; i < ModuleCatalog.Legacy.Length; i++)
            {
                var legacy = ModuleCatalog.Legacy[i];
                var alias = new ModuleDefinition
                {
                    PrefabId = 1000 + i, BulkItemId = 0, StableId = legacy.StableId + "_alias",
                    DisplayName = legacy.DisplayName, FormFactor = legacy.FormFactor, Media = legacy.Media,
                    EthernetStandard = legacy.EthernetStandard, SpeedGbps = legacy.SpeedGbps,
                    MaxReachMeters = legacy.MaxReachMeters, PowerWatts = legacy.PowerWatts,
                    Connector = legacy.Connector, ElectricalLaneCount = legacy.ElectricalLaneCount,
                    LaneSpeedGbps = legacy.LaneSpeedGbps, PriceMultiplier = 1, ShopGuid = legacy.ShopGuid,
                    ModuleColor = legacy.ModuleColor, Lifecycle = ModuleLifecycle.Legacy,
                    BasePrefabID = legacy.BasePrefabID, BaseBoxIndex = legacy.BaseBoxIndex,
                };
                if (IsPrefabSlotAvailable(sfpPrefabs, alias.PrefabId))
                    ModuleRegistry.Register(alias.PrefabId,
                        new ModuleRegistry.Entry(alias, BaseQsfpSfpType, alias.BasePrefabID, 5, alias.BaseBoxIndex));
                else
                    MelonLogger.Msg($"Legacy alias prefabID={alias.PrefabId} already exists; alias skipped.");
            }

            foreach (var def in ModuleList.All)
            {
                if (def.Lifecycle == ModuleLifecycle.Experimental && !OfferExperimentalModules)
                    continue;

                if (!IsPrefabSlotAvailable(sfpPrefabs, def.PrefabId))
                {
                    var existing = def.PrefabId >= 0 && def.PrefabId < sfpPrefabs.Length
                        ? sfpPrefabs[def.PrefabId]
                        : null;
                    if (IsOwnTemplate(existing, def.PrefabId))
                    {
                        int formSfpType0 = ResolveFormSfpType(mgm, def, vanillaCount);
                        if (formSfpType0 < 0) formSfpType0 = BaseQsfpSfpType;
                        ModuleRegistry.Register(def.PrefabId,
                            new ModuleRegistry.Entry(def, formSfpType0, def.BasePrefabID, 5, def.BaseBoxIndex));
                        MelonLogger.Msg($"Re-registered '{def.DisplayName}' from existing template.");
                        continue;
                    }

                    MelonLogger.Error($"PrefabID {def.PrefabId} is already occupied; " +
                                       $"skipping '{def.DisplayName}'.");
                    continue;
                }

                int formSfpType = ResolveFormSfpType(mgm, def, vanillaCount);
                if (formSfpType < 0)
                {
                    // Form base missing — still register with the global QSFP+ type.
                    MelonLogger.Warning($"No vanilla base for '{def.DisplayName}' " +
                                        $"(BasePrefabID={def.BasePrefabID}); using global QSFP+ sfpType.");
                    formSfpType = BaseQsfpSfpType;
                }

                ModuleRegistry.Register(def.PrefabId,
                    new ModuleRegistry.Entry(def, formSfpType, def.BasePrefabID, 5, def.BaseBoxIndex));
                MelonLogger.Msg($"Registered '{def.DisplayName}': prefabID={def.PrefabId}, " +
                                $"{def.SpeedGbps} Gbps, sfpType={formSfpType}");
            }

            int maxPrefabId = Math.Max(ModuleRegistry.MaxKnownId, sfpPrefabs.Length - 1);
            var extended = new GameObject[maxPrefabId + 1];
            for (int i = 0; i < sfpPrefabs.Length; i++) extended[i] = sfpPrefabs[i];

            foreach (var pair in ModuleRegistry.Entries)
            {
                int id = pair.Key;
                var entry = pair.Value;
                if (id < extended.Length && IsOwnTemplate(extended[id], id))
                    continue;
                var template = BuildModulePrefab(mgm, id, entry, TemplateHolder.transform);
                if (template != null) template.name = $"SFPModule_template_{id}";
                if (template != null)
                    extended[id] = template;
            }

            mgm.sfpPrefabs = extended;
            MelonLogger.Msg($"sfpPrefabs extended: {sfpPrefabs.Length} → {extended.Length}");
            IsSetupComplete = ModuleRegistry.Entries.Count > 0;
        }

        internal static bool IsSetupCompleteFor(MainGameManager mgm)
        {
            if (!IsSetupComplete || mgm?.sfpPrefabs == null || ModuleRegistry.Entries.Count == 0)
                return false;

            foreach (var pair in ModuleRegistry.Entries)
            {
                if (pair.Key < 0 || pair.Key >= mgm.sfpPrefabs.Length || mgm.sfpPrefabs[pair.Key] == null)
                    return false;
            }

            return true;
        }

        // sfpType of the vanilla prefab with the definition's BasePrefabID
        // (form factor). -1 when the base is missing.
        private static int ResolveFormSfpType(MainGameManager mgm, ModuleDefinition def, int vanillaCount)
        {
            var sfpPrefabs = mgm.sfpPrefabs;
            if (sfpPrefabs == null) return -1;
            for (int i = 0; i < sfpPrefabs.Length && i < vanillaCount; i++)
            {
                var go = sfpPrefabs[i];
                if (go == null || !IsVanillaSfpName(go.name)) continue;
                var sfpMod = go.GetComponent<SFPModule>();
                var usableObj = go.GetComponent<UsableObject>();
                if (usableObj != null && usableObj.prefabID == def.BasePrefabID && sfpMod != null)
                    return sfpMod.sfpType;
            }

            return -1;
        }

        // Vanilla module shop/object names only (SFP_RJ45, SFP_fabric*, SFP_QSFP).
        private static bool IsVanillaSfpName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (name.StartsWith("SFP_", System.StringComparison.Ordinal)) return true;
            return false;
        }

        // Clone source for boxes: prefer highest vanilla boxType (Fibre 40G = 3),
        // else last non-null entry. Never use BaseQsfpPrefabID (module ID space).
        private static int FindBaseBoxIndex(MainGameManager mgm)
        {
            var boxes = mgm.sfpsBoxedPrefab;
            if (boxes == null || boxes.Length == 0) return -1;

            int best = -1;
            int bestBoxType = -1;
            int lastNonNull = -1;

            for (int i = 0; i < boxes.Length; i++)
            {
                var go = boxes[i];
                if (go == null) continue;
                lastNonNull = i;
                var sfpBox = go.GetComponent<SFPBox>();
                int bt = sfpBox != null ? sfpBox.sfpBoxType : -1;
                if (bt > bestBoxType)
                {
                    bestBoxType = bt;
                    best = i;
                }
            }

            return best >= 0 ? best : lastNonNull;
        }

        private static int FindNativePrefabCount(Il2CppReferenceArray<GameObject> prefabs)
        {
            int count = 0;
            while (count < prefabs.Length && prefabs[count] != null)
                count++;

            if (count > 0) return count;

            int highestNativeId = -1;
            int scanLimit = Math.Min(prefabs.Length, MOD_ID_BASE);
            for (int i = 0; i < scanLimit; i++)
            {
                var go = prefabs[i];
                var usable = go?.GetComponent<UsableObject>();
                if (usable != null && usable.prefabID == i)
                    highestNativeId = i;
            }

            return highestNativeId + 1;
        }

        private static bool IsPrefabSlotAvailable(Il2CppReferenceArray<GameObject> prefabs, int prefabId)
        {
            return prefabId >= prefabs.Length || prefabs[prefabId] == null;
        }

        // -----------------------------------------------------------------------
        // Clones the vanilla form-factor module prefab and applies our custom
        // speed and prefabID. parent: non-null → inactive template holder.
        // -----------------------------------------------------------------------
        internal static GameObject BuildModulePrefab(MainGameManager mgm, int prefabID,
                                                     ModuleRegistry.Entry entry,
                                                     Transform parent = null)
        {
            int baseIndex = entry.BasePrefabID >= 0 ? entry.BasePrefabID : BaseQsfpPrefabID;
            if (baseIndex < 0 || baseIndex >= mgm.sfpPrefabs.Length)
            {
                MelonLogger.Error($"Base prefab [{baseIndex}] out of range.");
                return null;
            }

            var basePrefab = mgm.sfpPrefabs[baseIndex];
            if (basePrefab == null)
            {
                MelonLogger.Error($"Base prefab [{baseIndex}] is null.");
                return null;
            }

            var clone = parent != null
                ? Object.Instantiate(basePrefab, parent, false)
                : Object.Instantiate(basePrefab);
            clone.name = $"SFPModule_custom_{prefabID}";

            var sfpMod = clone.GetComponent<SFPModule>();
            if (sfpMod != null)
                sfpMod.speed = entry.SpeedInternal;

            var usableObj = clone.GetComponent<UsableObject>();
            if (usableObj != null)
                usableObj.prefabID = prefabID;

            ApplyModuleTint(clone, prefabID);

            return clone;
        }

        // -----------------------------------------------------------------------
        // Recolors materials named "Blue" to the catalog tint for this prefabID.
        // -----------------------------------------------------------------------
        internal static void ApplyModuleTint(GameObject root, int prefabID)
        {
            if (root == null) return;

            if (!ModuleRegistry.TryGet(prefabID, out var entry)) return;
            Color tint = entry.Definition.ModuleColor;

            string[] colorProps = { "_Color", "_BaseColor", "_MainColor", "_TintColor", "_Tint", "_AlbedoColor" };

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var rend in renderers)
            {
                if (rend == null) continue;
                var mats = rend.materials;
                bool changed = false;

                for (int m = 0; m < mats.Length; m++)
                {
                    if (mats[m] == null) continue;
                    if (!mats[m].name.Contains("Blue")) continue;

                    foreach (var prop in colorProps)
                    {
                        if (mats[m].HasProperty(prop))
                            mats[m].SetColor(prop, tint);
                    }
                    changed = true;
                }

                if (changed) rend.materials = mats;
            }
        }

        // -----------------------------------------------------------------------
        // Clones the vanilla box prefab matching the definition's form factor
        // (BaseBoxIndex / global BaseBoxPrefabIndex) and applies custom types.
        // -----------------------------------------------------------------------
        internal static GameObject BuildBoxPrefab(MainGameManager mgm, int prefabID,
                                                  ModuleRegistry.Entry entry,
                                                  Transform parent = null)
        {
            var boxPrefabs = mgm.sfpsBoxedPrefab;
            if (boxPrefabs == null) return null;

            int wantBox = entry.BaseBoxIndex >= 0 ? entry.BaseBoxIndex : BaseBoxPrefabIndex;
            GameObject baseBox = null;
            if (wantBox >= 0 && wantBox < boxPrefabs.Length)
                baseBox = boxPrefabs[wantBox];

            // Fall back to the last non-null box (highest vanilla boxType).
            if (baseBox == null)
                for (int i = boxPrefabs.Length - 1; i >= 0; i--)
                    if (boxPrefabs[i] != null) { baseBox = boxPrefabs[i]; break; }

            if (baseBox == null)
            {
                MelonLogger.Warning("No base box prefab found.");
                return null;
            }

            var clone = parent != null
                ? Object.Instantiate(baseBox, parent, false)
                : Object.Instantiate(baseBox);
            clone.name = $"SFPBox_custom_{prefabID}";

            var sfpBox = clone.GetComponent<SFPBox>();
            if (sfpBox != null)
                sfpBox.sfpBoxType = prefabID;

            var usableObj = clone.GetComponent<UsableObject>();
            if (usableObj != null)
                usableObj.prefabID = prefabID;

            // Only update speed on children — do NOT set prefabID (world tracker
            // would spawn loose modules). Identity is fixed at insertion time.
            foreach (var childModule in clone.GetComponentsInChildren<SFPModule>())
            {
                childModule.speed = entry.SpeedInternal;
                childModule.gameObject.name = $"realisticModule_{prefabID}_child";
                ModuleRegistry.RememberLiveModule(childModule.gameObject, prefabID);
                ApplyModuleTint(childModule.gameObject, prefabID);
            }

            return clone;
        }

        // -----------------------------------------------------------------------
        // Triggered on every scene load. Starts the shop injection coroutine for
        // any scene other than the main menu (buildIndex 0).
        // -----------------------------------------------------------------------
        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            // A running box scan is cancelled on scene change;
            // reset the flag so future deliveries expand again.
            _boxScannerRunning = false;

            if (!ModConfig.Enabled)
            {
                IsSetupComplete = false;
                ModuleRegistry.Clear();
                return;
            }

            if (buildIndex != 0)
                MelonCoroutines.Start(AddShopItems());
        }

        // -----------------------------------------------------------------------
        // Waits for the shop to finish initializing, then injects a shop button
        // for each registered custom module: 5x box + trays 16/32/64/128.
        // -----------------------------------------------------------------------
        private IEnumerator AddShopItems()
        {
            if (!ModConfig.Enabled || !IsSetupComplete)
                yield break;

            const int maxAttempts = 10;
            ShopItem sourceItem = null;
            ComputerShop computerShop = null;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                if (attempt > 0) yield return new WaitForSeconds(3f);
                else yield return new WaitForSeconds(1.5f);

                var mgm = MainGameManager.instance;
                if (mgm == null) continue;

                computerShop = mgm.computerShop;
                if (computerShop == null) continue;

                sourceItem = FindShopTemplate(computerShop);
                if (sourceItem != null) break;

                if (attempt == 0)
                    LoggerInstance.Warning("No SFP shop template yet — retrying (shop may build lazily).");
            }

            if (sourceItem == null || computerShop == null)
            {
                LoggerInstance.Warning("No shop template found after retries — shop buttons skipped.");
                yield break;
            }

            var shopRoot = computerShop.shopItemParent;
            if (shopRoot == null) { LoggerInstance.Warning("shopItemParent null."); yield break; }

            var sfpParent = sourceItem.transform.parent != null
                ? sourceItem.transform.parent.gameObject
                : shopRoot;

            // Target row count: 1 x 5-pack + 4 tray packs (16/32/64/128) per module.
            int packagesPerModule = 1 + TraySizeCount;
            var customRows = EnsureCustomSfpRows(shopRoot, sfpParent,
                                                 ModuleList.All.Length * packagesPerModule);
            if (customRows.Count < 0)
                LoggerInstance.Warning("'HL Mods' not found — falling back to shopItemParent.");

            float itemHeight = 0f;
            var sourceRt = sourceItem.GetComponent<UnityEngine.RectTransform>();
            if (sourceRt != null)
                itemHeight = sourceRt.rect.height;

            int addedSfpCount = 0;
            int packageIndex  = 0;

            var formTemplates = new Dictionary<int, ShopItem>();

            for (int i = 0; i < ModuleList.All.Length; i++)
            {
                var def      = ModuleList.All[i];
                int prefabID = def.PrefabId;
                if (!ModuleRegistry.TryGet(prefabID, out _)) continue;

                var formTemplate = FormShopTemplate(computerShop, sourceItem, formTemplates, def.BaseBoxIndex);
                int basePrice = formTemplate != null && formTemplate.shopItemSO != null
                    ? formTemplate.shopItemSO.price
                    : sourceItem.shopItemSO.price;
                Sprite formSprite = ResolveFormSprite(formTemplate);

                // 5x pack (standard).
                var added5 = AddShopPackage(computerShop, formTemplate ?? sourceItem,
                                            RowForPackage(customRows, sfpParent, packageIndex),
                                            prefabID,
                                            BuildShopLabel("5x", def),
                                            (int)(basePrice * def.PriceMultiplier),
                                            def.XpToUnlock, def.ShopGuid, formSprite);
                if (added5 != null) addedSfpCount++;
                packageIndex++;

                // Tray packs 16 / 32 / 64 / 128 pcs — in addition to the 5x box.
                for (int s = 0; s < TraySizeCount; s++)
                {
                    int cap         = TraySizes[s];
                    int trayItemID  = TRAY_ID_BASE + i * TraySizeCount + s;
                    int trayPrice   = (int)(basePrice * def.PriceMultiplier * (cap / 5f));

                    var addedTray = AddShopPackage(computerShop, formTemplate ?? sourceItem,
                                                   RowForPackage(customRows, sfpParent, packageIndex),
                                                   trayItemID,
                                                   BuildShopLabel($"{cap}x", def),
                                                   trayPrice,
                                                   def.XpToUnlock,
                                                   def.ShopGuid + $"_{cap}x", formSprite);
                    if (addedTray != null) addedSfpCount++;
                    packageIndex++;
                }
            }

            EnsureBackplaneTopSpacer(computerShop, shopRoot, sfpParent, itemHeight);

            ExtendVerticalContainer(shopRoot, itemHeight, customRows.Count);

            RebuildShopLayout(shopRoot);
        }

        // Prefer the Fibre 40G / QSFP+ box (itemID == BaseBoxPrefabIndex), then
        // highest itemID box, then any box. itemID for vanilla boxes tracks the
        // box array index, not the module prefabID.
        private static ShopItem FindShopTemplate(ComputerShop computerShop)
        {
            ShopItem exactBox = null;
            ShopItem bestIdBox = null;
            ShopItem anyBox = null;
            int arrayCount = 0;
            int hierarchyBoxes = 0;
            bool yieldedAny = false;

            var items = computerShop.shopItems;
            if (items != null)
            {
                foreach (var si in items)
                {
                    if (si == null || si.shopItemSO == null) continue;
                    arrayCount++;
                    if ((int)si.shopItemSO.itemType != 9) continue;
                    yieldedAny = true;
                    (exactBox, bestIdBox, anyBox) = PreferBox(si, exactBox, bestIdBox, anyBox);
                }
            }

            if (!yieldedAny)
            {
                var root = computerShop.shopItemParent;
                var all = root != null ? root.GetComponentsInChildren<ShopItem>(true) : null;
                if (all != null)
                {
                    foreach (var si in all)
                    {
                        if (si == null || si.shopItemSO == null) continue;
                        if ((int)si.shopItemSO.itemType != 9) continue;
                        hierarchyBoxes++;
                        (exactBox, bestIdBox, anyBox) = PreferBox(si, exactBox, bestIdBox, anyBox);
                    }
                }
            }

            ShopItem picked = exactBox ?? bestIdBox ?? anyBox;
            if (picked != null)
            {
                if (picked.shopItemSO.sprite != null)
                    BaseQsfpSprite = picked.shopItemSO.sprite;
                string tier = picked == exactBox
                    ? $"exact box itemID={picked.shopItemSO.itemID}"
                    : $"SFP box (itemID={picked.shopItemSO.itemID})";
                MelonLoader.MelonLogger.Msg($"Shop template: {tier}.");
            }
            else
            {
                MelonLoader.MelonLogger.Msg(
                    $"Shop scan: {arrayCount} array items, {hierarchyBoxes} hierarchy boxes — no SFP box yet.");
            }
            return picked;
        }

        private static (ShopItem exact, ShopItem bestId, ShopItem any) PreferBox(
            ShopItem si, ShopItem exact, ShopItem bestId, ShopItem any)
        {
            int id = si.shopItemSO != null ? si.shopItemSO.itemID : -1;
            if (BaseBoxPrefabIndex >= 0 && id == BaseBoxPrefabIndex)
                exact = si;
            if (bestId == null || id > (bestId.shopItemSO != null ? bestId.shopItemSO.itemID : -1))
                bestId = si;
            if (any == null)
                any = si;
            return (exact, bestId, any);
        }

        private static List<GameObject> EnsureCustomSfpRows(GameObject shopRoot,
                                                            GameObject templateRow,
                                                            int itemCount)
        {
            var rows = new List<GameObject>();
            if (shopRoot == null || templateRow == null) return rows;

            int rowCount = Mathf.CeilToInt(itemCount / 4f);
            int insertIndex = templateRow.transform.GetSiblingIndex() + 1;

            for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                string rowName = $"HL gregMod.RealisticModules {rowIndex + 1}";
                var existing = shopRoot.transform.Find(rowName);
                GameObject row = existing != null ? existing.gameObject : null;

                if (row == null)
                {
                    row = Object.Instantiate(templateRow, shopRoot.transform, false);
                    row.name = rowName;
                    ClearRowChildren(row);
                }

                row.transform.SetSiblingIndex(insertIndex + rowIndex);
                row.SetActive(true);
                rows.Add(row);
            }

            return rows;
        }

        private static void ClearRowChildren(GameObject row)
        {
            if (row == null) return;

            for (int i = row.transform.childCount - 1; i >= 0; i--)
            {
                var child = row.transform.GetChild(i);
                child.SetParent(null, false);
                Object.Destroy(child.gameObject);
            }
        }

        private static GameObject RowForPackage(List<GameObject> customRows,
                                                GameObject fallback, int packageIndex)
        {
            return customRows.Count > 0
                ? customRows[Mathf.Min(packageIndex / 4, customRows.Count - 1)]
                : fallback;
        }

        private static string BuildShopLabel(string quantity, ModuleDefinition def)
        {
            string speed = $"{def.SpeedGbps:0}Gbps";
            string moduleName = def.DisplayName;
            if (moduleName.EndsWith(speed))
                moduleName = moduleName.Substring(0, moduleName.Length - speed.Length).TrimEnd();

            return $"{quantity} {moduleName} {def.EthernetStandard} · {speed} · {def.Media} · {def.MaxReachMeters:0}m";
        }

        // Shop template per box form factor (cached). Fallback: QSFP+ template.
        private static ShopItem FormShopTemplate(ComputerShop computerShop, ShopItem fallback,
                                                 Dictionary<int, ShopItem> cache, int boxIndex)
        {
            int key = boxIndex >= 0 ? boxIndex : BaseBoxPrefabIndex;
            if (cache.TryGetValue(key, out var cached)) return cached;
            ShopItem found = null;
            try
            {
                var items = computerShop.shopItems;
                if (items != null && key >= 0)
                {
                    foreach (var si in items)
                    {
                        if (si == null || si.shopItemSO == null) continue;
                        if ((int)si.shopItemSO.itemType != 9) continue;
                        if (si.shopItemSO.itemID == key) { found = si; break; }
                    }
                }
            }
            catch { found = null; }

            if (found == null) found = fallback;
            cache[key] = found;
            return found;
        }

        private static Sprite ResolveFormSprite(ShopItem formTemplate)
        {
            try
            {
                if (formTemplate != null && formTemplate.shopItemSO != null &&
                    formTemplate.shopItemSO.sprite != null)
                    return formTemplate.shopItemSO.sprite;
            }
            catch { }
            return BaseQsfpSprite;
        }

        private static void ExtendVerticalContainer(GameObject parent, float itemHeight, int addedRows)
        {
            if (parent == null || itemHeight <= 0f || addedRows <= 0) return;

            var containerRt = parent.GetComponent<UnityEngine.RectTransform>();
            if (containerRt == null) return;

            int instanceId = parent.GetInstanceID();
            ExtendedShopRowsByParent.TryGetValue(instanceId, out int alreadyAddedRows);
            int rowsToAdd = addedRows - alreadyAddedRows;
            if (rowsToAdd <= 0) return;

            var sd = containerRt.sizeDelta;
            sd.y += itemHeight * rowsToAdd;
            containerRt.sizeDelta = sd;
            ExtendedShopRowsByParent[instanceId] = addedRows;
        }

        private static void EnsureBackplaneTopSpacer(ComputerShop computerShop,
                                                     GameObject shopRoot,
                                                     GameObject templateRow,
                                                     float itemHeight)
        {
            if (shopRoot == null || templateRow == null) return;
            if (!HasBoostedSystemXItems(computerShop) && !IsBackplaneBoostServersLoaded()) return;

            const string spacerName = "HL Backplane Top Padding";
            var existing = shopRoot.transform.Find(spacerName);
            GameObject spacer = existing != null ? existing.gameObject : null;

            if (spacer == null)
            {
                spacer = Object.Instantiate(templateRow, shopRoot.transform, false);
                spacer.name = spacerName;
                ClearRowChildren(spacer);
                MelonLogger.Msg("Added Backplane shop top padding for clipped SystemX server row.");
            }

            float height = Mathf.Max(160f, itemHeight * 0.7f);

            spacer.transform.SetSiblingIndex(0);
            spacer.SetActive(true);

            var rt = spacer.GetComponent<RectTransform>();
            if (rt != null)
            {
                var sd = rt.sizeDelta;
                sd.y = height;
                rt.sizeDelta = sd;
            }

            var layout = spacer.GetComponent<LayoutElement>();
            if (layout == null)
                layout = spacer.AddComponent<LayoutElement>();

            layout.ignoreLayout = false;
            layout.minHeight = height;
            layout.preferredHeight = height;
            layout.flexibleHeight = 0f;
        }

        private static bool HasBoostedSystemXItems(ComputerShop computerShop)
        {
            var items = computerShop?.shopItems;
            if (items == null) return false;

            foreach (var item in items)
            {
                if (item == null) continue;

                string name = item.itemDisplayName;
                if (string.IsNullOrEmpty(name) && item.txtName != null)
                    name = item.txtName.text;
                if (string.IsNullOrEmpty(name)) continue;

                if (name.Contains("SystemX") &&
                    (name.Contains("100K") || name.Contains("125K") || name.Contains("500K")))
                    return true;
            }

            return false;
        }

        private static bool IsBackplaneBoostServersLoaded()
        {
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var name = assembly.GetName().Name;
                if (string.IsNullOrEmpty(name)) continue;
                if (name.Contains("BackplaneBoostServers") ||
                    name.Contains("DataCenterAutomatorServers") ||
                    name.Contains("gregMod.Backplanes"))
                    return true;
            }

            return false;
        }

        private static void RebuildShopLayout(GameObject shopRoot)
        {
            if (shopRoot == null) return;

            Canvas.ForceUpdateCanvases();

            var contentRt = shopRoot.GetComponent<RectTransform>();
            if (contentRt != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRt);

            var scrollRect = shopRoot.GetComponentInParent<ScrollRect>();
            if (scrollRect != null)
                scrollRect.verticalNormalizedPosition = 1f;

            Canvas.ForceUpdateCanvases();
        }

        // -----------------------------------------------------------------------
        // Clones an existing shop item GameObject, assigns a new ShopItemSO with
        // the custom module's name/price/ID, and adds it to the given parent.
        // -----------------------------------------------------------------------
        private static GameObject AddShopPackage(ComputerShop computerShop, ShopItem source,
                                               GameObject parent, int prefabID,
                                               string label, int price, int xpToUnlock, string guid,
                                               Sprite icon = null)
        {
            string objectName = $"ShopItem_{label.Replace(" ", "_").Replace("/", "_")}";
            if (parent.transform.Find(objectName) != null)
                return null;

            bool alreadyRegistered = ShopItemAlreadyRegistered(computerShop, prefabID, guid);
            Sprite useSprite = icon ?? BaseQsfpSprite;

            var newSO = ScriptableObject.CreateInstance<ShopItemSO>();
            newSO.itemName   = label;
            newSO.price      = price;
            newSO.xpToUnlock = xpToUnlock;
            newSO.itemType   = PlayerManager.ObjectInHand.SFPBox; // always a box
            newSO.itemID     = prefabID;
            newSO.eol        = source.shopItemSO.eol;
            newSO.isCustomColor = source.shopItemSO.isCustomColor;
            newSO.sprite     = useSprite;

            var cloned = Object.Instantiate(source.gameObject, parent.transform, false);
            cloned.name = objectName;
            cloned.transform.localPosition = Vector3.zero;
            cloned.transform.localScale    = Vector3.one;

            var shopItem = cloned.GetComponent<ShopItem>();
            if (shopItem == null)
            {
                MelonLogger.Error($"ShopItem component missing for '{label}'.");
                Object.Destroy(cloned);
                return null;
            }

            shopItem.shopItemSO = newSO;
            shopItem.guid       = guid;
            shopItem.itemDisplayName = label;
            shopItem.isUnlocked = true;

            if (shopItem.txtName != null)
                shopItem.txtName.text = label;
            if (shopItem.txtPrice != null)
                shopItem.txtPrice.text = $"{price} $";
            if (shopItem.txtXpToUnlock != null)
                shopItem.txtXpToUnlock.text = "";
            if (shopItem.unlockButton != null)
                shopItem.unlockButton.SetActive(false);
            if (shopItem.itemIcon != null && useSprite != null)
                shopItem.itemIcon.sprite = useSprite;

            if (!alreadyRegistered)
                RegisterShopItem(computerShop, shopItem);
            cloned.SetActive(true);

            MelonLogger.Msg($"Shop-Paket hinzugefügt: '{newSO.itemName}' " +
                            $"(itemID={prefabID}, price={newSO.price}, parent={parent.name})");
            return cloned;
        }

        private static bool ShopItemAlreadyRegistered(ComputerShop computerShop, int prefabID, string guid)
        {
            var items = computerShop?.shopItems;
            if (items == null) return false;

            foreach (var item in items)
            {
                if (item == null) continue;
                if (item.guid == guid) return true;
                if (item.shopItemSO != null && item.shopItemSO.itemID == prefabID)
                    return true;
            }

            return false;
        }

        private static void RegisterShopItem(ComputerShop computerShop, ShopItem shopItem)
        {
            var oldItems = computerShop?.shopItems;
            if (oldItems == null || shopItem == null) return;

            var newItems = new Il2CppReferenceArray<ShopItem>(oldItems.Length + 1);
            for (int i = 0; i < oldItems.Length; i++)
                newItems[i] = oldItems[i];
            newItems[oldItems.Length] = shopItem;
            computerShop.shopItems = newItems;
        }

        // -----------------------------------------------------------------------
        // Builds a box prefab for the 32x bulk shop item. Marked "_bulk_" so the
        // post-delivery scanner expands it to 32 slots.
        // -----------------------------------------------------------------------
        internal static GameObject BuildBulkBoxPrefab(MainGameManager mgm, int bulkItemID,
                                                      ModuleRegistry.Entry entry,
                                                      Transform parent = null)
        {
            var box = BuildBoxPrefab(mgm, entry.Definition.PrefabId, entry, parent);
            if (box == null) return null;

            box.name = $"SFPBox_bulk_{entry.Definition.PrefabId}";
            return box;
        }

        // -----------------------------------------------------------------------
        // Tray item IDs: TRAY_ID_BASE + moduleIndex * TraySizeCount + sizeIndex.
        // moduleIndex is the index into ModuleCatalog.All (explicit PrefabIds).
        // -----------------------------------------------------------------------
        internal static bool IsCustomItemID(int itemID)
        {
            if (ModuleRegistry.TryGet(itemID, out var entry) && entry.Definition.IsShopItem) return true;
            if (ModuleRegistry.TryGetByBulkItem(itemID, out _, out _)) return true;
            if (IsCustomTrayItemID(itemID)) return true;
            return false;
        }

        internal static bool IsCustomTrayItemID(int itemID)
        {
            return IsTrayItemID(itemID, out _, out _);
        }

        internal static bool IsTrayItemID(int itemID, out int moduleIndex, out int sizeIndex)
        {
            moduleIndex = -1;
            sizeIndex   = -1;
            int offset = itemID - TRAY_ID_BASE;
            if (offset < 0) return false;
            moduleIndex = offset / TraySizeCount;
            sizeIndex   = offset % TraySizeCount;
            return moduleIndex < ModuleCatalog.All.Length;
        }

        internal static int RegularIdForTray(int trayItemID)
        {
            return IsTrayItemID(trayItemID, out int moduleIndex, out _)
                ? ModuleCatalog.All[moduleIndex].PrefabId
                : -1;
        }

        internal static int TraySizeFromItemID(int trayItemID)
        {
            return IsTrayItemID(trayItemID, out _, out int sizeIndex)
                ? TraySizes[sizeIndex]
                : -1;
        }

        internal static GameObject BuildTrayBoxPrefab(MainGameManager mgm, int trayItemID,
                                                      ModuleRegistry.Entry entry,
                                                      Transform parent = null)
        {
            if (!IsTrayItemID(trayItemID, out int moduleIndex, out int sizeIndex)) return null;

            int regularPrefabID = ModuleCatalog.All[moduleIndex].PrefabId;
            int capacity        = TraySizes[sizeIndex];

            var box = BuildBoxPrefab(mgm, regularPrefabID, entry, parent);
            if (box == null) return null;

            box.name = $"SFPBox_tray_{regularPrefabID}_{capacity}";
            return box;
        }

        // -----------------------------------------------------------------------
        // Coroutine that scans the world for size-coded module boxes (tray/bulk)
        // that haven't been expanded yet. Polls for a time window because the
        // delivery box arrives seconds after Buy / at checkout.
        //   "_bulk_"                          → 32
        //   "SFPBox_tray_<regularID>_<Capacity>"  → that capacity (16/32/64/128)
        // -----------------------------------------------------------------------
        private static bool _boxScannerRunning;

        internal static IEnumerator ExpandAllSizedBoxes()
        {
            if (_boxScannerRunning) yield break;
            _boxScannerRunning = true;

            float deadline = Time.time + 90f;
            int emptyPasses = 0;

            while (Time.time < deadline)
            {
                bool foundAny = false;
                var allBoxes = Object.FindObjectsOfType<SFPBox>();

                foreach (var box in allBoxes)
                {
                    if (box == null) continue;
                    if (!box.gameObject.activeInHierarchy) continue;

                    int capacity = GetTargetCapacity(box.gameObject.name);
                    if (capacity < 0) continue;
                    if (box.sfpPositions != null && box.sfpPositions.Length >= capacity) continue;

                    UpgradeToBulkBox(box, capacity);
                    foundAny = true;
                }

                if (foundAny) emptyPasses = 0;
                else emptyPasses++;

                // Stop after ~8 empty passes (about 12 s with no new box).
                if (emptyPasses >= 8) break;

                yield return new WaitForSeconds(1.5f);
            }

            _boxScannerRunning = false;
        }

        internal static int GetTargetCapacity(string boxName)
        {
            if (string.IsNullOrEmpty(boxName)) return -1;

            string name = boxName.Trim();
            const string cloneSuffix = "(Clone)";
            if (name.EndsWith(cloneSuffix, System.StringComparison.Ordinal))
                name = name.Substring(0, name.Length - cloneSuffix.Length);

            if (name.IndexOf("_bulk_", System.StringComparison.Ordinal) >= 0) return 32;

            int idx = name.IndexOf("_tray_", System.StringComparison.Ordinal);
            if (idx < 0) return -1;
            string tail = name.Substring(idx + "_tray_".Length);
            int under = tail.LastIndexOf('_');
            if (under < 0) return -1;
            return int.TryParse(tail.Substring(under + 1), out int cap) && cap > 0 ? cap : -1;
        }

        // -----------------------------------------------------------------------
        // Expands a live SFPBox from its vanilla capacity (5) to newCapacity
        // by cloning slot positions and using proper Il2Cpp array types.
        // -----------------------------------------------------------------------
        internal static void UpgradeToBulkBox(SFPBox box, int newCapacity)
        {
            var oldPositions = box.sfpPositions;
            if (oldPositions == null || oldPositions.Length == 0) return;

            int oldCap = oldPositions.Length;
            if (oldCap >= newCapacity) return;

            var newPositions = new Il2CppReferenceArray<Transform>(newCapacity);
            var newUsed      = new Il2CppStructArray<int>(newCapacity);

            int fullSlotValue = box.usedPositions != null && box.usedPositions.Length > 0
                ? box.usedPositions[oldCap - 1] : 1;

            for (int i = 0; i < oldCap; i++)
            {
                newPositions[i] = oldPositions[i];
                newUsed[i] = box.usedPositions != null && i < box.usedPositions.Length
                    ? box.usedPositions[i] : 0;
            }

            for (int i = oldCap; i < newCapacity; i++)
            {
                int baseIdx = i % oldCap;
                Transform baseSlot = oldPositions[baseIdx];

                var newSlotObj = Object.Instantiate(baseSlot.gameObject, baseSlot.parent);
                newSlotObj.name = $"SFPPositionInBox_{i}";
                newSlotObj.transform.localPosition = baseSlot.localPosition;

                newPositions[i] = newSlotObj.transform;
                newUsed[i] = fullSlotValue;
            }

            box.sfpPositions  = newPositions;
            box.usedPositions = newUsed;

            MelonLogger.Msg($"Upgraded box '{box.gameObject.name}' from {oldCap} → {newCapacity} slots.");
        }
    }
}
