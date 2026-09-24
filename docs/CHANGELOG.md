# Changelog

## v1.3.0

- **Mass Insert** (F1 panel + F8 settings tab):
  - **Fill empty ports** — spawns catalog modules into every empty SFP cage
    whose `sfpTypeSupported` matches a module form factor (round-robin per type).
  - **Fill + replace matching** — also swaps occupied cages when the connector
    type still matches and no cable is attached (`RemoveSFP` → `InsertSFP`).
  - Always uses vanilla `CableLink.InsertSFP` so identity rewrite, gregCore
    hooks, and Backplanes reassert run; one insert per frame; safety cap 400.

## v1.2.0

- **F1 / gregCore integration:** RealisticModules appears in the F1 mod hub
  with an openable config panel (Mod active + Strict port compatibility).
  Optional F8 settings tab with the same toggles.
- **Master switch `Enabled`** (MelonPreferences category `gregMod.RealisticModules`):
  when off, catalog setup, shop injection, and Harmony behavior stay vanilla.
  Soft re-enable reuses existing templates when possible; full effect after
  scene load / restart.
- gregCore is optional at runtime (`GregHost` type probe + separate
  registration method so the mod still loads without gregCore).

## v1.1.0

- Feature parity with `gregMod.MoreModules` v1.0.18 (realistic catalog kept):
  - **Tray packages 16 / 32 / 64 / 128** next to the classic 5-piece box
    (IDs `TRAY_ID_BASE` 310+; price scales with the piece count). The legacy
    32x bulk path (`BulkItemId` 210+) keeps working.
  - **Checkout box scanner:** delivery boxes expand to their tray capacity;
    scanner restarts on `ComputerShop.ButtonCheckOut` and polls a time window
    (≈12 s after the last box) so late spawns still upgrade.
  - **No triple-spawn:** add-to-cart no longer calls `SpawnPhysicalItem`
    (cart entry only); tray/bulk templates park under the inactive
    `TemplateHolder`, so exactly one delivery box is instantiated.
  - **Vanilla-name base filter:** shop/module base detection only accepts
    `SFP_*` names (avoids picking pre-existing `CustomSFP_*` / templates).
  - **Box clone source:** `BaseBoxIndex` / global `BaseBoxPrefabIndex`
    (vanilla box array) instead of module prefabID space.
  - **Per-form shop templates/sprites** for RJ45 / SFP+ / SFP28 / QSFP+ forms.
- Explicit IDs 110+/210+ and legacy save migration remain unchanged.
- Updated game interop references for Data Center 1.1.0 on Unity 6000.4.12f1.

## v1.0.0

- Reworked the MoreModules basis into `gregMod.RealisticModules`
- Added explicit stable prefab and bulk IDs beginning at 110/210
- Added technical module metadata, catalog validation, legacy aliases, and identity migration
- Added realistic 100G, 200G, 400G, 800G, and experimental 1.6T variants
- Updated game interop references for Data Center 1.1.0 on Unity 6000.4.12f1
