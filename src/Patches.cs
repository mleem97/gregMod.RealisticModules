using System.Collections;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace GregModMoreModules
{
    // =========================================================================
    // Patch: MainGameManager.Awake (Postfix)
    // Earliest point where sfpPrefabs is populated — before OnLoad() restores
    // save data. Populates the registry and extends sfpPrefabs.
    // =========================================================================
    [HarmonyPatch(typeof(MainGameManager), nameof(MainGameManager.Awake))]
    internal static class PatchMainGameManagerAwake
    {
        private static void Postfix(MainGameManager __instance)
        {
            MelonLogger.Msg("MainGameManager.Awake → setting up registry.");
            Core.SetupRegistry(__instance);
        }
    }

    // =========================================================================
    // Patch: MainGameManager.Start (Postfix)
    // Safety net — re-runs SetupRegistry if Start() reset sfpPrefabs back to
    // its vanilla size (which would orphan our custom indices).
    // =========================================================================
    [HarmonyPatch(typeof(MainGameManager), nameof(MainGameManager.Start))]
    internal static class PatchMainGameManagerStart
    {
        private static void Postfix(MainGameManager __instance)
        {
            var arr = __instance.sfpPrefabs;
            int len = arr?.Length ?? 0;

            if (len > 0 && !Core.IsSetupCompleteFor(__instance))
            {
                MelonLogger.Warning("sfpPrefabs was RESET — re-extending in Start.");
                Core.SetupRegistry(__instance);
            }
        }
    }

    // =========================================================================
    // Patch: ComputerShop.ButtonBuyShopItem (Prefix)
    // Adds custom shop items to the cart. The game's regular ButtonBuyShopItem
    // path silently rejects custom item IDs before it reaches GetPrefabForItem,
    // so these IDs use the lower-level spawn + ShopCartItem flow directly.
    // =========================================================================
    [HarmonyPatch(typeof(ComputerShop), nameof(ComputerShop.ButtonBuyShopItem))]
    internal static class PatchButtonBuyShopItem
    {
        private static bool Prefix(ComputerShop __instance, int itemID, int price,
                                   PlayerManager.ObjectInHand itemType, string displayName,
                                   bool isCustomColor)
        {
            if (ModuleRegistry.IsKnownShopItem(itemID))
            {
                int before = __instance.cartUIItems != null ? __instance.cartUIItems.Count : -1;
                MelonLogger.Msg($"Buy clicked: itemID={itemID}, type={(int)itemType}, " +
                                $"price={price}, customColor={isCustomColor}, name='{displayName}'");

                bool added = AddCustomItemToCart(__instance, itemID, price, itemType, displayName);

                int after = __instance.cartUIItems != null ? __instance.cartUIItems.Count : -1;
                MelonLogger.Msg($"Custom cart add: success={added}, before={before}, after={after}, " +
                                $"currentPrice={__instance.currentPrice}");
                return false;
            }

            return true;
        }

        private static bool AddCustomItemToCart(ComputerShop shop, int itemID, int price,
                                                PlayerManager.ObjectInHand itemType,
                                                string displayName)
        {
            var prefab = BuildCartPrefab(itemID, itemType);
            if (prefab == null)
            {
                MelonLogger.Error($"No prefab for custom cart itemID={itemID}, type={(int)itemType}.");
                return false;
            }

            if (shop.shopCartItemPrefab == null || shop.parentForShopCartItems == null ||
                shop.cartUIItems == null)
            {
                MelonLogger.Error("Shop cart UI references missing.");
                return false;
            }

            var spawnedUID = shop.SpawnPhysicalItem(prefab, price, itemType);
            if (!spawnedUID.HasValue)
            {
                MelonLogger.Error($"SpawnPhysicalItem failed for custom itemID={itemID}.");
                return false;
            }

            int uid = spawnedUID.Value;
            var existingCartItem = FindExistingCartItem(shop, itemID, itemType);
            if (existingCartItem != null)
            {
                existingCartItem.AddOne();
                shop.UpdateCartTotal();
                MelonLogger.Msg($"Custom cart quantity increased: itemID={itemID}, uid={uid}, " +
                                $"quantity={existingCartItem.Quantity}");
                return true;
            }

            var cartObject = Object.Instantiate(shop.shopCartItemPrefab,
                                                shop.parentForShopCartItems, false);
            var cartItem = cartObject.GetComponent<ShopCartItem>();
            if (cartItem == null)
            {
                Object.Destroy(cartObject);
                shop.RemoveSpawnedItem(uid);
                MelonLogger.Error("ShopCartItem component missing on cart prefab clone.");
                return false;
            }

            var noCustomColor = new Il2CppSystem.Nullable<Color>();
            cartItem.Initialize(shop, displayName, itemID, price, itemType, noCustomColor);
            shop.cartUIItems.Add(cartItem);
            shop.UpdateCartTotal();

            MelonLogger.Msg($"Custom cart item created: itemID={itemID}, uid={uid}, " +
                            $"quantity={cartItem.Quantity}");
            return true;
        }

        private static ShopCartItem FindExistingCartItem(ComputerShop shop, int itemID,
                                                         PlayerManager.ObjectInHand itemType)
        {
            if (shop.cartUIItems == null) return null;

            foreach (var cartItem in shop.cartUIItems)
            {
                if (cartItem == null) continue;
                if (cartItem.ItemID == itemID && cartItem.ItemType == itemType)
                    return cartItem;
            }

            return null;
        }

        private static GameObject BuildCartPrefab(int itemID, PlayerManager.ObjectInHand itemType)
        {
            var mgm = MainGameManager.instance;
            if (mgm == null) return null;

            Transform templateParent = Core.TemplateHolder != null
                ? Core.TemplateHolder.transform
                : null;

            if (ModuleRegistry.TryGetByBulkItem(itemID, out var bulkEntry, out _))
            {
                if ((int)itemType != 9) return null;

                MelonCoroutines.Start(Core.BulkUpgradeScanner());
                return Core.BuildBulkBoxPrefab(mgm, itemID, bulkEntry, templateParent);
            }

            if (!ModuleRegistry.TryGet(itemID, out var entry)) return null;

            if ((int)itemType == 9)
                return Core.BuildBoxPrefab(mgm, itemID, entry, templateParent);
            if ((int)itemType == 8)
                return Core.BuildModulePrefab(mgm, itemID, entry, templateParent);

            return null;
        }
    }

    // =========================================================================
    // Patch: ComputerShop.GetPrefabForItem (Prefix)
    // Routes our custom itemID to the correct prefab when the player buys from
    // the shop. Handles both SFPBox (type 9) and bare SFPModule (type 8).
    // =========================================================================
    [HarmonyPatch(typeof(ComputerShop), nameof(ComputerShop.GetPrefabForItem))]
    internal static class PatchGetPrefabForItem
    {
        private static bool Prefix(int itemID, PlayerManager.ObjectInHand itemType, ref GameObject __result)
        {
            var mgm = MainGameManager.instance;
            if (mgm == null) return true;

            // 32x bulk item: BULK_ID_BASE + i → return a box marked with "_bulk_"
            // in its name. The actual 32-slot expansion is done post-delivery by
            // BulkUpgradeScanner (game re-initializes slots after instantiation).
            if (ModuleRegistry.TryGetByBulkItem(itemID, out var bulkEntry, out _))
            {
                if ((int)itemType == 9)
                {
                    MelonLogger.Msg($"GetPrefabForItem custom bulk: itemID={itemID}, prefabID={bulkEntry.Definition.PrefabId}");
                    __result = Core.BuildBulkBoxPrefab(mgm, itemID, bulkEntry);
                    MelonCoroutines.Start(Core.BulkUpgradeScanner());
                    return false;
                }
                return true;
            }

            if (!ModuleRegistry.TryGet(itemID, out var entry)) return true;

            // ObjectInHand.SFPBox == 9, ObjectInHand.SFPModule == 8
            if ((int)itemType == 9)
            {
                MelonLogger.Msg($"GetPrefabForItem custom box: itemID={itemID}");
                __result = Core.BuildBoxPrefab(mgm, itemID, entry);
                return false;
            }
            if ((int)itemType == 8)
            {
                MelonLogger.Msg($"GetPrefabForItem custom module: itemID={itemID}");
                __result = Core.BuildModulePrefab(mgm, itemID, entry);
                return false;
            }

            return true;
        }
    }

    // =========================================================================
    // Patch: SFPBox.LoadSFPsFromSave (Prefix)
    // The load code accesses sfpPrefabs[prefabID] directly — it does NOT call
    // GetSfpPrefab(). Il2Cpp's GC can null our cached template between Awake
    // and the actual load. This prefix rebuilds fresh templates at all custom
    // indices immediately before the load code reads the array.
    // =========================================================================
    [HarmonyPatch(typeof(SFPBox), nameof(SFPBox.LoadSFPsFromSave))]
    internal static class PatchLoadSFPsFromSave
    {
        private static void Prefix()
        {
            var mgm = MainGameManager.instance;
            if (mgm == null) return;

            var arr = mgm.sfpPrefabs;
            if (arr == null) return;

            foreach (var (prefabID, entry) in ModuleRegistry.Entries)
            {
                if (prefabID < 0 || prefabID >= arr.Length) continue;

                if (arr[prefabID] == null)
                {
                    var template = Core.BuildModulePrefab(mgm, prefabID, entry,
                                                          Core.TemplateHolder?.transform);
                    if (template != null)
                        template.name = $"SFPModule_template_{prefabID}";
                    arr[prefabID] = template;
                }
            }
        }
    }

    // =========================================================================
    // Patch: CableLink.InsertSFP (Prefix)
    // Child modules taken from a custom box retain the vanilla QSFP+ prefabID
    // (3) because setting prefabID on active child GameObjects causes the world
    // tracker to spawn infinite loose modules. Instead we fix it here — at the
    // exact moment the module is inserted into a port — so the save stores the
    // correct custom prefabID and load can restore the right module.
    // =========================================================================
    [HarmonyPatch(typeof(CableLink), nameof(CableLink.InsertSFP))]
    internal static class PatchCableLinkInsertSFP
    {
        private static void Prefix(float speed, SFPModule module)
        {
            var usableObj = module?.GetComponent<UsableObject>();
            if (usableObj == null) return;

            if (ModuleRegistry.TryResolveIdentity(module, out int exactPrefabId))
            {
                usableObj.prefabID = exactPrefabId;
                return;
            }

            // Speed is intentionally only a migration fallback for old saves.
            // New modules with equal speeds never use this path.
            if (ModuleRegistry.TryResolveLegacyBySpeed(speed, out int legacyPrefabId))
            {
                usableObj.prefabID = legacyPrefabId;
                MelonLogger.Warning($"Migrated legacy SFP identity by speed to prefabID={legacyPrefabId}.");
            }
        }
    }

    // =========================================================================
    // Patch: SFPBox.CanAcceptSFP (Prefix)
    // Our custom box uses sfpBoxType == prefabID, but our modules
    // carry sfpType == vanilla QSFP+ type for port compatibility. Without this
    // patch the box would reject our module because the types don't match.
    // =========================================================================
    [HarmonyPatch(typeof(SFPBox), nameof(SFPBox.CanAcceptSFP))]
    internal static class PatchCanAcceptSFP
    {
        private static bool Prefix(SFPBox __instance, int sfpType, ref bool __result)
        {
            int boxType = __instance.sfpBoxType;
            if (!ModuleRegistry.TryGet(boxType, out var entry)) return true;

            __result = CompatibilityMatrix.CanAccept(entry, sfpType, Core.CompatibilityMode);
            return false;
        }
    }
}
