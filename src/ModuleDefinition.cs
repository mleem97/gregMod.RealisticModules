using System;
using UnityEngine;

namespace GregModMoreModules
{
    internal enum ModuleFormFactor
    {
        SfpPlus,
        Sfp28,
        Sfp56,
        QsfpPlus,
        Qsfp28,
        Qsfp56,
        QsfpDd,
        QsfpDd800,
        QsfpDd1600,
        Osfp,
    }

    internal enum ModuleMedia
    {
        PassiveDac,
        ActiveCopper,
        Aoc,
        MultimodeFiber,
        SinglemodeFiber,
        CoherentDwdm,
    }

    internal enum ConnectorType
    {
        Copper,
        Lc,
        Mpo,
    }

    internal enum ModuleLifecycle
    {
        Stable,
        Experimental,
        Legacy,
    }

    internal enum CompatibilityMode
    {
        SimplifiedCompatibility,
        StrictCompatibility,
    }

    internal sealed class BreakoutProfile
    {
        public string DisplayName;
        public int LaneCount;
        public float LaneSpeedGbps;
        public float TotalSpeedGbps => LaneCount * LaneSpeedGbps;
    }

    internal sealed class ModuleDefinition
    {
        public int PrefabId;
        public int BulkItemId;
        public string StableId;
        public string DisplayName;
        public ModuleFormFactor FormFactor;
        public ModuleMedia Media;
        public string EthernetStandard;
        public float SpeedGbps;
        public float MaxReachMeters;
        public float PowerWatts;
        public ConnectorType Connector;
        public int ElectricalLaneCount;
        public float LaneSpeedGbps;
        public int RequiredPortSfpType = -1;
        public BreakoutProfile[] BreakoutOptions = Array.Empty<BreakoutProfile>();
        public float PriceMultiplier;
        public int XpToUnlock;
        public string ShopGuid;
        public Color ModuleColor;
        public ModuleLifecycle Lifecycle;
        public string SourceNote;

        internal float InternalSpeed => SpeedGbps / 5f;
        internal bool IsShopItem => Lifecycle != ModuleLifecycle.Legacy;

        internal bool IsPlausible()
        {
            if (PrefabId <= 0 || BulkItemId <= 0 || string.IsNullOrWhiteSpace(StableId) ||
                string.IsNullOrWhiteSpace(ShopGuid) || SpeedGbps <= 0 || MaxReachMeters <= 0 ||
                PriceMultiplier <= 0 || ElectricalLaneCount <= 0 || LaneSpeedGbps <= 0)
                return false;

            foreach (var breakout in BreakoutOptions)
            {
                if (breakout == null || breakout.LaneCount <= 0 || breakout.LaneSpeedGbps <= 0 ||
                    breakout.TotalSpeedGbps > SpeedGbps + 0.01f)
                    return false;
            }

            return true;
        }
    }

    internal static class ModuleCatalog
    {
        // These IDs are deliberately explicit. Never derive save IDs from list order.
        internal const int FirstNewPrefabId = 110;
        internal const int FirstNewBulkId = 210;

