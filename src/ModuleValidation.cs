using System.Collections.Generic;
using MelonLoader;

namespace GregModMoreModules
{
    internal static class ModuleValidation
    {
        internal static bool ValidateCatalog()
        {
            var prefabIds = new HashSet<int>();
            var bulkIds = new HashSet<int>();
            var stableIds = new HashSet<string>();
            bool valid = true;

            foreach (var definition in ModuleCatalog.All)
            {
                if (!definition.IsPlausible())
                {
                    MelonLogger.Error($"Invalid module definition: {definition.StableId}");
                    valid = false;
                    continue;
                }

                if (!prefabIds.Add(definition.PrefabId) || !bulkIds.Add(definition.BulkItemId) ||
                    !stableIds.Add(definition.StableId))
                {
                    MelonLogger.Error($"Duplicate module identity: {definition.StableId}");
                    valid = false;
                }

                if (definition.BulkItemId < ModuleCatalog.FirstNewBulkId ||
                    definition.PrefabId < ModuleCatalog.FirstNewPrefabId)
                {
                    MelonLogger.Error($"New module uses a reserved ID: {definition.StableId}");
                    valid = false;
                }
            }

            return valid;
        }
    }
}
