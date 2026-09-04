# ForkPlus-Next

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Build](https://github.com/hebin123456/ForkPlus-Next/actions/workflows/build.yml/badge.svg)](https://github.com/hebin123456/ForkPlus-Next/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/hebin123456/ForkPlus-Next)](https://github.com/hebin123456/ForkPlus-Next/releases)

The cross-platform port of [ForkPlus](https://github.com/hebin123456/ForkPlus) (the WPF edition): the UI layer is rewritten on .NET 10 + Avalonia 12, and a single codebase runs on Windows / Linux / macOS. The underlying Rust engine (biturbo native), AI-assisted development, 8 languages, 12 theme skins, the git mm workflow, and visualizations such as contribution heatmaps and repository treemaps remain identical to the original.

> Environment setup for the migration, the chain of historical fixes, and lessons learned are documented in [MIGRATION.md](MIGRATION.md).

[English](README.en.md) | [简体中文](README.md) | [繁體中文](README.zh-Hant.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Français](README.fr.md) | [Deutsch](README.de.md) | [Español](README.es.md)

## Key Features

- **Cross-platform**: Avalonia 12 based cross-platform UI layer; CI produces Windows x64 / Linux x64 / macOS arm64 builds in parallel
- **Multi-language support**: Built-in English, Simplified Chinese, Traditional Chinese, Japanese, Korean, French, German, Spanish, extensible with more languages via JSON files
- **Multiple themes**: 12 built-in skins (Light/Dark, Solarized, GitHub, Dracula, Monokai, Purple/Green light & dark) plus user-customizable color overrides applied instantly
- **git mm workflow**: Bundled `git mm` subcommand providing Lean Branching workflows that manage changes and sync across multiple sub-repositories
- **AI-assisted development**: Integrated AI code review, automatic commit message generation, and AI-assisted code modification
- **Contribution heatmap**: GitHub-style 53-week × 7-day commit heatmap with color-scale legend and statistics summary (total commits / longest streak / most active day); hovering shows the commit count and Top 3 authors of that day
- **Repository treemap**: File-size visualization powered by the biturbo native treemap algorithm, with click-to-drill-down
- **Remote branch tracking**: The right-click "Track" action is a two-level menu grouped by remote with a pinned search box, for quickly locating branches among many remotes
- **Performance optimizations**: Targeted improvements for large repository refresh, diff rendering, and submodule management
- **Code statistics**: Integrates tokei (Rust, 200+ languages) for per-language line counts (files, code, comments, blanks) with pie-chart visualization, supporting Workspace/branch/tag ref switching

## Repository Layout

```
ForkPlus-Next/
├── src/
│   ├── ForkPlus/              # Main application source (Avalonia 12 cross-platform UI), XAML, assets
│   │   ├── Biturbo/           # P/Invoke bindings for the biturbo native library
│   │   ├── Languages/         # Localization translation files (JSON)
│   │   │   ├── zh-Hans.json   # Simplified Chinese
│   │   │   ├── zh-Hant.json   # Traditional Chinese
│   │   │   ├── ja-JP.json     # Japanese
│   │   │   ├── ko-KR.json     # Korean
│   │   │   ├── fr-FR.json     # French
│   │   │   ├── de-DE.json     # German
│   │   │   ├── es-ES.json     # Spanish
│   │   │   └── README.md      # Language file format description
│   │   └── ...
│   ├── ForkPlus.AskPass/      # Git/SSH askpass helper
│   ├── ForkPlus.RI/           # Interactive rebase editor helper
│   ├── ForkPlus.Tests/        # xUnit unit tests (incl. Avalonia.Headless UI smoke tests)
│   ├── ForkPlus.AskPass.Tests/# AskPass helper unit tests
│   └── ForkPlus.RI.Tests/     # RI helper unit tests
├── third_party/               # Native binaries fetched at build time (see "biturbo native library" below)
├── gitmm/                     # git mm workflow reference docs
└── .github/workflows/         # GitHub Actions CI config
```

## Build

### Prerequisites

- Windows 10 or later / Linux / macOS (cross-platform)
- .NET 10 SDK
- IDE (optional): Visual Studio 2026 (Windows; open the solution with one click via `OpenForkPlusInVS2026.cmd` in the repo root), Rider, or VS Code
- Git 2.40 or later (recommended; older versions trigger a startup warning and some features may misbehave. The app prefers its bundled git 2.50.1 and falls back to the system git when missing)
- git-mm 3.0 or later (required for the git mm workflow; a warning is shown on startup if older; without it the git mm workspace features are unavailable; configure the git-mm path in Preferences)

### Build Steps

```bash
# ① The charting library OxyPlot.Avalonia is integrated as an out-of-repo source
#    reference (the csproj points to a sibling directory of this repo), so clone
#    it next to this repo before the first build. Keep the official version
#    untouched and do not modify its Avalonia version:
git clone --depth 1 https://github.com/oxyplot/oxyplot-avalonia.git ../oxyplot-avalonia

# ② Build (from the repo root):
dotnet build ForkPlus.sln -c Release
```

Alternatively, open `ForkPlus.sln` in the repo root directly with Visual Studio 2026.

### biturbo native library

The biturbo native add-on (Rust) provides repository treemap layout, commit-graph caching, revision header parsing, and more. **The binary is not committed to this repo**; instead it is fetched at build time from the latest release of the [Biturbo repository](https://github.com/hebin123456/Biturbo), selecting the file per platform:

| Platform | File |
|----------|------|
| Windows x64 | `third_party/biturbo.dll` |
| Linux x64 | `third_party/libbiturbo.so` |
| macOS arm64 | `third_party/libbiturbo.dylib` |

Mechanics (see [ForkPlus.csproj](src/ForkPlus/ForkPlus.csproj)):

- `RestoreBiturbo` target (`BeforeTargets=Build`): automatically downloads the native library for the current platform when missing (PowerShell on Windows, bash + curl on Linux/macOS selecting `.so` / `.dylib` via `uname -s`, with retries)
- `CopyHelperExecutables` / `PublishHelperExecutables` targets (`AfterTargets=Build` / `Publish`): copy the native library and the AskPass/RI helper outputs to the Build / Publish directories
- `.gitignore` already excludes these files under `third_party/`

Therefore the first build requires network access to GitHub; on CI the workflow downloads and verifies them explicitly (non-empty and >1MB), with the csproj `RestoreBiturbo` as a fallback.

### tokei

[tokei](https://github.com/XAMPPRocky/tokei) (MIT licensed) powers the "lines of code" panel. At build time the **prebuilt binary** is fetched from the latest release of the [hebin123456/tokei](https://github.com/hebin123456/tokei) repository, so no local Rust toolchain is needed:

- Windows x64 → bare exe, saved as `third_party/tokei.exe`
- Linux x64 / macOS → tar.gz (containing a bare `tokei` binary), extracted to `third_party/tokei`
- The macOS asset is x86_64 and runs on Apple Silicon via Rosetta 2

Same mechanism as biturbo: the `RestoreTokei` target (`BeforeTargets=Build`) fetches it automatically, CI downloads and verifies it explicitly, and `.gitignore` excludes the artifacts.

### Continuous Integration

The project is configured with GitHub Actions ([`.github/workflows/build.yml`](.github/workflows/build.yml)): on push / PR to `master`, or manual dispatch, it builds in parallel on three platforms and uploads the artifacts:

| Matrix | Runner | RID |
|--------|--------|-----|
| windows-x64 | windows-latest | win-x64 |
| linux-x64 | ubuntu-latest | linux-x64 |
| macos-arm64 | macos-latest | osx-arm64 |

The artifacts are **framework-dependent publishes** (the target machine needs the .NET 10 runtime), containing the main app, the AskPass/RI helper trio, the platform's biturbo native library, tokei, and language files. Download them from the corresponding run on the [Actions](https://github.com/hebin123456/ForkPlus-Next/actions) page (Artifacts, retained for 14 days).

## Tests

- Unit tests: `dotnet test src/ForkPlus.Tests/ForkPlus.Tests.csproj` (incl. Avalonia.Headless UI smoke and end-to-end tests, cross-platform, run together with unit tests)
- 4000+ cases in total; every key fix during the migration has a regression guard (see [MIGRATION.md](MIGRATION.md))

## Multi-language Support

### Built-in Languages

| Language Code | Display Name | Status |
|---------------|-------------|--------|
| `en` | English | Source language |
| `zh-Hans` | 简体中文 | Complete |
| `zh-Hant` | 繁體中文 | Complete |
| `ja-JP` | 日本語 | Complete |
| `ko-KR` | 한국어 | Complete |
| `fr-FR` | Français | Complete |
| `de-DE` | Deutsch | Complete |
| `es-ES` | Español | Complete |

### Adding a New Language

Create a new `<language-code>.json` file in the `src/ForkPlus/Languages/` directory—no code changes required. File format:

```json
{
  "code": "ko",
  "name": "한국어",
  "translations": {
    "Preferences": "환경설정",
    "General": "일반",
    "Commit": "커밋"
  }
}
```

See [src/ForkPlus/Languages/README.md](src/ForkPlus/Languages/README.md) for details.

### Internationalization API

The codebase uses the following APIs for internationalization:

- `PreferencesLocalization.Current("English text")` — Simple string translation
- `PreferencesLocalization.FormatCurrent("...{0}...", args)` — Parameterized string translation
- `PreferencesLocalization.Translate(text, language)` — Translation for a specific language

## Download

- CI build artifacts: [Actions](https://github.com/hebin123456/ForkPlus-Next/actions) page → the corresponding build run → Artifacts (framework-dependent, requires the .NET 10 runtime)
- Official releases: [Releases page](https://github.com/hebin123456/ForkPlus-Next/releases)
- For changes in each version, see the [Release Notes](RELEASE_NOTE.md) (including the WPF edition history)

## Development Conventions

- When modifying the application itself, stay within `src/ForkPlus`; runtime binaries under `third_party/` (the biturbo native library, tokei) are fetched automatically at build time—do not commit binaries manually
- To upgrade biturbo / tokei, publish a new release in the corresponding repository; the next build of this repo picks it up automatically
- `../oxyplot-avalonia` is an out-of-repo source reference—keep the official version untouched (see MIGRATION.md for the lesson learned from accidentally modifying its version)
- The environment setup, work in progress, and the chain of historical fixes for the migration (WPF → Avalonia) are documented in [MIGRATION.md](MIGRATION.md)

## License

This project is open source under the [MIT License](LICENSE).

Copyright (c) 2026 hebin123456