        internal static readonly ModuleDefinition[] All =
        {
            New(110, 210, "qsfp28_100g_dac_v1", "QSFP28 100G DAC", ModuleFormFactor.Qsfp28,
                ModuleMedia.PassiveDac, "100GBASE-CR4", 100, 3, 1.5f, ConnectorType.Mpo, 4, 25,
                1.5f, 0, 2.2f, new Color(0.45f, 0.45f, 0.45f, 1), ModuleLifecycle.Stable,
                "100G QSFP28 passive direct-attach copper; short rack interconnect."),
            New(111, 211, "qsfp28_100g_aoc_v1", "QSFP28 100G AOC", ModuleFormFactor.Qsfp28,
                ModuleMedia.Aoc, "100G AOC", 100, 30, 2.5f, ConnectorType.Mpo, 4, 25,
                1.7f, 0, 2.8f, new Color(0.2f, 0.75f, 0.75f, 1), ModuleLifecycle.Stable,
                "100G active optical cable."),
            New(112, 212, "qsfp28_100g_sr4_v1", "QSFP28 100GBASE-SR4", ModuleFormFactor.Qsfp28,
                ModuleMedia.MultimodeFiber, "100GBASE-SR4", 100, 100, 3.5f, ConnectorType.Mpo, 4, 25,
                2.0f, 0, 3.4f, new Color(0.2f, 0.8f, 0.35f, 1), ModuleLifecycle.Stable,
                "100G multimode-fiber short-reach optic."),
            New(113, 213, "qsfp28_100g_fr_v1", "QSFP28 100GBASE-FR", ModuleFormFactor.Qsfp28,
                ModuleMedia.SinglemodeFiber, "100GBASE-FR", 100, 2000, 4.5f, ConnectorType.Lc, 4, 25,
                2.5f, 0, 4.6f, new Color(0.2f, 0.35f, 0.95f, 1), ModuleLifecycle.Stable,
                "100G singlemode-fiber 2 km optic."),
            New(114, 214, "qsfp28_100g_lr4_v1", "QSFP28 100GBASE-LR4", ModuleFormFactor.Qsfp28,
                ModuleMedia.SinglemodeFiber, "100GBASE-LR4", 100, 10000, 5.5f, ConnectorType.Lc, 4, 25,
                2.8f, 0, 6.2f, new Color(0.95f, 0.8f, 0.1f, 1), ModuleLifecycle.Stable,
                "100G singlemode-fiber long-reach optic."),
            New(115, 215, "qsfp56_200g_sr4_v1", "QSFP56 200G SR4", ModuleFormFactor.Qsfp56,
                ModuleMedia.MultimodeFiber, "200GBASE-SR4", 200, 100, 4.5f, ConnectorType.Mpo, 4, 50,
                3.4f, 0, 6.8f, new Color(0.15f, 0.8f, 0.45f, 1), ModuleLifecycle.Stable,
                "200G multimode-fiber short-reach optic."),
            New(116, 216, "qsfp56_200g_fr4_v1", "QSFP56 200G FR4", ModuleFormFactor.Qsfp56,
                ModuleMedia.SinglemodeFiber, "200GBASE-FR4", 200, 2000, 5.5f, ConnectorType.Lc, 4, 50,
                3.8f, 0, 8.5f, new Color(0.2f, 0.45f, 0.95f, 1), ModuleLifecycle.Stable,
                "200G singlemode-fiber 2 km optic."),
            New(117, 217, "qsfpdd_400g_dr4_v1", "QSFP-DD 400G DR4", ModuleFormFactor.QsfpDd,
                ModuleMedia.SinglemodeFiber, "400GBASE-DR4", 400, 500, 8f, ConnectorType.Mpo, 8, 50,
                4.5f, 200, 11f, new Color(0.2f, 0.4f, 0.95f, 1), ModuleLifecycle.Stable,
                "400G singlemode-fiber 500 m optic."),
            New(118, 218, "qsfpdd_400g_fr4_v1", "QSFP-DD 400G FR4", ModuleFormFactor.QsfpDd,
                ModuleMedia.SinglemodeFiber, "400GBASE-FR4", 400, 2000, 9f, ConnectorType.Lc, 4, 100,
                4.8f, 200, 13f, new Color(0.2f, 0.8f, 0.35f, 1), ModuleLifecycle.Stable),
            New(119, 219, "qsfpdd_400g_lr4_v1", "QSFP-DD 400G LR4", ModuleFormFactor.QsfpDd,
                ModuleMedia.SinglemodeFiber, "400GBASE-LR4", 400, 10000, 10f, ConnectorType.Lc, 4, 100,
                5.0f, 200, 15f, new Color(0.95f, 0.8f, 0.1f, 1), ModuleLifecycle.Stable),
            New(120, 220, "qsfpdd800_800g_dr8_v1", "QSFP-DD800 800G DR8", ModuleFormFactor.QsfpDd800,
                ModuleMedia.SinglemodeFiber, "800GBASE-DR8", 800, 500, 14f, ConnectorType.Mpo, 8, 100,
                6.5f, 300, 22f, new Color(0.55f, 0.1f, 0.85f, 1), ModuleLifecycle.Experimental),
            New(121, 221, "qsfpdd1600_1600g_experimental_v1", "QSFP-DD1600 1.6T Experimental",
                ModuleFormFactor.QsfpDd1600, ModuleMedia.CoherentDwdm, "QSFP-DD1600", 1600, 2000, 18f,
                ConnectorType.Lc, 8, 200, 8f, 500, 42f, new Color(0.65f, 0.1f, 0.85f, 1),
                ModuleLifecycle.Experimental),
        };

