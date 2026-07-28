using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using Math = System.Math;
using System.Collections;
using System.Collections.Generic;

[assembly: MelonInfo(typeof(GregModMoreModules.Core), "gregMod.RealisticModules", "1.0.0", "TeamGreg Modding (leoms1408 / mleem97)")]
[assembly: MelonGame("Waseku", "Data Center")]

namespace GregModMoreModules
{
    public class Core : MelonMod
    {
        // Sprite from the vanilla QSFP+ shop entry — reused as icon for all custom modules.
        internal static Sprite BaseQsfpSprite;

        // sfpType of the vanilla QSFP+ module (form-factor; determines port compatibility).
        // Our custom modules keep this value so they fit the same switch ports.
        internal static int BaseQsfpSfpType = -1;

        // prefabID of the vanilla QSFP+ module — used as clone source in BuildModulePrefab/BuildBoxPrefab.
        internal static int BaseQsfpPrefabID = -1;

        // New save IDs are explicit in ModuleDefinition. These constants only document
        // the reserved ranges; no runtime identity is derived from array position.
        internal const int MOD_ID_BASE  = ModuleCatalog.FirstNewPrefabId;
        internal const int BULK_ID_BASE = ModuleCatalog.FirstNewBulkId;

        // Inactive holder for prefab templates — parenting templates here makes their
        // activeInHierarchy = false, so the game's UsableObject tracker ignores them.
        // Object.Instantiate still produces active clones from inactive-hierarchy objects.
        internal static GameObject TemplateHolder { get; private set; }
        internal static CompatibilityMode CompatibilityMode { get; private set; } = CompatibilityMode.SimplifiedCompatibility;
        private static readonly Dictionary<int, int> ExtendedShopRowsByParent = new Dictionary<int, int>();

        [System.Obsolete]
        public override void OnApplicationStart()
        {
            var category = MelonPreferences.CreateCategory("gregMod.RealisticModules");
            var strict = category.CreateEntry("StrictCompatibility", false,
                "Strict compatibility", "Require the observed host sfpType to match the module.");
            CompatibilityMode = strict.Value ? CompatibilityMode.StrictCompatibility : CompatibilityMode.SimplifiedCompatibility;
            MelonLogger.Msg($"Compatibility mode: {CompatibilityMode}");
        }

        // -----------------------------------------------------------------------
        // Scans vanilla sfpPrefabs to find the highest-speed module (QSFP+ 40G),
        // stores it as the clone source, then extends the sfpPrefabs array with
        // one slot per custom module starting at MOD_ID_BASE.
        //
        // Starting well above vanillaCount prevents prefabID collisions if
        // the game later adds new vanilla SFP types at indices 4, 5, 6 …
        //
        // Called from PatchMainGameManagerAwake — the earliest point where
        // sfpPrefabs is populated, guaranteed to run before OnLoad() restores saves.
        // -----------------------------------------------------------------------
        internal static void SetupRegistry(MainGameManager mgm)
        {
            ModuleRegistry.Clear();

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

            MelonLogger.Msg($"Vanilla SFP prefabs: {sfpPrefabs.Length}");

            float highestSpeed = -1f;

            for (int i = 0; i < sfpPrefabs.Length; i++)
            {
                var go        = sfpPrefabs[i];
                if (go == null) continue;
                var sfpMod    = go.GetComponent<SFPModule>();
                var usableObj = go.GetComponent<UsableObject>();
                float speed   = sfpMod    != null ? sfpMod.speed       : -1f;
                int   sfpType = sfpMod    != null ? sfpMod.sfpType     : -1;
                int   pid     = usableObj != null ? usableObj.prefabID : -1;

                if (speed > highestSpeed)
                {
                    highestSpeed     = speed;
                    BaseQsfpSfpType  = sfpType;
                    BaseQsfpPrefabID = pid;
                }
            }

            if (BaseQsfpPrefabID < 0)
            {
                MelonLogger.Error("Could not identify base QSFP+ prefab.");
                return;
            }

            MelonLogger.Msg($"Base QSFP+: prefabID={BaseQsfpPrefabID}, " +
                            $"sfpType={BaseQsfpSfpType}, {highestSpeed * 5f} Gbps");

            // Create/recreate the inactive holder that hides templates from the world system.
            if (TemplateHolder != null)
                Object.Destroy(TemplateHolder);
            TemplateHolder = new GameObject("gregModRealisticModules_TemplateHolder");
            TemplateHolder.SetActive(false);
            Object.DontDestroyOnLoad(TemplateHolder);

            int vanillaCount = sfpPrefabs.Length;

            if (vanillaCount > MOD_ID_BASE)
            {
                MelonLogger.Error($"vanilla sfpPrefabs.Length={vanillaCount} exceeds " +
                                  $"MOD_ID_BASE={MOD_ID_BASE}! prefabID collision risk — mod disabled.");
                return;
            }

            foreach (var def in ModuleCatalog.Legacy)
                ModuleRegistry.Register(def.PrefabId, new ModuleRegistry.Entry(def, BaseQsfpSfpType, BaseQsfpPrefabID));
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
                };
                ModuleRegistry.Register(alias.PrefabId, new ModuleRegistry.Entry(alias, BaseQsfpSfpType, BaseQsfpPrefabID));
            }
            foreach (var def in ModuleList.All)
                ModuleRegistry.Register(def.PrefabId, new ModuleRegistry.Entry(def, BaseQsfpSfpType, BaseQsfpPrefabID));

