# ForkPlus-Next

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Build](https://github.com/hebin123456/ForkPlus-Next/actions/workflows/build.yml/badge.svg)](https://github.com/hebin123456/ForkPlus-Next/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/hebin123456/ForkPlus-Next)](https://github.com/hebin123456/ForkPlus-Next/releases)

[ForkPlus](https://github.com/hebin123456/ForkPlus)（WPF 版）的跨平台迁移版本：UI 层基于 .NET 10 + Avalonia 12 重写，一套代码运行于 Windows / Linux / macOS。底层 Rust 引擎（biturbo native）、AI 辅助开发、8 种语言、12 套主题皮肤、git mm 工作流，以及贡献热力图、仓库树图等可视化能力与原版保持一致。

> 迁移环境搭建、历史修复链与经验教训记录在 [MIGRATION.md](MIGRATION.md)。

[English](README.en.md) | [简体中文](README.md) | [繁體中文](README.zh-Hant.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Français](README.fr.md) | [Deutsch](README.de.md) | [Español](README.es.md)

## 主要特性

- **跨平台**：基于 Avalonia 12 的跨平台 UI 层，CI 同时产出 Windows x64 / Linux x64 / macOS arm64 三平台构建
- **多语言支持**：内置英语、简体中文、繁體中文、日本語、한국어、Français、Deutsch、Español 8 种语言，并支持通过 JSON 文件扩展更多语言
- **多主题皮肤**：内置 12 套预设皮肤（Light/Dark、Solarized、GitHub、Dracula、Monokai、紫色/绿色浅色深色），并支持用户自定义颜色覆盖，即时生效
- **git mm 工作流**：内置 `git mm` 子命令，提供精益分支（Lean Branching）工作流，统一管理多子仓的变更与同步
- **AI 辅助开发**：集成 AI 代码审查、自动生成提交信息、AI 辅助修改代码
- **贡献热力图**：GitHub 风格 53 周 × 7 天提交热力图，附带色阶图例和统计摘要（总提交数 / 最长连续提交天数 / 最活跃日期），鼠标悬停显示当日提交数和 Top 3 作者
- **仓库树图**：基于 biturbo native treemap 算法的仓库文件大小可视化，支持逐级钻取点击
- **远端分支跟踪**：右键"跟踪"改为按远端分组的二级菜单，菜单内嵌置顶搜索框，支持大量远端分支快速检索
- **性能优化**：针对大型仓库的刷新、diff 渲染、子模块管理做了专项优化
- **代码统计**：集成 tokei（Rust 编写，支持 200+ 语言），按语言统计代码行数、文件数、注释行、空白行，饼图可视化，支持 Workspace/分支/tag 切换 ref

## 项目结构

```
ForkPlus-Next/
├── src/
│   ├── ForkPlus/              # 主应用程序源码（Avalonia 12 跨平台 UI）、XAML、资源
│   │   ├── Biturbo/           # biturbo native 三方件的 P/Invoke 绑定
│   │   ├── Languages/         # 多语言翻译文件（JSON）
│   │   │   ├── zh-Hans.json   # 简体中文
│   │   │   ├── zh-Hant.json   # 繁體中文
│   │   │   ├── ja-JP.json     # 日本語
│   │   │   ├── ko-KR.json     # 한국어
│   │   │   ├── fr-FR.json     # Français
│   │   │   ├── de-DE.json     # Deutsch
│   │   │   ├── es-ES.json     # Español
│   │   │   └── README.md      # 语言文件格式说明
│   │   └── ...
│   ├── ForkPlus.AskPass/      # Git/SSH 密码输入辅助程序
│   ├── ForkPlus.RI/           # 交互式 rebase 编辑器辅助程序
│   ├── ForkPlus.Tests/        # xUnit 单元测试（含 Avalonia.Headless UI 冒烟测试）
│   ├── ForkPlus.AskPass.Tests/# AskPass 辅助程序单元测试
│   └── ForkPlus.RI.Tests/     # RI 辅助程序单元测试
├── third_party/               # 构建期拉取的原生二进制（见下文「biturbo native 库来源」）
├── gitmm/                     # git mm 工作流参考文档
└── .github/workflows/         # GitHub Actions CI 配置
```

## 编译

### 环境要求

- Windows 10 或更高版本 / Linux / macOS（跨平台支持）
- .NET 10 SDK
- IDE（可选）：Visual Studio 2026（Windows，可用仓库根目录的 `OpenForkPlusInVS2026.cmd` 一键打开解决方案）、Rider 或 VS Code
- Git 2.40 或更高版本（推荐；低于推荐版本启动时会警告，部分功能可能异常。应用优先使用的内置 git 实例版本为 2.50.1，缺失时回退系统 git）
- git-mm 3.0 或更高版本（使用 git mm 工作流时必需，低于此版本启动时会警告；未安装时 git mm 工作区功能不可用，可在偏好设置中配置 git-mm 路径）

### 编译步骤

```bash
# ① 图表库 OxyPlot.Avalonia 以"仓库外源码引用"方式集成（csproj 相对路径指向仓库同级目录），
#    首次编译前先克隆到本仓库旁边。保持官方原版零改动，勿修改其 Avalonia 版本：
git clone --depth 1 https://github.com/oxyplot/oxyplot-avalonia.git ../oxyplot-avalonia

# ② 编译（仓库根目录）：
dotnet build ForkPlus.sln -c Release
```

也可用 Visual Studio 2026 直接打开仓库根目录的 `ForkPlus.sln` 编译。

### biturbo native 库来源

biturbo native 三方件（Rust）提供仓库树图布局、提交图缓存、revision header 解析等能力。**该文件不以二进制形式提交到本仓库**，而是在构建期自动从 [Biturbo 仓库](https://github.com/hebin123456/Biturbo) 的最新 Release 拉取，按平台选择文件：

| 平台 | 文件 |
|------|------|
| Windows x64 | `third_party/biturbo.dll` |
| Linux x64 | `third_party/libbiturbo.so` |
| macOS arm64 | `third_party/libbiturbo.dylib` |

具体机制（见 [ForkPlus.csproj](src/ForkPlus/ForkPlus.csproj)）：

- `RestoreBiturbo` target（`BeforeTargets=Build`）：检测到当前平台的 native 库缺失时自动下载（Windows 走 PowerShell，Linux/macOS 走 bash + curl 并按 `uname -s` 选择 `.so` / `.dylib`，均带重试）
- `CopyHelperExecutables` / `PublishHelperExecutables` target（`AfterTargets=Build` / `Publish`）：将 native 库与 AskPass/RI 子进程产物拷贝到 Build / Publish 输出目录
- `.gitignore` 已忽略 `third_party/` 下这些文件

因此首次编译需要网络访问 GitHub；CI 上由 workflow 显式下载并校验（非空且 >1MB），csproj 的 `RestoreBiturbo` 作为兜底。

### tokei 来源

[tokei](https://github.com/XAMPPRocky/tokei)（MIT 协议）用于统计面板的"代码行数"功能。构建期从 [hebin123456/tokei](https://github.com/hebin123456/tokei) 仓库的最新 Release 拉取**预编译二进制**，不再需要本地 Rust 工具链：

- Windows x64 → 裸 exe，存为 `third_party/tokei.exe`
- Linux x64 / macOS → tar.gz（内含裸 `tokei` 二进制），解压存为 `third_party/tokei`
- macOS 资产为 x86_64，Apple Silicon 经 Rosetta 2 运行

机制与 biturbo 相同：`RestoreTokei` target（`BeforeTargets=Build`）自动拉取，CI 显式下载并校验，`.gitignore` 忽略产物。

### 持续集成

项目配置了 GitHub Actions（[`.github/workflows/build.yml`](.github/workflows/build.yml)）：push / PR 到 `master` 分支或手动触发时，在三平台并行构建并上传产物：

| 矩阵 | Runner | RID |
|------|--------|-----|
| windows-x64 | windows-latest | win-x64 |
| linux-x64 | ubuntu-latest | linux-x64 |
| macos-arm64 | macos-latest | osx-arm64 |

产物为 **framework-dependent publish**（目标机需安装 .NET 10 运行时），包含主程序、AskPass/RI 子进程三件套、对应平台的 biturbo native 库、tokei 与语言文件。可在仓库 [Actions](https://github.com/hebin123456/ForkPlus-Next/actions) 页面的对应运行中下载（Artifacts，保留 14 天）。

## 测试

- 单元测试：`dotnet test src/ForkPlus.Tests/ForkPlus.Tests.csproj`（含 Avalonia.Headless UI 冒烟与端到端测试，跨平台，随单测一起运行）
- 全量 4000+ 用例；迁移过程中的关键修复均配有回归防线（详见 [MIGRATION.md](MIGRATION.md)）

## 多语言支持

### 内置语言

| 语言代码 | 显示名称 | 状态 |
|---------|---------|------|
| `en` | English | 源语言 |
| `zh-Hans` | 简体中文 | 完整 |
| `zh-Hant` | 繁體中文 | 完整 |
| `ja-JP` | 日本語 | 完整 |
| `ko-KR` | 한국어 | 完整 |
| `fr-FR` | Français | 完整 |
| `de-DE` | Deutsch | 完整 |
| `es-ES` | Español | 完整 |

### 添加新语言

在 `src/ForkPlus/Languages/` 目录下新建 `<语言代码>.json` 文件即可，无需修改代码。文件格式：

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

详见 [src/ForkPlus/Languages/README.md](src/ForkPlus/Languages/README.md)。

### 国际化 API

代码中通过以下 API 实现国际化：

- `PreferencesLocalization.Current("English text")` — 简单字符串翻译
- `PreferencesLocalization.FormatCurrent("...{0}...", args)` — 带参数的字符串翻译
- `PreferencesLocalization.Translate(text, language)` — 指定语言的翻译

## 下载

- CI 构建产物：[Actions](https://github.com/hebin123456/ForkPlus-Next/actions) 页面 → 对应 build 运行 → Artifacts（框架依赖式，需 .NET 10 运行时）
- 正式发布版本：[Releases 页面](https://github.com/hebin123456/ForkPlus-Next/releases)
- 各版本变更详情请查阅 [Release Notes](RELEASE_NOTE.md)（含原 WPF 版历史）

## 开发约定

- 修改应用程序本身时，保持在 `src/ForkPlus` 目录内；`third_party/` 下的运行时二进制（biturbo native 库、tokei）由构建期自动拉取，不要手动提交二进制文件
- 如需升级 biturbo / tokei 版本，在对应仓库发布新 Release 即可，本仓库下次构建会自动拉取
- `../oxyplot-avalonia` 为仓库外源码引用，保持官方原版零改动（误改版本的教训见 MIGRATION.md）
- 迁移工作（WPF → Avalonia）的环境配置、进行中事项与历史修复链记录在 [MIGRATION.md](MIGRATION.md)

## 许可证

本项目基于 [MIT License](LICENSE) 开源。

Copyright (c) 2026 hebin123456
