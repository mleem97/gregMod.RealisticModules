# Changelog

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