        internal static readonly ModuleDefinition[] Legacy =
        {
            LegacyEntry(100, "legacy_qsfp_100g_v1", "Legacy QSFP 100G", 100, new Color(0f, .8f, .3f, 1)),
            LegacyEntry(101, "legacy_qsfp_200g_v1", "Legacy QSFP 200G", 200, new Color(1f, .5f, 0f, 1)),
            LegacyEntry(102, "legacy_qsfp_400g_v1", "Legacy QSFP 400G", 400, new Color(1f, .75f, 0f, 1)),
            LegacyEntry(103, "legacy_qsfp_800g_v1", "Legacy QSFP 800G", 800, new Color(.9f, .05f, .05f, 1)),
            LegacyEntry(104, "legacy_qsfp_1600g_v1", "Legacy QSFP 1.6T", 1600, new Color(.6f, 0f, 1f, 1)),
            LegacyEntry(105, "legacy_qsfp_3200g_v1", "Legacy QSFP 3.2T", 3200, new Color(1f, 0f, .6f, 1)),
            LegacyEntry(106, "legacy_qsfp_6400g_v1", "Legacy QSFP 6.4T", 6400, new Color(0f, .9f, .9f, 1)),
        };

        internal static ModuleDefinition Find(int prefabId)
        {
            foreach (var definition in All)
                if (definition.PrefabId == prefabId) return definition;
            foreach (var definition in Legacy)
                if (definition.PrefabId == prefabId) return definition;
            return null;
        }

        private static ModuleDefinition New(int prefabId, int bulkItemId, string stableId, string displayName,
            ModuleFormFactor formFactor, ModuleMedia media, string standard, float speed, float reach,
            float watts, ConnectorType connector, int lanes, float laneSpeed, float price, int xp,
            float multiplier, Color color, ModuleLifecycle lifecycle, string source = "") => new ModuleDefinition
        {
            PrefabId = prefabId, BulkItemId = bulkItemId, StableId = stableId, DisplayName = displayName,
            FormFactor = formFactor, Media = media, EthernetStandard = standard, SpeedGbps = speed,
            MaxReachMeters = reach, PowerWatts = watts, Connector = connector, ElectricalLaneCount = lanes,
            LaneSpeedGbps = laneSpeed, PriceMultiplier = multiplier, XpToUnlock = xp,
            ShopGuid = "realistic_modules_" + stableId, ModuleColor = color, Lifecycle = lifecycle,
            SourceNote = string.IsNullOrWhiteSpace(source) ? standard + " product-class baseline." : source,
            BreakoutOptions = lanes > 1 ? new[] { new BreakoutProfile { DisplayName = $"{lanes}x {laneSpeed:0}G", LaneCount = lanes, LaneSpeedGbps = laneSpeed } } : Array.Empty<BreakoutProfile>(),
        };

        private static ModuleDefinition LegacyEntry(int prefabId, string stableId, string name, float speed, Color color) => new ModuleDefinition
        {
            PrefabId = prefabId, BulkItemId = 0, StableId = stableId, DisplayName = name,
            FormFactor = ModuleFormFactor.QsfpPlus, Media = ModuleMedia.SinglemodeFiber,
            EthernetStandard = "Legacy", SpeedGbps = speed, MaxReachMeters = 1, PowerWatts = 1,
            Connector = ConnectorType.Mpo, ElectricalLaneCount = 4, LaneSpeedGbps = speed / 4,
            PriceMultiplier = 1, XpToUnlock = 0, ShopGuid = "realistic_modules_" + stableId,
            ModuleColor = color, Lifecycle = ModuleLifecycle.Legacy, SourceNote = "Reserved for save migration.",
        };
    }

    internal static class ModuleList
    {
        internal static ModuleDefinition[] All => ModuleCatalog.All;
    }
}
