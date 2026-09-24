# gregMod.RealisticModules

> gregMod.RealisticModules

![License](https://img.shields.io/github/license/mleem97/gregMod.RealisticModules?style=for-the-badge) ![Last commit](https://img.shields.io/github/last-commit/mleem97/gregMod.RealisticModules?style=for-the-badge) ![Repo size](https://img.shields.io/github/repo-size/mleem97/gregMod.RealisticModules?style=for-the-badge) ![Stars](https://img.shields.io/github/stars/mleem97/gregMod.RealisticModules?style=for-the-badge)

## Links

- **Steam Workshop:** [My Workshop (Data Center)](https://steamcommunity.com/id/frikadelle3000/myworkshopfiles/?appid=4170200)
- **Repository:** [https://github.com/mleem97/gregMod.RealisticModules](https://github.com/mleem97/gregMod.RealisticModules)
- **Issues:** [https://github.com/mleem97/gregMod.RealisticModules/issues](https://github.com/mleem97/gregMod.RealisticModules/issues)
- **Releases:** [https://github.com/mleem97/gregMod.RealisticModules/releases](https://github.com/mleem97/gregMod.RealisticModules/releases)

## Overview

**gregMod.RealisticModules** — realistic transceiver modules for the Data Center shop
(100G–1.6T product classes with media, reach, and lane metadata). Ships the same package
system as `gregMod.MoreModules`: **5 / 16 / 32 / 64 / 128**-piece boxes, form-factor
shop templates, and a post-checkout box-expansion scanner. When both mods are installed,
`gregMod.MoreModules` yields while RealisticModules is **Enabled** (F1 toggle).

Siehe [docs/INDEX.md](docs/INDEX.md) für die komplette Dokumentation.

## Compatibility

| Plattform | Status |
|---|---|
| Windows x64 | Supported |
| Linux x64 | Supported |

## Features

- Realistic 100G / 200G / 400G / 800G / 1.6T transceiver catalog (DAC, AOC, SR/FR/LR, DR)
- Shop packages: **5x box** + **trays 16 / 32 / 64 / 128** (legacy 32x bulk IDs still load)
- Form-factor shop templates (vanilla box array), stable explicit save IDs 110+/210+
- Identity-safe inserts (same-speed variants persist correctly); optional strict port compatibility
- Coexists with `gregMod.MoreModules` (that mod disables itself while this one is enabled)
- **F1 gregCore hub:** open config panel — Master switch **Mod active** + **Strict port compatibility**
- **Mass Insert:** fill all empty matching SFP cages (optional replace when connector still matches, no cable)
- Optional F8 settings tab with the same toggles (MelonPreferences `gregMod.RealisticModules`)
- Siehe [docs/INDEX.md](docs/INDEX.md) und [QUICKSTART.md](QUICKSTART.md)

## Installation

Siehe [QUICKSTART.md](QUICKSTART.md).

## Build from Source

```bash
git clone git@github.com:mleem97/gregMod.RealisticModules.git
cd gregMod.RealisticModules
```

Details: [QUICKSTART.md](QUICKSTART.md), [CONTRIBUTING.md](CONTRIBUTING.md).

## Repository Layout

```
├── README.md            # Diese Datei
├── QUICKSTART.md        # Schnellstart
├── CHANGELOG.md         # Changelog (Keep a Changelog)
├── CONTRIBUTING.md      # Mitmachen
├── SECURITY.md          # Sicherheitsmeldungen
├── CODE_OF_CONDUCT.md   # Verhaltenskodex
├── AGENTS.md            # Hinweise für KI-Agenten
├── LICENSE              # Apache-2.0
├── VERSION              # Single Source of Truth für die Version
├── docs/                # Dokumentation ([Index](docs/INDEX.md))
├── scripts/             # Build-/Hilfsskripte
├── tests/               # Tests
├── references/          # Referenzen
├── sponsors/            # Sponsoren
└── examples/            # Beispiele
```

## API Documentation

Siehe [`docs/INDEX.md`](docs/INDEX.md).

## Credits

| Rolle | Contributor |
|---|---|
| **Codebase** | [mleem97](https://github.com/mleem97) |

## Contributing

Siehe [CONTRIBUTING.md](CONTRIBUTING.md).

## License

Apache-2.0 — siehe [`LICENSE`](LICENSE).

## 🚀 Join the gregFramework Team!

Baust du gerne Mods, Tools oder Docs? Melde dich: **apply@gregframework.eu** oder via
[Discord](https://discord.gg/greg) — Code, Assets, Docs, Testing, Infra, Community.

---

**gregFramework — powered by the community.**