            int maxPrefabId = Math.Max(ModuleRegistry.MaxKnownId, vanillaCount - 1);
            var extended = new GameObject[maxPrefabId + 1];
            for (int i = 0; i < vanillaCount; i++) extended[i] = sfpPrefabs[i];

            foreach (var pair in ModuleRegistry.Entries)
            {
                int id = pair.Key;
                var entry = pair.Value;
                var template = BuildModulePrefab(mgm, id, entry, TemplateHolder.transform);
                if (template != null) template.name = $"SFPModule_template_{id}";
                extended[id] = template;
                MelonLogger.Msg($"Registered '{entry.Definition.DisplayName}': prefabID={id}, {entry.Definition.SpeedGbps} Gbps");
            }

            mgm.sfpPrefabs = extended;
            MelonLogger.Msg($"sfpPrefabs extended: {vanillaCount} → {extended.Length}");
        }

        // -----------------------------------------------------------------------
        // Clones the vanilla QSFP+ module prefab and applies our custom speed and
        // prefabID. Called on-demand from patches rather than caching the result,
        // because Il2Cpp's GC can silently invalidate native pointers on cached
        // GameObjects stored in C# data structures.
        //
        // parent: when non-null the clone is instantiated directly under that transform,
        // so it is never active in hierarchy and the world tracker cannot pick it up.
        // Pass TemplateHolder.transform for cached templates, null for live clones.
        // -----------------------------------------------------------------------
        internal static GameObject BuildModulePrefab(MainGameManager mgm, int prefabID,
                                                     ModuleRegistry.Entry entry,
                                                     Transform parent = null)
        {
            var basePrefab = mgm.sfpPrefabs[entry.BasePrefabID];
            if (basePrefab == null)
            {
                MelonLogger.Error($"Base prefab [{entry.BasePrefabID}] is null.");
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
        // Walks every Renderer in the module hierarchy, clones any material whose
        // name contains "Blue", and recolors it to the tint defined per prefabID.
        // Uses GetComponentsInChildren because the MeshRenderer of the QSFP+ model
        // lives on a child GameObject, not on the root.
        // -----------------------------------------------------------------------
        internal static void ApplyModuleTint(GameObject root, int prefabID)
        {
            if (root == null) return;

            if (!ModuleRegistry.TryGet(prefabID, out var entry)) return;
            Color tint = entry.Definition.ModuleColor;

            // Common color property names across shaders we might encounter.
            string[] colorProps = { "_Color", "_BaseColor", "_MainColor", "_TintColor", "_Tint", "_AlbedoColor" };

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var rend in renderers)
            {
                if (rend == null) continue;
                var mats = rend.materials; // returns instanced copies — safe to mutate
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
        // Clones the vanilla QSFP+ box prefab and applies our custom sfpBoxType
        // and prefabID. Also updates all child SFPModule components inside the box
        // so the player receives the correct module when unboxing.
        // -----------------------------------------------------------------------
        internal static GameObject BuildBoxPrefab(MainGameManager mgm, int prefabID,
                                                  ModuleRegistry.Entry entry,
                                                  Transform parent = null)
        {
            var boxPrefabs = mgm.sfpsBoxedPrefab;
            if (boxPrefabs == null) return null;

            GameObject baseBox = entry.BasePrefabID < boxPrefabs.Length
                ? boxPrefabs[entry.BasePrefabID]
                : null;

            // Fall back to the first non-null box if the expected index is missing.
            if (baseBox == null)
                for (int i = 0; i < boxPrefabs.Length; i++)
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

            // The box prefab contains the SFPModules as child GameObjects.
            // Only update speed — do NOT set prefabID on children, as that would
            // register them as independent world items and cause them to spawn loose.
            // PatchCableLinkInsertSFP corrects the prefabID at insertion time instead.
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
            if (buildIndex != 0)
                MelonCoroutines.Start(AddShopItems());
        }

        // -----------------------------------------------------------------------
        // Waits for the shop to finish initializing, then injects a shop button
        // for each registered custom module into the "HL Mods" section.
        // The 1.5 s delay is necessary because the shop UI is built after scene load.
        // -----------------------------------------------------------------------
        private IEnumerator AddShopItems()
        {
            yield return new WaitForSeconds(1.5f);

            var mgm = MainGameManager.instance;
            if (mgm == null) { LoggerInstance.Warning("MGM null — shop skipped."); yield break; }

            var computerShop = mgm.computerShop;
            if (computerShop == null) { LoggerInstance.Warning("Shop null — skipped."); yield break; }

            // Find the vanilla QSFP+ box shop entry to use as a UI clone template.
            // The shop sells SFPBox items (type 9), not bare SFPModule items.
            ShopItem sourceItem = null;
            if (computerShop.shopItems != null)
            {
                foreach (var si in computerShop.shopItems)
                {
                    if (si == null || si.shopItemSO == null) continue;

                    if ((int)si.shopItemSO.itemType == 9 && si.shopItemSO.itemID == BaseQsfpPrefabID)
                    {
                        sourceItem     = si;
                        BaseQsfpSprite = si.shopItemSO.sprite;
                    }
                }
            }

            if (sourceItem == null)
            {
                LoggerInstance.Warning("No QSFP+ box shop item found — shop buttons skipped.");
                yield break;
            }

            var shopRoot = computerShop.shopItemParent;
            if (shopRoot == null) { LoggerInstance.Warning("shopItemParent null."); yield break; }

            // Target the "HL Mods" section inside VL-ShopItems so our items appear
            // in the correct category rather than being appended at the end.
            var sfpParent = sourceItem.transform.parent != null
                ? sourceItem.transform.parent.gameObject
                : shopRoot;

            var customRows = EnsureCustomSfpRows(shopRoot, sfpParent, ModuleList.All.Length * 2);
            if (customRows.Count < 0)
                LoggerInstance.Warning("'HL Mods' not found — falling back to shopItemParent.");

            float itemHeight = 0f;
            var sourceRt = sourceItem.GetComponent<UnityEngine.RectTransform>();
            if (sourceRt != null)
                itemHeight = sourceRt.rect.height;

            int addedSfpCount  = 0;
            int basePrice      = sourceItem.shopItemSO.price;

            for (int i = 0; i < ModuleList.All.Length; i++)
            {
                var def      = ModuleList.All[i];
                int prefabID = def.PrefabId;
                if (!ModuleRegistry.TryGet(prefabID, out _)) continue;

                // 5x shop button (standard)
                string label5 = BuildShopLabel("5x", def);
                int price5    = (int)(basePrice * def.PriceMultiplier);
                var rowParent = customRows.Count > 0
                    ? customRows[Mathf.Min(i / 4, customRows.Count - 1)]
                    : sfpParent;
                var added5    = AddShopButton(computerShop, sourceItem, rowParent, prefabID,
                                              label5, price5, def.XpToUnlock, def.ShopGuid);
                if (added5 != null) addedSfpCount++;

                int bulkPrice = (int)(basePrice * def.PriceMultiplier * 5.5f);
                string bulkGuid = def.ShopGuid + "_bulk32";
                var added32 = AddShopButton(computerShop, sourceItem, rowParent, def.BulkItemId,
                    BuildShopLabel("32x", def), bulkPrice, def.XpToUnlock, bulkGuid);
                if (added32 != null) addedSfpCount++;
            }

            EnsureBackplaneTopSpacer(computerShop, shopRoot, sfpParent, itemHeight);

            // The HL Mods container has a fixed height — extend it so the ScrollRect
            // can scroll far enough to reveal our newly added items.
            ExtendVerticalContainer(shopRoot, itemHeight, customRows.Count);

            RebuildShopLayout(shopRoot);
        }

        private static System.Collections.Generic.List<GameObject> EnsureCustomSfpRows(GameObject shopRoot,
                                                                                       GameObject templateRow,
                                                                                       int itemCount)
        {
            var rows = new System.Collections.Generic.List<GameObject>();
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

        private static string BuildShopLabel(string quantity, ModuleDefinition def)
        {
            string speed = $"{def.SpeedGbps:0}Gbps";
            string moduleName = def.DisplayName;
            if (moduleName.EndsWith(speed))
                moduleName = moduleName.Substring(0, moduleName.Length - speed.Length).TrimEnd();

            return $"{quantity} {moduleName} {def.EthernetStandard} · {speed} · {def.Media} · {def.MaxReachMeters:0}m";
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
                    (name.Contains("125K") || name.Contains("500K")))
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
                    name.Contains("DataCenterAutomatorServers"))
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
        // Returns the created GameObject, or null if the ShopItem component is missing.
        // -----------------------------------------------------------------------
        private static GameObject AddShopButton(ComputerShop computerShop, ShopItem source,
                                               GameObject parent, int prefabID,
                                               string label, int price, int xpToUnlock, string guid)
        {
            string objectName = $"ShopItem_{label.Replace(" ", "_").Replace("/", "_")}";
            if (parent.transform.Find(objectName) != null)
                return null;

            bool alreadyRegistered = ShopItemAlreadyRegistered(computerShop, prefabID, guid);

            var newSO = ScriptableObject.CreateInstance<ShopItemSO>();
            newSO.itemName   = label;
            newSO.price      = price;
            newSO.xpToUnlock = xpToUnlock;
            newSO.itemType   = source.shopItemSO.itemType; // SFPBox (9)
            newSO.itemID     = prefabID;
            newSO.eol        = source.shopItemSO.eol;
            newSO.isCustomColor = source.shopItemSO.isCustomColor;
            newSO.sprite     = BaseQsfpSprite;

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
            if (shopItem.itemIcon != null && BaseQsfpSprite != null)
                shopItem.itemIcon.sprite = BaseQsfpSprite;

            if (!alreadyRegistered)
                RegisterShopItem(computerShop, shopItem);
            cloned.SetActive(true);

            MelonLogger.Msg($"Shop button added: '{newSO.itemName}' " +
                            $"(prefabID={prefabID}, price={newSO.price}, parent={parent.name})");
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
        // Builds a box prefab for the 32x shop item. Identical to a regular custom
        // box but with "_bulk_" in the name. The actual slot expansion to 32 happens
        // post-delivery via BulkUpgradeScanner — the game re-initializes sfpPositions
        // after instantiation, so upgrading at prefab time has no effect.
        // -----------------------------------------------------------------------
        internal static GameObject BuildBulkBoxPrefab(MainGameManager mgm, int bulkItemID,
                                                      ModuleRegistry.Entry entry,
                                                      Transform parent = null)
        {
            var box = BuildBoxPrefab(mgm, entry.Definition.PrefabId, entry, parent);
            if (box == null) return null;

            // Mark with distinctive name so the scanner can identify it.
            box.name = $"SFPBox_bulk_{entry.Definition.PrefabId}";
            return box;
        }

        // -----------------------------------------------------------------------
        // Coroutine that scans the world for boxes with "_bulk_" in their name
        // that haven't been expanded to 32 slots yet. Started when a bulk item is
        // purchased; runs until no more un-upgraded bulk boxes remain.
        // -----------------------------------------------------------------------
        private static bool _bulkScannerRunning;

        internal static IEnumerator BulkUpgradeScanner()
        {
            if (_bulkScannerRunning) yield break;
            _bulkScannerRunning = true;

            // Wait for the game to finish spawning and initializing the box.
            yield return new WaitForSeconds(2f);

            for (int scan = 0; scan < 40; scan++)
            {
                bool foundAny = false;
                var allBoxes = Object.FindObjectsOfType<SFPBox>();

                foreach (var box in allBoxes)
                {
                    if (box == null) continue;
                    if (!box.gameObject.activeInHierarchy) continue;
                    if (!box.gameObject.name.Contains("(Clone")) continue;
                    if (box.sfpPositions != null && box.sfpPositions.Length >= 32) continue;
                    if (!box.gameObject.name.Contains("_bulk_")) continue;

                    UpgradeToBulkBox(box, 32);
                    foundAny = true;
                }

                if (!foundAny) break;
                yield return new WaitForSeconds(1.5f);
            }

            _bulkScannerRunning = false;
        }

        // -----------------------------------------------------------------------
        // Expands a live SFPBox from its vanilla capacity (5) to newCapacity (32)
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

            // Copy existing slots.
            for (int i = 0; i < oldCap; i++)
            {
                newPositions[i] = oldPositions[i];
                newUsed[i] = box.usedPositions != null && i < box.usedPositions.Length
                    ? box.usedPositions[i] : 0;
            }

            // Clone new slots from the original positions (round-robin).
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
