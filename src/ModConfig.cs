using System;
using MelonLoader;

namespace GregModMoreModules
{
    /// <summary>
    /// User-facing toggles. Category/entry names stay stable so existing
    /// MelonPreferences.cfg files keep working.
    /// </summary>
    internal static class ModConfig
    {
        internal static MelonPreferences_Entry<bool> PrefEnabled;
        internal static MelonPreferences_Entry<bool> PrefStrict;
        internal static MelonPreferences_Entry<bool> PrefExperimental;

        /// <summary>Master switch: catalog/shop inactive when false.</summary>
        internal static bool Enabled => PrefEnabled?.Value ?? true;

        internal static bool StrictCompatibility => PrefStrict?.Value ?? false;

        internal static bool OfferExperimentalModules => PrefExperimental?.Value ?? false;

        internal static void Load()
        {
            try
            {
                var category = MelonPreferences.CreateCategory("gregMod.RealisticModules",
                    "RealisticModules (transceiver catalog)");
                PrefEnabled = category.CreateEntry("Enabled", true,
                    "Enable RealisticModules",
                    "When off, no custom modules are registered and the shop stays vanilla. Takes effect on scene load / restart.");
                PrefStrict = category.CreateEntry("StrictCompatibility", false,
                    "Strict compatibility",
                    "Require the observed host sfpType to match the module.");
                PrefExperimental = category.CreateEntry("OfferExperimentalModules", false,
                    "Offer experimental modules",
                    "Offer Experimental-lifecycle modules in the shop (default off).");
                category.SaveToFile(false);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("gregMod.RealisticModules: could not load preferences, using defaults: " + ex.Message);
            }
        }

        internal static void SetEnabled(bool value)
        {
            try
            {
                if (PrefEnabled == null) Load();
                if (PrefEnabled == null) return;
                if (PrefEnabled.Value == value) return;
                PrefEnabled.Value = value;
                MelonPreferences.Save();
                MelonLogger.Msg($"gregMod.RealisticModules: Enabled={value}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("gregMod.RealisticModules: could not save Enabled: " + ex.Message);
            }
        }

        internal static void SetStrict(bool value)
        {
            try
            {
                if (PrefStrict == null) Load();
                if (PrefStrict == null) return;
                if (PrefStrict.Value == value) return;
                PrefStrict.Value = value;
                MelonPreferences.Save();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("gregMod.RealisticModules: could not save StrictCompatibility: " + ex.Message);
            }
        }
    }
}
