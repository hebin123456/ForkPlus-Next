# ForkPlus-Next

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Build](https://github.com/hebin123456/ForkPlus-Next/actions/workflows/build.yml/badge.svg)](https://github.com/hebin123456/ForkPlus-Next/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/hebin123456/ForkPlus-Next)](https://github.com/hebin123456/ForkPlus-Next/releases)

[ForkPlus](https://github.com/hebin123456/ForkPlus)（WPF 版）的跨平台遷移版本：UI 層基於 .NET 10 + Avalonia 12 重寫，一套程式碼執行於 Windows / Linux / macOS。底層 Rust 引擎（biturbo native）、AI 輔助開發、8 種語言、12 套主題皮膚、git mm 工作流，以及貢獻熱力圖、倉庫樹圖等視覺化能力與原版保持一致。

> 遷移環境搭建、歷史修復鏈與經驗教訓記錄在 [MIGRATION.md](MIGRATION.md)。

[English](README.en.md) | [简体中文](README.md) | [繁體中文](README.zh-Hant.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Français](README.fr.md) | [Deutsch](README.de.md) | [Español](README.es.md)

## 主要特性

- **跨平台**：基於 Avalonia 12 的跨平台 UI 層，CI 同時產出 Windows x64 / Linux x64 / macOS arm64 三平台建置
- **多語言支援**：內建英語、簡體中文、繁體中文、日本語、한국어、Français、Deutsch、Español 8 種語言，並支援透過 JSON 檔案擴充更多語言
- **多主題皮膚**：內建 12 套預設皮膚（Light/Dark、Solarized、GitHub、Dracula、Monokai、紫色/綠色淺色深色），並支援使用者自訂顏色覆蓋，即時生效
- **git mm 工作流**：內建 `git mm` 子命令，提供精益分支（Lean Branching）工作流，統一管理多子倉的變更與同步
- **AI 輔助開發**：整合 AI 程式碼審查、自動產生提交訊息、AI 輔助修改程式碼
- **貢獻熱力圖**：GitHub 風格 53 週 × 7 天提交熱力圖，附帶色階圖例和統計摘要（總提交數 / 最長連續提交天數 / 最活躍日期），滑鼠懸停顯示當日提交數和 Top 3 作者
- **倉庫樹圖**：基於 biturbo native treemap 演算法的倉庫檔案大小視覺化，支援逐級鑽取點擊
- **遠端分支追蹤**：右鍵「追蹤」改為按遠端分組的二級選單，選單內嵌置頂搜尋框，支援大量遠端分支快速檢索
- **效能優化**：針對大型倉庫的重新整理、diff 渲染、子模組管理做了專項優化
- **代碼統計**：整合 tokei（Rust 編寫，支援 200+ 語言），按語言統計代碼行數、檔案數、註解行、空白行，圓餅圖視覺化，支援 Workspace/分支/tag 切換 ref

## 專案結構

```
ForkPlus-Next/
├── src/
│   ├── ForkPlus/              # 主應用程式原始碼（Avalonia 12 跨平台 UI）、XAML、資源
│   │   ├── Biturbo/           # biturbo native 三方件的 P/Invoke 綁定
│   │   ├── Languages/         # 多語言翻譯檔案（JSON）
│   │   │   ├── zh-Hans.json   # 簡體中文
│   │   │   ├── zh-Hant.json   # 繁體中文
│   │   │   ├── ja-JP.json     # 日本語
│   │   │   ├── ko-KR.json     # 한국어
│   │   │   ├── fr-FR.json     # Français
│   │   │   ├── de-DE.json     # Deutsch
│   │   │   ├── es-ES.json     # Español
│   │   │   └── README.md      # 語言檔案格式說明
│   │   └── ...
│   ├── ForkPlus.AskPass/      # Git/SSH 密碼輸入輔助程式
│   ├── ForkPlus.RI/           # 互動式 rebase 編輯器輔助程式
│   ├── ForkPlus.Tests/        # xUnit 單元測試（含 Avalonia.Headless UI 冒煙測試）
│   ├── ForkPlus.AskPass.Tests/# AskPass 輔助程式單元測試
│   └── ForkPlus.RI.Tests/     # RI 輔助程式單元測試
├── third_party/               # 建置期拉取的原生二進位檔案（見下文「biturbo native 庫來源」）
├── gitmm/                     # git mm 工作流參考文件
└── .github/workflows/         # GitHub Actions CI 配置
```

## 編譯

### 環境要求

- Windows 10 或更高版本 / Linux / macOS（跨平台支援）
- .NET 10 SDK
- IDE（可選）：Visual Studio 2026（Windows，可用倉庫根目錄的 `OpenForkPlusInVS2026.cmd` 一鍵開啟解決方案）、Rider 或 VS Code
- Git 2.40 或更高版本（推薦；低於推薦版本啟動時會警告，部分功能可能異常。應用優先使用的內建 git 實例版本為 2.50.1，缺失時回退系統 git）
- git-mm 3.0 或更高版本（使用 git mm 工作流時必需，低於此版本啟動時會警告；未安裝時 git mm 工作區功能不可用，可在偏好設定中配置 git-mm 路徑）

### 編譯步驟

```bash
# ① 圖表庫 OxyPlot.Avalonia 以「倉庫外原始碼引用」方式整合（csproj 相對路徑指向倉庫同級目錄），
#    首次編譯前先複製到本倉庫旁邊。保持官方原版零改動，勿修改其 Avalonia 版本：
git clone --depth 1 https://github.com/oxyplot/oxyplot-avalonia.git ../oxyplot-avalonia

# ② 編譯（倉庫根目錄）：
dotnet build ForkPlus.sln -c Release
```

也可用 Visual Studio 2026 直接開啟倉庫根目錄的 `ForkPlus.sln` 編譯。

### biturbo native 庫來源

biturbo native 三方件（Rust）提供倉庫樹圖佈局、提交圖快取、revision header 解析等能力。**該檔案不以二進位形式提交到本倉庫**，而是在建置期自動從 [Biturbo 倉庫](https://github.com/hebin123456/Biturbo) 的最新 Release 拉取，按平台選擇檔案：

| 平台 | 檔案 |
|------|------|
| Windows x64 | `third_party/biturbo.dll` |
| Linux x64 | `third_party/libbiturbo.so` |
| macOS arm64 | `third_party/libbiturbo.dylib` |

具體機制（見 [ForkPlus.csproj](src/ForkPlus/ForkPlus.csproj)）：

- `RestoreBiturbo` target（`BeforeTargets=Build`）：偵測到當前平台的 native 庫缺失時自動下載（Windows 走 PowerShell，Linux/macOS 走 bash + curl 並按 `uname -s` 選擇 `.so` / `.dylib`，均帶重試）
- `CopyHelperExecutables` / `PublishHelperExecutables` target（`AfterTargets=Build` / `Publish`）：將 native 庫與 AskPass/RI 子程序產物拷貝到 Build / Publish 輸出目錄
- `.gitignore` 已忽略 `third_party/` 下這些檔案

因此首次編譯需要網路存取 GitHub；CI 上由 workflow 明確下載並校驗（非空且 >1MB），csproj 的 `RestoreBiturbo` 作為兜底。

### tokei 來源

[tokei](https://github.com/XAMPPRocky/tokei)（MIT 授權）用於統計面板的「代碼行數」功能。建置期從 [hebin123456/tokei](https://github.com/hebin123456/tokei) 倉庫的最新 Release 拉取**預編譯二進位**，不再需要本地 Rust 工具鏈：

- Windows x64 → 裸 exe，存為 `third_party/tokei.exe`
- Linux x64 / macOS → tar.gz（內含裸 `tokei` 二進位），解壓存為 `third_party/tokei`
- macOS 資產為 x86_64，Apple Silicon 經 Rosetta 2 執行

機制與 biturbo 相同：`RestoreTokei` target（`BeforeTargets=Build`）自動拉取，CI 明確下載並校驗，`.gitignore` 忽略產物。

### 持續整合

專案配置了 GitHub Actions（[`.github/workflows/build.yml`](.github/workflows/build.yml)）：push / PR 到 `master` 分支或手動觸發時，在三平台並行建置並上傳產物：

| 矩陣 | Runner | RID |
|------|--------|-----|
| windows-x64 | windows-latest | win-x64 |
| linux-x64 | ubuntu-latest | linux-x64 |
| macos-arm64 | macos-latest | osx-arm64 |

產物為 **framework-dependent publish**（目標機需安裝 .NET 10 執行時），包含主程式、AskPass/RI 子程序三件套、對應平台的 biturbo native 庫、tokei 與語言檔案。可在倉庫 [Actions](https://github.com/hebin123456/ForkPlus-Next/actions) 頁面的對應執行中下載（Artifacts，保留 14 天）。

## 測試

- 單元測試：`dotnet test src/ForkPlus.Tests/ForkPlus.Tests.csproj`（含 Avalonia.Headless UI 冒煙與端到端測試，跨平台，隨單測一起執行）
- 全量 4000+ 用例；遷移過程中的關鍵修復均配有回歸防線（詳見 [MIGRATION.md](MIGRATION.md)）

## 多語言支援

### 內建語言

| 語言代碼 | 顯示名稱 | 狀態 |
|---------|---------|------|
| `en` | English | 來源語言 |
| `zh-Hans` | 简体中文 | 完整 |
| `zh-Hant` | 繁體中文 | 完整 |
| `ja-JP` | 日本語 | 完整 |
| `ko-KR` | 한국어 | 完整 |
| `fr-FR` | Français | 完整 |
| `de-DE` | Deutsch | 完整 |
| `es-ES` | Español | 完整 |

### 新增新語言

在 `src/ForkPlus/Languages/` 目錄下新建 `<語言代碼>.json` 檔案即可，無需修改程式碼。檔案格式：

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

詳見 [src/ForkPlus/Languages/README.md](src/ForkPlus/Languages/README.md)。

### 國際化 API

程式碼中透過以下 API 實現國際化：

- `PreferencesLocalization.Current("English text")` — 簡單字串翻譯
- `PreferencesLocalization.FormatCurrent("...{0}...", args)` — 帶參數的字串翻譯
- `PreferencesLocalization.Translate(text, language)` — 指定語言的翻譯

## 下載

- CI 建置產物：[Actions](https://github.com/hebin123456/ForkPlus-Next/actions) 頁面 → 對應 build 執行 → Artifacts（框架相依式，需 .NET 10 執行時）
- 正式發布版本：[Releases 頁面](https://github.com/hebin123456/ForkPlus-Next/releases)
- 各版本變更詳情請查閱 [Release Notes](RELEASE_NOTE.md)（含原 WPF 版歷史）

## 開發約定

- 修改應用程式本身時，保持在 `src/ForkPlus` 目錄內；`third_party/` 下的執行時二進位（biturbo native 庫、tokei）由建置期自動拉取，不要手動提交二進位檔案
- 如需升級 biturbo / tokei 版本，在對應倉庫發布新 Release 即可，本倉庫下次建置會自動拉取
- `../oxyplot-avalonia` 為倉庫外原始碼引用，保持官方原版零改動（誤改版本的教訓見 MIGRATION.md）
- 遷移工作（WPF → Avalonia）的環境配置、進行中事項與歷史修復鏈記錄在 [MIGRATION.md](MIGRATION.md)

## 授權條款

本專案基於 [MIT License](LICENSE) 開源。

Copyright (c) 2026 hebin123456
