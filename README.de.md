# ForkPlus-Next

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Build](https://github.com/hebin123456/ForkPlus-Next/actions/workflows/build.yml/badge.svg)](https://github.com/hebin123456/ForkPlus-Next/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/hebin123456/ForkPlus-Next)](https://github.com/hebin123456/ForkPlus-Next/releases)

Die plattformübergreifende Portierung von [ForkPlus](https://github.com/hebin123456/ForkPlus) (der WPF-Edition): Die UI-Schicht wurde auf .NET 10 + Avalonia 12 neu geschrieben — eine einzige Codebasis läuft auf Windows / Linux / macOS. Die darunterliegende Rust-Engine (biturbo native), KI-gestützte Entwicklung, 8 Sprachen, 12 Theme-Skins, der git mm-Workflow sowie Visualisierungen wie Beitrags-Heatmaps und Repository-Treemaps bleiben gegenüber dem Original unverändert.

> Umgebungsaufbau der Migration, die Kette historischer Fixes und die gewonnenen Erkenntnisse sind in [MIGRATION.md](MIGRATION.md) dokumentiert.

[English](README.en.md) | [简体中文](README.md) | [繁體中文](README.zh-Hant.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Français](README.fr.md) | [Deutsch](README.de.md) | [Español](README.es.md)

## Hauptfunktionen

- **Plattformübergreifend**: Auf Avalonia 12 basierende UI-Schicht; die CI erzeugt parallel Builds für Windows x64 / Linux x64 / macOS arm64
- **Mehrsprachigkeit**: 8 integrierte Sprachen (Englisch, vereinfachtes Chinesisch, traditionelles Chinesisch, Japanisch, Koreanisch, Französisch, Deutsch, Spanisch), per JSON-Dateien erweiterbar
- **Mehrere Themes**: 12 eingebaute Skins (Light/Dark, Solarized, GitHub, Dracula, Monokai, Lila/Grün hell & dunkel) sowie benutzerdefinierte Farbüberschreibungen, die sofort angewendet werden
- **git mm-Workflow**: Mitgelieferter `git mm`-Unterbefehl für Lean-Branching-Workflows zur einheitlichen Verwaltung und Synchronisierung von Änderungen über mehrere Sub-Repositories
- **KI-gestützte Entwicklung**: Integrierte KI-Codeüberprüfung, automatische Commit-Nachrichten-Generierung und KI-gestützte Codeänderung
- **Beitrags-Heatmap**: GitHub-artige Commit-Heatmap über 53 Wochen × 7 Tage mit Farbskala-Legende und Statistikzusammenfassung (Gesamtcommits / längste Serie / aktivster Tag); beim Hovern werden Commits des Tages und Top-3-Autoren angezeigt
- **Repository-Treemap**: Visualisierung der Dateigrößen basierend auf dem Treemap-Algorithmus von biturbo native, mit Drilldown per Klick
- **Tracking entfernter Branches**: Der Rechtsklick-Befehl „Tracken“ ist ein zweistufiges, nach Remote gruppiertes Menü mit angedocktem Suchfeld für das schnelle Auffinden unter vielen Remote-Branches
- **Leistungsoptimierungen**: Gezielte Verbesserungen für Aktualisierung großer Repositorys, Diff-Rendering und Submodul-Verwaltung
- **Code-Statistiken**: Integriert tokei (Rust, 200+ Sprachen) für zeilenbasierte Statistiken pro Sprache (Dateien, Code, Kommentare, Leerzeilen) mit Tortendiagramm, unterstützt Workspace/Branch/Tag-Ref-Wechsel

## Repository-Struktur

```
ForkPlus-Next/
├── src/
│   ├── ForkPlus/              # Hauptanwendungsquelle (plattformübergreifende Avalonia-12-UI), XAML, Assets
│   │   ├── Biturbo/           # P/Invoke-Bindungen für die native biturbo-Bibliothek
│   │   ├── Languages/         # Mehrsprachige Übersetzungsdateien (JSON)
│   │   │   ├── zh-Hans.json   # Vereinfachtes Chinesisch
│   │   │   ├── zh-Hant.json   # Traditionelles Chinesisch
│   │   │   ├── ja-JP.json     # Japanisch
│   │   │   ├── ko-KR.json     # Koreanisch
│   │   │   ├── fr-FR.json     # Französisch
│   │   │   ├── de-DE.json     # Deutsch
│   │   │   ├── es-ES.json     # Spanisch
│   │   │   └── README.md      # Beschreibung des Sprachdateiformats
│   │   └── ...
│   ├── ForkPlus.AskPass/      # Git/SSH-Askpass-Hilfsprogramm
│   ├── ForkPlus.RI/           # Interaktiver Rebase-Editor-Hilfsprogramm
│   ├── ForkPlus.Tests/        # xUnit-Einheitentests (inkl. Avalonia.Headless-UI-Rauchtests)
│   ├── ForkPlus.AskPass.Tests/# Einheitentests des AskPass-Hilfsprogramms
│   └── ForkPlus.RI.Tests/     # Einheitentests des RI-Hilfsprogramms
├── third_party/               # Zur Build-Zeit bezogene native Binärdateien (siehe „Native biturbo-Bibliothek“ unten)
├── gitmm/                     # git mm-Workflow-Referenzdokumentation
└── .github/workflows/         # GitHub Actions CI-Konfiguration
```

## Kompilierung

### Voraussetzungen

- Windows 10 oder höher / Linux / macOS (plattformübergreifend)
- .NET 10 SDK
- IDE (optional): Visual Studio 2026 (Windows; die Lösung lässt sich per `OpenForkPlusInVS2026.cmd` im Repository-Stammverzeichnis mit einem Klick öffnen), Rider oder VS Code
- Git 2.40 oder höher (empfohlen; ältere Versionen lösen beim Start eine Warnung aus und einige Funktionen funktionieren möglicherweise nicht. Die App bevorzugt die mitgelieferte Git-Instanz 2.50.1 und fällt bei fehlender Instanz auf das System-Git zurück)
- git-mm 3.0 oder höher (erforderlich für den git mm-Workflow; Warnung beim Start bei älterer Version; ohne ihn sind die git-mm-Workspace-Funktionen nicht verfügbar; git-mm-Pfad in den Einstellungen konfigurierbar)

### Kompilierungsschritte

```bash
# ① Die Chartbibliothek OxyPlot.Avalonia wird als „Quellreferenz außerhalb des
#    Repositorys“ integriert (der csproj-Pfad zeigt auf ein Nachbarverzeichnis
#    dieses Repositorys); vor dem ersten Build neben dieses Repository klonen.
#    Die offizielle Version unangetastet lassen und ihre Avalonia-Version nicht
#    ändern:
git clone --depth 1 https://github.com/oxyplot/oxyplot-avalonia.git ../oxyplot-avalonia

# ② Kompilieren (im Repository-Stammverzeichnis):
dotnet build ForkPlus.sln -c Release
```

Alternativ können Sie `ForkPlus.sln` im Repository-Stammverzeichnis direkt mit Visual Studio 2026 öffnen.

### Native biturbo-Bibliothek

Das native biturbo-Add-on (Rust) bietet Treemap-Layout, Commit-Graph-Caching, Revision-Header-Parsing und mehr. **Die Binärdatei wird nicht in dieses Repository eingecheckt**, sondern zur Build-Zeit automatisch aus dem neuesten Release des [Biturbo-Repositorys](https://github.com/hebin123456/Biturbo) bezogen — plattformabhängig:

| Plattform | Datei |
|------|------|
| Windows x64 | `third_party/biturbo.dll` |
| Linux x64 | `third_party/libbiturbo.so` |
| macOS arm64 | `third_party/libbiturbo.dylib` |

Mechanismen (siehe [ForkPlus.csproj](src/ForkPlus/ForkPlus.csproj)):

- `RestoreBiturbo`-Target (`BeforeTargets=Build`): lädt die fehlende native Bibliothek der aktuellen Plattform automatisch herunter (PowerShell unter Windows, bash + curl unter Linux/macOS mit Auswahl von `.so` / `.dylib` per `uname -s`, jeweils mit Wiederholungen)
- `CopyHelperExecutables` / `PublishHelperExecutables`-Targets (`AfterTargets=Build` / `Publish`): kopieren die native Bibliothek und die AskPass/RI-Hilfsprogrammausgaben in die Build-/Publish-Verzeichnisse
- `.gitignore` schließt diese Dateien unter `third_party/` bereits aus

Daher benötigt der erste Build Netzwerkzugriff auf GitHub; in der CI lädt der Workflow sie explizit herunter und prüft sie (nicht leer und > 1 MB), wobei `RestoreBiturbo` im csproj als Rückfallebene dient.

### tokei

[tokei](https://github.com/XAMPPRocky/tokei) (MIT-lizenziert) treibt das Panel „Codezeilen“ an. Zur Build-Zeit wird das **vorkompilierte Binärprogramm** aus dem neuesten Release des Repositorys [hebin123456/tokei](https://github.com/hebin123456/tokei) bezogen — keine lokale Rust-Toolchain nötig:

- Windows x64 → reine exe, gespeichert als `third_party/tokei.exe`
- Linux x64 / macOS → tar.gz (enthält das reine `tokei`-Binärprogramm), entpackt nach `third_party/tokei`
- Das macOS-Artefakt ist x86_64 und läuft auf Apple Silicon über Rosetta 2

Gleicher Mechanismus wie bei biturbo: das `RestoreTokei`-Target (`BeforeTargets=Build`) holt es automatisch, die CI lädt und prüft es explizit, und `.gitignore` schließt die Artefakte aus.

### Kontinuierliche Integration

Das Projekt ist mit GitHub Actions konfiguriert ([`.github/workflows/build.yml`](.github/workflows/build.yml)): Bei Push / PR auf `master` oder manuellem Start wird parallel auf drei Plattformen kompiliert und die Artefakte hochgeladen:

| Matrix | Runner | RID |
|--------|--------|-----|
| windows-x64 | windows-latest | win-x64 |
| linux-x64 | ubuntu-latest | linux-x64 |
| macos-arm64 | macos-latest | osx-arm64 |

Die Artefakte sind **frameworkabhängige Publishes** (der Zielrechner benötigt die .NET-10-Runtime) und enthalten die Haupt-App, das AskPass/RI-Hilfsprogramm-Trio, die biturbo-Nativbibliothek der Plattform, tokei und die Sprachdateien. Download über den entsprechenden Lauf auf der [Actions](https://github.com/hebin123456/ForkPlus-Next/actions)-Seite (Artifacts, 14 Tage aufbewahrt).

## Tests

- Einheitentests: `dotnet test src/ForkPlus.Tests/ForkPlus.Tests.csproj` (inkl. plattformübergreifender Avalonia.Headless-UI-Rauch- und End-to-End-Tests, zusammen mit den Einheitentests)
- Insgesamt 4000+ Fälle; jeder wichtige Fix der Migration hat eine Regressionssicherung (siehe [MIGRATION.md](MIGRATION.md))

## Mehrsprachigkeitsunterstützung

### Integrierte Sprachen

| Sprachcode | Anzeigename | Status |
|------------|-------------|--------|
| `en` | English | Quellsprache |
| `zh-Hans` | 简体中文 | Vollständig |
| `zh-Hant` | 繁體中文 | Vollständig |
| `ja-JP` | 日本語 | Vollständig |
| `ko-KR` | 한국어 | Vollständig |
| `fr-FR` | Français | Vollständig |
| `de-DE` | Deutsch | Vollständig |
| `es-ES` | Español | Vollständig |

### Hinzufügen einer neuen Sprache

Erstellen Sie eine neue `<sprachcode>.json`-Datei im Verzeichnis `src/ForkPlus/Languages/` — keine Codeänderungen erforderlich. Dateiformat:

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

Siehe [src/ForkPlus/Languages/README.md](src/ForkPlus/Languages/README.md) für Details.

### Internationalisierungs-API

Die Codebasis verwendet die folgenden APIs für die Internationalisierung:

- `PreferencesLocalization.Current("English text")` — Einfache Zeichenfolgenübersetzung
- `PreferencesLocalization.FormatCurrent("...{0}...", args)` — Parametrisierte Zeichenfolgenübersetzung
- `PreferencesLocalization.Translate(text, language)` — Übersetzung für eine bestimmte Sprache

## Download

- CI-Build-Artefakte: [Actions](https://github.com/hebin123456/ForkPlus-Next/actions)-Seite → der entsprechende Build-Lauf → Artifacts (frameworkabhängig, .NET-10-Runtime erforderlich)
- Offizielle Releases: [Releases-Seite](https://github.com/hebin123456/ForkPlus-Next/releases)
- Die Änderungen der einzelnen Versionen finden Sie in den [Release Notes](RELEASE_NOTE.md) (inkl. der Geschichte der WPF-Edition)

## Entwicklungskonventionen

- Beim Ändern der Anwendung selbst bleiben Sie im Verzeichnis `src/ForkPlus`; Laufzeit-Binärdateien unter `third_party/` (native biturbo-Bibliothek, tokei) werden zur Build-Zeit automatisch bezogen — Binärdateien nicht manuell einchecken
- Zum Upgrade von biturbo / tokei genügt ein neues Release im jeweiligen Repository; der nächste Build dieses Repositorys holt es automatisch
- `../oxyplot-avalonia` ist eine Quellreferenz außerhalb des Repositorys — die offizielle Version unangetastet lassen (die Lehre aus einer versehentlich geänderten Version steht in MIGRATION.md)
- Umgebungsaufbau, laufende Arbeiten und die Kette historischer Fixes der Migration (WPF → Avalonia) sind in [MIGRATION.md](MIGRATION.md) dokumentiert

## Lizenz

Dieses Projekt ist Open Source unter der [MIT-Lizenz](LICENSE).

Copyright (c) 2026 hebin123456
