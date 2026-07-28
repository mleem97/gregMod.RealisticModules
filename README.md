# gregMod.RealisticModules

> Realistic transceiver variants for **Data Center** with explicit identities, technical metadata, legacy save support, and optional strict host compatibility.

[![Discord](https://img.shields.io/discord/1392073682133848075?style=for-the-badge&logo=discord&logoColor=white&label=Discord)](https://discord.gg/greg)
[![gregFramework](https://img.shields.io/badge/gregFramework-Website-blue?style=for-the-badge)](https://gregframework.eu)
[![License](https://img.shields.io/badge/License-Apache%202.0-green?style=for-the-badge)](./LICENSE)
[![Version](https://img.shields.io/badge/Version-1.0.0-orange?style=for-the-badge)]()
[![GameVersion](https://img.shields.io/badge/Game%20Version-1.1.0-yellow?style=for-the-badge)]()
[![Unity](https://img.shields.io/badge/Unity-6000.4.12f1-black?style=for-the-badge&logo=unity&logoColor=white)]()

## Links

- **Repository:** [github.com/mleem97/gregMod.RealisticModules](https://github.com/mleem97/gregMod.RealisticModules)
- **Discord / Support:** [discord.gg/greg](https://discord.gg/greg)
- **Website:** [gregframework.eu](https://gregframework.eu)

## Overview

The original module basis cloned the fastest vanilla QSFP+ prefab, identified modules by speed, and derived save IDs from array order. This fork uses fixed IDs and a technical catalog so equal-speed variants remain distinct.

## Features

- Explicit `PrefabId`, `BulkItemId`, `StableId`, and shop GUID for every new module
- Legacy IDs `100–106` and the former `1000–1006` aliases remain loadable and are not offered in the shop
- Realistic variants from 100G through experimental 1.6T
- Media, connector, lane, power, reach, lifecycle, source-note, price, and XP metadata
- Identity resolved by saved prefab ID, live object mapping, or stable object marker; speed is migration-only fallback
- Catalog validation disables only invalid catalog registration before touching the shop
- Optional `StrictCompatibility` MelonPreferences setting
- 5-module and 32-module boxes

## Module Catalog

| ID | Module | Medium | Reach | Lifecycle |
|---:|---|---|---:|---|
| 110 | QSFP28 100G DAC | Passive DAC | 3 m | Stable |
| 111 | QSFP28 100G AOC | AOC | 30 m | Stable |
| 112 | QSFP28 100GBASE-SR4 | Multimode fiber | 100 m | Stable |
| 113 | QSFP28 100GBASE-FR | Singlemode fiber | 2 km | Stable |
| 114 | QSFP28 100GBASE-LR4 | Singlemode fiber | 10 km | Stable |
| 115 | QSFP56 200G SR4 | Multimode fiber | 100 m | Stable |
| 116 | QSFP56 200G FR4 | Singlemode fiber | 2 km | Stable |
| 117 | QSFP-DD 400G DR4 | Singlemode fiber | 500 m | Stable |
| 118 | QSFP-DD 400G FR4 | Singlemode fiber | 2 km | Stable |
| 119 | QSFP-DD 400G LR4 | Singlemode fiber | 10 km | Stable |
| 120 | QSFP-DD800 800G DR8 | Singlemode fiber | 500 m | Experimental |
| 121 | QSFP-DD1600 1.6T | Coherent DWDM | 2 km | Experimental |

The vanilla model remains the visual placeholder. Port geometry and physical reach enforcement require additional stable game-side fields and are intentionally not faked by this first implementation.

## Compatibility and Configuration

The setting is stored under `gregMod.RealisticModules`:

- `StrictCompatibility = false`: simplified mode for existing QSFP+ maps.
- `StrictCompatibility = true`: enforces the observed host `sfpType`; future port-generation evidence can extend the matrix without changing save IDs.

Reach, medium, connector, power, and breakout data currently drive catalog metadata, pricing, XP, and shop labels. The game API does not expose a verified cable-length or port-form-factor field in this basis, so reach and physical port shape are not rejected at runtime yet.

## Installation

1. Install MelonLoader for Data Center.
2. Copy `gregMod.RealisticModules.dll` into `Data Center/Mods/`.
3. Launch the game and check the MelonLoader log for catalog validation and compatibility mode.

## Save Compatibility

Existing legacy module IDs are reserved. New entries start at 110 and never depend on catalog ordering. Do not reuse a `PrefabId`, `BulkItemId`, `StableId`, or shop GUID after release.

## Build from Source

Requirements:

- .NET 6 SDK
- Current Data Center 1.1.0 / Unity 6000.4.12f1 references
- MelonLoader references in `references/`

```bash
dotnet build gregMod.RealisticModules.csproj -c Release -p:Platform=x64
```

Release output: `bin/x64/Release/net6.0/gregMod.RealisticModules.dll`

## Project Structure

```
gregMod.RealisticModules/
├── src/
│   ├── Core.cs                 # Prefabs, shop, boxes, configuration
│   ├── ModuleDefinition.cs     # Technical model and catalog
│   ├── ModuleRegistry.cs       # Stable and runtime identity registry
│   ├── ModuleValidation.cs     # Startup catalog validation
│   ├── CompatibilityMatrix.cs  # Host compatibility boundary
│   └── Patches.cs              # Harmony integration points
├── references/                 # Current game and MelonLoader assemblies
├── docs/
├── gregMod.RealisticModules.csproj
└── README.md
```

## Credits

- Original implementation basis: [leoms1408](https://github.com/leoms1408)
- Realistic catalog and gregMod rework: [TeamGreg Modding](https://github.com/teamGregModding)

## License

Licensed under the Apache License 2.0. See [LICENSE](./LICENSE).

## Join the gregFramework Team!

### macOS Support

A native macOS version of Data Center already exists. At the moment, however, there is no implementation path available for macOS support in this mod, and I do not have access to an Apple device for development or testing. I am actively looking for contributors who can help make macOS support possible. See “Join the gregFramework Team” below.

Contributions, testing, documentation, and feedback are welcome in the [greg Discord](https://discord.gg/greg).
