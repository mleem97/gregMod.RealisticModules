# Architecture — gregMod.RealisticModules

> Realistic transceiver catalog with the MoreModules package system, optional gregCore F1 config.

## Components

- **`src/Core.cs`** — MelonMod entry, registry setup, prefab builders, shop injection, tray/bulk box expansion, config load / F1 wiring.
- **`src/Patches.cs`** — Harmony patches (`MainGameManager.Awake/Start`, cart, checkout, `GetPrefabForItem`, save load, insert, box accept); gated on `ModConfig.Enabled`.
- **`src/ModConfig.cs`** — MelonPreferences (`Enabled`, `StrictCompatibility`, `OfferExperimentalModules`).
- **`src/GregHost.cs` / `src/GregCoreIntegration.cs`** — optional gregCore F1 hub + config panel (JIT-safe type probe).
- **`src/MassInsert.cs`** — bulk fill/replace of matching SFP cages via vanilla `InsertSFP`.
- **`src/ModuleDefinition.cs`** — enums, `ModuleDefinition`, `ModuleCatalog` (explicit IDs 110+/210+, legacy 100–106).
- **`src/ModuleRegistry.cs`** — prefab/bulk/tray lookup, live identity map, legacy speed migration.
- **`src/ModuleValidation.cs`**, **`src/CompatibilityMatrix.cs`** — startup validation and optional strict port mode.
- **Scripts / Tests / References:** [`scripts/`](../scripts/), [`tests/`](../tests/), [`references/`](../references/).

## Data flows

0. `OnInitializeMelon` → load prefs; if gregCore present, register F1 hub entry + config panel.
1. `MainGameManager.Awake` → `Core.SetupRegistry` (skipped when `Enabled=false`): vanilla `SFP_*` base scan, `BaseBoxPrefabIndex`, extend `sfpPrefabs`, park templates under inactive `TemplateHolder`.
2. Scene load → shop coroutine injects **5x** + trays **16/32/64/128** per module (`TRAY_ID_BASE` 310+).
3. Cart add (no physical spawn) → checkout → `GetPrefabForItem` builds one tray/bulk/regular box → `ExpandAllSizedBoxes` grows slots to capacity.
4. Insert into port → identity from live map / name marker / tagged take (not speed-only).

Record changes here + in [`CHANGELOG.md`](../CHANGELOG.md) (Unreleased).
