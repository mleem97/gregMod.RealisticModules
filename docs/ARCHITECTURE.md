# Architektur — gregMod.RealisticModules

> Realistic transceiver catalog with the MoreModules v1.0.18 package system.

## Komponenten

- **`src/Core.cs`** — MelonMod entry, registry setup, prefab builders, shop injection, tray/bulk box expansion.
- **`src/Patches.cs`** — Harmony patches (`MainGameManager.Awake/Start`, cart, checkout, `GetPrefabForItem`, save load, insert, box accept).
- **`src/ModuleDefinition.cs`** — enums, `ModuleDefinition`, `ModuleCatalog` (explicit IDs 110+/210+, legacy 100–106).
- **`src/ModuleRegistry.cs`** — prefab/bulk/tray lookup, live identity map, legacy speed migration.
- **`src/ModuleValidation.cs`**, **`src/CompatibilityMatrix.cs`** — startup validation and optional strict port mode.
- **Skripte / Tests / Referenzen:** [`scripts/`](../scripts/), [`tests/`](../tests/), [`references/`](../references/).

## Datenflüsse

1. `MainGameManager.Awake` → `Core.SetupRegistry` (vanilla `SFP_*` base scan, `BaseBoxPrefabIndex`, extend `sfpPrefabs`, park templates under inactive `TemplateHolder`).
2. Scene load → shop coroutine injects **5x** + trays **16/32/64/128** per module (`TRAY_ID_BASE` 310+).
3. Cart add (no physical spawn) → checkout → `GetPrefabForItem` builds one tray/bulk/regular box → `ExpandAllSizedBoxes` grows slots to capacity.
4. Insert into port → identity from live map / name marker / tagged take (not speed-only).

Änderungen hier + [`CHANGELOG.md`](../CHANGELOG.md) nachtragen.
