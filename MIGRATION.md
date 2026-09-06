# ForkPlus-Next — WPF → Avalonia 迁移

> 本仓库是 [ForkPlus](https://github.com/hebin123456/ForkPlus)（WPF 版）向 Avalonia 12 的迁移目标。
> 历史修复链记录已按用户要求精简（2026-09-02），本文档只保留环境配置。

## 环境与构建（重要）

**📍 路径说明：仓库位于 `/data/user/work/ForkPlus-Next`**（沙盒重置后重新克隆的位置）。

**🚨 分支约定（2026-09-06 用户明确，务必遵守）**：
- 本仓库（ForkPlus-Next）**只使用 `master` 分支**，远程 `main` 分支已删除。
- 所有提交一律 `git push origin master`；**不要**新建/推送 `main` 分支（此前多个 agent 误把提交推到 `main`，已清理）。
- 若在仓库里看到 `origin/main` 残留引用：`git fetch --prune origin` 清掉即可。

```bash
# dotnet 不在默认 PATH，必须先 export（沙盒环境重置后 SDK 装在 ~/.dotnet）
export PATH="$HOME/.dotnet:$PATH"
export DOTNET_ROOT="$HOME/.dotnet"

# ── SDK 重装实录（2026-09-02 实测）──
# ① 中科大/南大镜像站无 dotnet 目录（404 实证），官方源直连仅 ~147KB/s；
#    正解 = aria2 16 连接切片下载，240MB 约 15 秒（先 apt-get update &&
#    apt-get install -y aria2 xvfb xdotool x11-utils，后三者为截图冒烟必备）：
aria2c -x 16 -s 16 -k 8M --file-allocation=none \
  -o dotnet-sdk-10.0.400-linux-x64.tar.gz \
  https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.400/dotnet-sdk-10.0.400-linux-x64.tar.gz
# ② SHA512 校验（对照官方 .sha512 文件）：
sha512sum dotnet-sdk-10.0.400-linux-x64.tar.gz
# ③ 解压安装：
mkdir -p ~/.dotnet && tar -xzf dotnet-sdk-10.0.400-linux-x64.tar.gz -C ~/.dotnet

# ── git 2.50.1 编译安装（2026-09-02 实测，必装）──
# Ubuntu 22.04 apt 的 git 是 2.34.1 < 推荐的 2.40（GitVersionChecker.RecommendedVersion），
# 应用每次启动都会弹"Git 版本过旧"对话框（账号窗口等交互前还得先点掉它）。
# 正解 = 源码编译 2.50.1（与 App.ForkGitInstancePath 期望的版本一致），装两处：
#   ① /usr/local/bin/git —— 系统级（shell 里也是新版）
#   ② ~/.local/share/ForkPlus/gitInstance/2.50.1/bin/git —— 软链到 ①，
#      即真实 Fork 的"自带 git 实例"，App.GitPath 首选路径（settings 里 GitInstancePath
#      为 null 时生效；若曾被写成 /usr/bin/git 需改回 null，否则仍用旧版 git）
apt-get update && apt-get install -y zlib1g-dev libssl-dev libcurl4-openssl-dev \
  libexpat1-dev gettext gcc make
cd /tmp && aria2c -x 16 -s 16 -k 1M --file-allocation=none \
  -o git-2.50.1.tar.xz https://www.kernel.org/pub/software/scm/git/git-2.50.1.tar.xz
tar -xf git-2.50.1.tar.xz && cd git-2.50.1
./configure --prefix=/usr/local --without-tcltk && make -j$(nproc) all && make install
GI="$HOME/.local/share/ForkPlus/gitInstance/2.50.1" && mkdir -p "$GI/bin" \
  && ln -sf /usr/local/bin/git "$GI/bin/git"
# 验证：/usr/local/bin/git --version → 2.50.1；$GI/bin/git --exec-path → /usr/local/libexec/git-core

# ── oxyplot-avalonia 是仓库外源码引用（csproj 的 ..\..\..\oxyplot-avalonia），沙盒重置即丢，必须重新克隆（与 build.yml 的 Clone 步骤同源）──
git clone --depth 1 https://github.com/oxyplot/oxyplot-avalonia.git /data/user/work/oxyplot-avalonia
# ⚠️ 教训（2026-09-03 实证）：克隆后直接 dotnet build，一行都不要改！
# 官方版 oxyplot 用 Avalonia 11.0.0 + netstandard2.0，与主工程（Avalonia 12.1.1 + net10.0）并存完全正常——
# 它自己按 11 编译成 netstandard2.0 程序集，主工程直接 ProjectReference 引用，NuGet 各按各的版本还原，互不冲突。
# 曾错误地把它当"版本不匹配"去改 AvaloniaVersion→12.1.1，结果：netstandard2.0 下大量类型解析失败（786 错），
# 又继续"救火"改 TFM→net10.0、剪贴板 API SetTextAsync→SetDataAsync、关编译绑定开关……越改越多、全部是白干。
# 正确姿势：官方原版直接编译即可通过（Build succeeded, 0 Error, ~21s），三方件保持零改动。

# ── git 身份（沙盒重置后需重新设置）──
git config user.name "Test User" && git config user.email "test@example.com"

# ── 环境就绪验证口径（2026-09-02 全部实证）：dotnet build 0 错误 + ForkPlus.Tests 全绿 ──
# 注意：曾偶发 "Test Run Aborted"（根因：Dispatcher.UIThread 是进程级单例、首触线程拥有，
# 并行测试集先触碰后 headless 启动线程初始化 Compositor 即崩；各测试类 SpinUntil 超时
# 后继续也会让 worker 抢先触碰）。已根治（2026-09-02）：HeadlessAppBootstrap 用
# [ModuleInitializer] 在程序集加载期启动真实 App 并同步等待就绪，归属恒为 UI 线程。

# 编译主工程（在 /data/user/work/ForkPlus-Next/src/ForkPlus 下）
dotnet build --no-restore -v q -nologo 2>&1 | grep -E "error CS" | sed -E 's/ \[.*//' | sort -u

# 编译整个解决方案（在 /data/user/work/ForkPlus-Next/src 下）
dotnet build ForkPlus.sln -clp:ErrorsOnly -nologo 2>&1 | tail -3

# 查看源生成器产物（调试 x:Name 字段问题时极其有用）
dotnet build --no-restore -v q -nologo -p:EmitCompilerGeneratedFiles=true
# 产物位于 obj/Debug/net10.0/generated/Avalonia.Generators/Avalonia.Generators.NameGenerator.AvaloniaNameIncrementalGenerator/
```

## GUI 调试与冒烟

**首选：headless 控件级自动化（快、准、带堆栈）**——in-process 驱动真实 App 资源，
异常堆栈直接进测试输出（截图+xdotool 坐标点击复现一次崩溃要几分钟，headless 秒级）。
原 Windows-only FlaUI/UIA3 套件 ForkPlus.AutomationTests 已删除（2026-09-02），
UI 冒烟测试全部归一到此处：

```bash
# 在 /data/user/work/ForkPlus-Next/src 下
dotnet test ForkPlus.Tests --filter "FullyQualifiedName~MenuWindowSmokeTests" -v q --nologo
```

启动基建已统一收拢到 `src/ForkPlus.Tests/HeadlessAppBootstrap.cs`：[ModuleInitializer]
在程序集加载期启动继承真实 `App` 的 headless 单例（全套 App.axaml 资源，只 override
掉启动副作用；`ShutdownMode=OnExplicitShutdown` 防 Dispatcher 连锁关闭），任何测试线程
不再与 Compositor 初始化竞争。新窗口/菜单冒烟直接 `[Collection("HeadlessAvalonia")]` +
`HeadlessAppBootstrap.Run(delegate { ... })`，参照 `UiSmokeHeadlessTests.cs` /
`MenuWindowSmokeTests.cs` 加测试即可。

**备选：Xvfb 真机截图冒烟**（最终视觉确认用）：

```bash
Xvfb :99 -screen 0 1920x1080x24 &   # 后台启动虚拟显示
export DISPLAY=:99
cd /data/user/work/ForkPlus-Next/src/ForkPlus && ./bin/Debug/net10.0/ForkPlus
import -window root /tmp/ui.png     # 截图（imagemagick）
xdotool mousemove X Y click 1       # 菜单点击（坐标靠截图测量，效率低，仅最终验证用）
```

# git 推送（凭据已配置在 remote url 中）
git push origin HEAD
```

## 提交列表（轨道树）性能审计（2026-09-03）

用户报告"轨道树没做缓存，内容多时很卡"，与 WPF 原版（github.com/hebin123456/ForkPlus）全面对比后的结论——**不是缓存问题，核心路径健康**：

- **数据层逐字节相同**：`GraphInfo.cs`/`GraphLine.cs`/`Git/RevisionVisualGraph.cs`/`Biturbo/CommitGraphCache.cs` 与 WPF 原版 diff 一致。原版**没有渲染缓存**（`GraphCellView.OnRender` 每次重建 StreamGeometry），靠 Freeze + retained 复合管线兜底；"原版有缓存"假设不成立。
- **列表虚拟化生效**：5000 项只实化 11 个容器（`ListViewWithGridViewStyle` 的 `ItemsPresenter ItemsPanel="{TemplateBinding ItemsPanel}"` 绑定有效，见 Listview.axaml 修复链21 注释）。
- **首帧 O(1)**：1000/5000/20000 行首帧均 ~12-14ms（首档 452ms 是冷 JIT/主题加载一次性成本，与行数无关）。
- **滚动健康**：40 屏连续滚动后实化仍 13 个容器（容器回收生效），headless 软渲染 ~10-20ms/屏。
- **尝试过的负优化（勿重蹈）**：按 `GraphInfo` 用 `ConditionalWeakTable` 缓存整行几何——单向滚动（主流场景）无复用机会（新行流过，几何构建本就是一次性必须成本），实测无收益（分配 40MB 不变、耗时持平偏慢），已回退。教训：WPF 原版不卡恰恰证明几何构建不是瓶颈，滚动成本大头在容器换绑+绑定重建（与 GraphCellView 无关）。
- **若用户仍报告卡顿**，需具体场景定位（仓库规模/操作步骤/卡在哪一步）；已知未对齐项：WPF GuidelineSet 像素对齐（视觉清晰度差异，非性能）。

审计沉淀的回归防线（防止虚拟化被后续改动破坏）：
- `RevisionListVirtualizationPerfTests`：5000 项实化容器 < 100（虚拟化失效即红）
- `RevisionListScrollPerfTests`：12-lane 辫子拓扑滚动 40 屏，实化容器不累积（回收失效即红）
- `RevisionListLoadPerfTests`：首帧耗时随行数 O(1)（5000→20000 行 4 倍裕量阈值，隐藏全量实化即红）

## AvaloniaEdit 空操作滚动 API：FileDiff 左右视图不同步（2026-09-03）

用户报告"FileDiff 视图左右代码原版同步上下滚动，我们是分离的"。根因**不在自家代码**——
`SideBySideTextDiffControl` 的同步逻辑与 WPF 原版逐行一致，而是三方件 AvaloniaEdit
（Avalonia.AvaloniaEdit 12.0.0）的 `TextEditor.ScrollToVerticalOffset/ScrollToHorizontalOffset`
是**空操作**（源码里滚动实现整段被注释，只剩 `ApplyTemplate()`；headless 实证：
调用后 offset=0）。兼容层 `ScrollViewerCompat` 当时误判"AvaloniaEdit 原生即有，直接转发"，
于是所有同步/恢复滚动的调用静默失效。

**正确滚动入口是模板 `PART_ScrollViewer`（本项目为 TouchpadAwareScrollViewer）的 `Offset`**，
与 AvaloniaEdit 自家 `ScrollTo(line,column)`、`TouchpadAwareScrollViewer` 滚轮路径一致：
Offset 变更 → ScrollContentPresenter 逻辑滚动订阅 → TextView → 触发 `ScrollOffsetChanged`。
两个坑（勿重蹈）：

- **别直接改 `TextView` 的 `IScrollable.Offset`**：TextView 的 setter 不调
  `RaiseScrollInvalidated`（只在 `SetScrollData`/`MakeVisible` 里调），外层 ScrollViewer.Offset
  会滞留旧值，用户下一次滚轮按旧值增量直接跳回旧位置（Avalonia issue #20484 同源问题）。
- **`TextEditor.ScrollViewer` 是 AvaloniaEdit internal**，拿不到；用
  `GetVisualDescendants().OfType<ScrollViewer>()` 按名字 `PART_ScrollViewer` 找。

一次性修复受益面（都走同一个兼容方法）：SideBySide 文本 diff 左右同步、
SideBySideCommitTextDiffControl（commit 视图）、HexDiffUserControl（十六进制 diff）、
`SplitTextDiffControl.ScrollToVerticalOffset`（BlameWindow 列表↔编辑器同步）、
`CodeEditor.SetScrollPosition`（切换文件恢复滚动位置——原来也一直是坏的）。

防回声守卫（WPF 原版同款，保留勿动）：`OnScrollOffsetChanged` 会抑制 100ms 内来自
另一侧编辑器的滚动事件——所以 headless 测试里反向滚动前要 `Thread.Sleep(150)` 模拟
真人换面板节奏，否则同步被守卫吞掉（这是原版行为，不是 bug）。

回归防线：`DiffScrollSyncTests`
- `ScrollToVerticalOffsetCompat_ScrollsEditorAndRaisesEvent`：兼容方法真滚动 + 触发事件
  （附诊断探针：原生 no-op 输出，不断言——上游哪天修了也不误报）
- `SideBySideTextDiffControl_ScrollSyncsLeftAndRight`：真实控件 300 行 diff，
  滚右→左跟随、滚左→右跟随（回归即红）

## 承诺式注释的坑："在文件资源管理器中显示"一直打开文档目录（2026-09-03）

用户报告"在文件资源管理器中显示，一直是打开文档目录，而不是打开需要打开的目录"。
根因是**迁移期删代码删出了回归**：WPF 原版 `ShowFileInFileExplorerCommand` 里
`Path.Combine(gitModule.Path, filePath).Replace("/", "\\")` 的 Replace 被删，注释写着
"Windows 分隔符交给 FileHelper 内部处理"——**但 FileHelper 从未实现这个处理**（空头承诺）。
链条：git 相对路径恒为正斜杠 → `Path.Combine` 后是混合分隔符（`C:\repo\src/App.cs`）→
.NET `File.Exists` 接受正斜杠（存在性守卫通过，掩盖了问题）→ `explorer.exe /select`
解析不了正斜杠路径 → Windows 忽略 `/select` 直接打开"文档"库（默认回退位置）。

教训：
- **删 WPF 原版代码时，注释里"XX 交给 YY 处理"的承诺必须当场兑现**，否则就是静默回归。
  本次修复把规范化收敛到 `FileHelper.BuildWindowsExplorerArguments`（Windows 分支专用；
  Unix 上反斜杠是合法文件名字符，绝不能全局替换）。
- **`.NET 的 File.Exists 接受正斜杠` ≠ `explorer.exe 接受正斜杠`**：.NET 走 Win32 API 会
  规范化分隔符，explorer.exe 自己的命令行解析不会——存在性守卫通过不等于下游工具能解析。
- explorer.exe 的失败模式（新版 Windows）：`/select` 目标不可解析 → 忽略 `/select` →
  打开"文档"库。FileHelper 历史注释记载的空格坑（`/select, "path"`）与本坑同源。
- Unix 分支的引号（`xdg-open \"path\"`）实测（net10.0）没问题：.NET 在 Unix 上会做
  shell 风格引号解析（剥引号、空格路径保持单参数），**勿"顺手修复"**。

回归防线：`FileExplorerRevealTests`（Linux CI 无法执行 Windows 分支，故抽出纯函数
`BuildWindowsExplorerArguments` 守卫参数构造契约）：混合分隔符规范化、纯反斜杠幂等、
目录无 `/select`、逗号后无空格、中文+空格路径带引号、深层路径全量转换（回归即红）。

## 横向滚动条只画出 13px 小方块（2026-09-03）

用户报告"上下滚动的滚动条没问题，左右滚动的滚动条绘制得有问题"。根因在
`Theme/Styles/Scrollviewer.axaml` 的 ScrollBar 主题，**两个迁移丢失**叠加：

1. **`Width="Auto"` 重置丢失（主因）**：WPF 原版基础样式设 `Width="13"`（纵向正确），
   `:horizontal` 触发器第一个 Setter 就是 `Width="Auto"` 把它重置掉——迁移时只搬了
   MinWidth/Height，丢了 Width 重置 → 横向滚动条被硬约束成 13×13 小方块（track 列
   13-20px 宽度算成负 → thumb 不可见）。Avalonia 里 Width 是 double，**NaN 即 WPF 的
   Auto**：`<Setter Property="Width" Value="NaN" />`。
2. **Track 未绑 Orientation（次因，修主因后显现）**：WPF 的 Track 没有 Orientation
   属性、按自身宽高比自动推断方向；Avalonia 的 `Track.Orientation` 经
   `ScrollBar.OrientationProperty.AddOwner` 共享**默认值 Vertical**，且 ScrollBar 不会
   同步给模板里的 Track → thumb 按纵向语义排列（宽度铺满全 track、value 变化沿 Y 移动）。
   官方 Fluent 主题在 Track 上显式绑 `Orientation="{TemplateBinding Orientation}"`。

教训：
- WPF 样式触发器迁移到 Avalonia 伪类样式时，**逐个 Setter 对账**——尤其"重置型"
  Setter（Auto/NaN）最容易被当作"没用的重复"丢掉。
- WPF/Avalonia 的 Track 行为差异：WPF 按几何推断方向，Avalonia 必须显式设置
  Orientation（默认纵向）。
- Avalonia 官方 ScrollBar 主题**从不设置 `Width`**（只用 MinWidth/MinHeight），
  方向差异交给 `:horizontal`/`:vertical` 分支模板布局。

回归防线：`HorizontalScrollBarRenderingTests`（headless 实测布局）：
- `HorizontalScrollBar_SpansViewportWidth`：横向条宽 > 300（Width 约束未重置即红）+
  纵向条仍 13px（防过度修复，双向守护）
- `HorizontalScrollBar_ThumbLaysOutHorizontally`：thumb 宽 ≈ track×视口比例（< 50% track）、
  offset 增加后 thumb 沿 X 右移（Track 方向错即红）

## Resources[key] 索引器不穿透合并字典：自定义颜色窗口全白（2026-09-03）

用户报告"自定义颜色窗口打开没有加载当前的颜色，显示全是 #FFFFFF"。根因：
`CustomColorsDialog.GetCurrentColorHex` 沿用 WPF 写法
`Application.Current.Resources[key]`——**两框架的索引器语义不同**：

- WPF 的 `ResourceDictionary[key]` 会先查顶层、再逆序穿透 `MergedDictionaries`
  （App 合并的 `Generic.{Skin}.axaml` 主题色全能取到）；
- Avalonia 的索引器**只查本字典自身条目**（headless 探针实测：`BackgroundColor`
  明明在合并字典里，`Resources["BackgroundColor"]` 返回 null）→ 30 个颜色 key
  全部命中不了 → 全走 fallback `"#FFFFFF"`。

修复：改用 `ResourceCompat.TryFindResource(Application.Current, key)`（底层
`Resources.TryGetResource`，与 WPF 索引器同语义：先顶层、再逆序穿透合并字典——
末尾 merge 的自定义颜色覆盖字典优先命中，主题原色与用户覆盖色都能取到）。
同对话框 `InitializeSwatches` 的 `BorderBrush` 取值同根因（null → 30 个预设色块
无描边），一并改掉。

教训：迁移后**审计所有 `Resources[key]` 直接索引**（含 `ResourceDictionary` 实例），
凡可能命中合并字典/主题字典的，一律换 `TryFindResource`/`TryGetResource` 链式查找。
索引器只适合"确定写在顶层字典"的场景。

附带修复（同轮发现）：`ToolbarUserControl` 在 ctor 里
`WeakEventManager` 订阅了 `ActiveTabChanged`/`ApplicationThemeChanged`，但
`_mainWindow` 要到 `MainWindow` 构造中 `Toolbar.Initialize(this)` 才赋值、
`TabManager` 更晚——初始化完成前收到事件即 NRE（测试直接 `new` 裸 toolbar 会
触发）。`RefreshToolbar`/`InitializeAppearanceToolBarButtonContextMenu` 加
`_mainWindow?.TabManager == null` 早退守卫。

回归防线：`CustomColorsDialogTests`：
- `DialogLoadsCurrentThemeColors_NotAllWhite`：清空自定义色后打开对话框，
  30 项取到主题当前色（AccentColor=#007ACC 等），非全 #FFFFFF
- `SwatchesGetBorderBrush_FromMergedDictionary`：30 个预设色块 BorderBrush 非空
- `ResourceLookup_LastMergedDictionaryWins`：末尾合并字典优先命中（自定义覆盖
  语义），移除后回落主题原色


- 工作目录：`/data/user/work/ForkPlus-Next`（主仓库）、`/data/user/work/oxyplot-avalonia`（图表库源码，仓库外引用）
- 进度截图统一放 `verification/`（仓根），有进展及时提交推送，不攒批
- 构建产物不入库（bin/obj 已在 .gitignore；publish/ 已于 2026-09-02 清除，CI 产物走 release artifact）
