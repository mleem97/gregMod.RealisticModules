using System.Collections.Generic;
using Il2Cpp;
using UnityEngine;

namespace GregModMoreModules
{
    internal static class ModuleRegistry
    {
        internal readonly struct Entry
        {
            internal readonly ModuleDefinition Definition;
            internal readonly float SpeedInternal;
            internal readonly int ModuleSfpType;
            internal readonly int BoxSfpType;
            internal readonly int BasePrefabID;
            internal readonly int ModuleCount;

            internal Entry(ModuleDefinition definition, int moduleSfpType, int basePrefabID, int moduleCount = 5)
            {
                Definition = definition;
                SpeedInternal = definition.InternalSpeed;
                ModuleSfpType = moduleSfpType;
                BoxSfpType = definition.PrefabId;
                BasePrefabID = basePrefabID;
                ModuleCount = moduleCount;
            }
        }

        private static readonly Dictionary<int, Entry> EntriesByPrefab = new();
        private static readonly Dictionary<int, int> PrefabByBulkItem = new();
        private static readonly Dictionary<int, int> LivePrefabByInstance = new();

        internal static IReadOnlyDictionary<int, Entry> Entries => EntriesByPrefab;

        internal static void Clear()
        {
            EntriesByPrefab.Clear();
            PrefabByBulkItem.Clear();
            LivePrefabByInstance.Clear();
        }

        internal static void Register(int prefabId, Entry entry)
        {
            EntriesByPrefab[prefabId] = entry;
            if (entry.Definition.BulkItemId > 0)
                PrefabByBulkItem[entry.Definition.BulkItemId] = prefabId;
        }

        internal static bool TryGet(int prefabId, out Entry entry) => EntriesByPrefab.TryGetValue(prefabId, out entry);

        internal static bool TryGetByBulkItem(int itemId, out Entry entry, out int prefabId)
        {
            if (PrefabByBulkItem.TryGetValue(itemId, out prefabId) && TryGet(prefabId, out entry)) return true;
            entry = default;
            prefabId = -1;
            return false;
        }

        internal static bool IsKnownShopItem(int itemId) =>
            EntriesByPrefab.ContainsKey(itemId) || PrefabByBulkItem.ContainsKey(itemId);

        internal static int MaxKnownId
        {
            get
            {
                int max = 0;
                foreach (var id in EntriesByPrefab.Keys) if (id > max) max = id;
                return max;
            }
        }

        internal static void RememberLiveModule(GameObject module, int prefabId)
        {
            if (module != null) LivePrefabByInstance[module.GetInstanceID()] = prefabId;
        }

        internal static bool TryResolveIdentity(SFPModule module, out int prefabId)
        {
            prefabId = -1;
            if (module == null) return false;

            var usable = module.GetComponent<UsableObject>();
            if (usable != null && EntriesByPrefab.ContainsKey(usable.prefabID))
            {
                prefabId = usable.prefabID;
                return true;
            }

            if (LivePrefabByInstance.TryGetValue(module.gameObject.GetInstanceID(), out prefabId)) return true;

            string name = module.gameObject.name ?? string.Empty;
            const string marker = "realisticModule_";
            int markerIndex = name.IndexOf(marker);
            if (markerIndex >= 0)
            {
                string digits = name.Substring(markerIndex + marker.Length);
                int separator = digits.IndexOf('_');
                if (separator >= 0) digits = digits.Substring(0, separator);
                if (int.TryParse(digits, out prefabId) && EntriesByPrefab.ContainsKey(prefabId)) return true;
            }

            return false;
        }

        internal static bool TryResolveLegacyBySpeed(float speedInternal, out int prefabId)
        {
            prefabId = -1;
            foreach (var pair in EntriesByPrefab)
            {
                if (pair.Value.Definition.Lifecycle != ModuleLifecycle.Legacy) continue;
                if (Mathf.Approximately(speedInternal, pair.Value.SpeedInternal))
                {
                    prefabId = pair.Key;
                    return true;
                }
            }
            return false;
        }
    }
}
