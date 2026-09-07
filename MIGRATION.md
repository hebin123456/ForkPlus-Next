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

# ── gitflow-avh 安装（2026-09-06 实测，模块17 测试起必装）──
# E2E 模块17 的 GitFlow 用例（flow init/start/finish）依赖 git-flow 子命令，纯净 git 没有。
# 源码安装 gitflow-avh 到 ~/.local/bin（已写入 ~/.bashrc）：
cd /tmp && git clone --depth 1 https://github.com/petervanderdoes/gitflow-avh.git
cd gitflow-avh && PREFIX="$HOME/.local" make install
# 验证：export PATH="$HOME/.local/bin:$PATH" && git flow version → 1.12.4-dev0 (AVH Edition)
# ⚠️ 注意：git 查找 git-flow 走 PATH 而非 exec-path——exec-path 方案无效（2026-09-06 探针实证），
# 跑测试的 shell 必须保证 ~/.local/bin 在 PATH 里（bashrc 只对交互 shell 生效）。

# ── oxyplot-avalonia：已改为消费 fork 的预编译 nupkg（2026-09-06），不再克隆源码、不再打补丁 ──
# 现状：PackageReference 引 hebin123456/oxyplot-avalonia fork release 的
# OxyPlot.Avalonia.2.1.2-avalonia12.1.nupkg（按 Avalonia 12.1.1 原生编译，net8.0/net10.0），
# 构建期自动下载到 third_party/nuget/（nuget.config 本地目录源；本地走 csproj 的
# RestoreOxyPlotAvalonia target，CI 走 build.yml 显式下载步骤）。沙盒重置后无需任何手动操作，
# dotnet restore/build 自动拉取（仅需网络可达 GitHub）。
#
# 历史教训（为什么必须 fork 发包，保留备查）：
# 根因（运行时实锤，非编译期）：官方版按 Avalonia 11.0.0 编译的 XAML IL 调用 TemplateBinding.ProvideValue()
# （11 时代签名），运行时 12.1.1 无此方法 → 任何 PlotView 进入布局/模板实例化即 MissingMethodException
# （布局期异常穿透渲染循环，应用级崩溃）。2026-09-03 的"按 11 编译并存正常"结论只验证了编译通过
# （当时无任何用例真正渲染过图表）；直到模块20 统计窗口首个 PlotView 布局用例才暴露。
# 曾在 2026-09-03 走对过一半（改版本+TFM+剪贴板 API+关编译绑定）但因"编译能过就零改动"的错误结论回退；
# 2026-09-06 起固化为 fork 仓库（hebin123456/oxyplot-avalonia）打包发版，本仓库零补丁消费。
# 补丁的具体内容（3 处）已并入 fork：Directory.Build.props AvaloniaVersion 11.0.0→12.1.1；csproj TFM
# netstandard2.0→net8.0;net10.0 + AvaloniaUseCompiledBindingsByDefault=false；PlotBase.cs 加 using
# Avalonia.Input.Platform（SetTextAsync 在 Avalonia 12 变为 ClipboardExtensions 扩展方法）。
# 注意：netstandard2.0 TFM 下直接升 AvaloniaVersion 会 786 个类型解析错误（Avalonia 12 无 netstandard2.0 资产），
# 必须同步改 TFM；其余 API 断点只有剪贴板一处。

# 原 docs/patches/ 下两个补丁文件（oxyplot-avalonia-avalonia12.patch / ci-build-oxyplot-apply.patch）
# 已随本变更删除（fork 已内置等效改动）。中间曾短暂用过"build.yml clone 后直接 apply 补丁"
# 方案（commit e846ec1），已被 fork 发包方案取代。
# ── CI 二次更新（2026-09-06，self-contained + 全量测试进 workflow）──
# ① 产物改 self-contained（主程序 + AskPass/RI 三 exe 同目录自包含发布，目标机免装 .NET 10；
#   helper 由 git 独立进程拉起，不自带运行时必挂——沙盒实证：runtimeconfig 含 includedFrameworks、
#   libcoreclr/libhostpolicy 就位、env -i 下 helper 正常走业务退出码、xvfb 下主程序存活）；
# ② 新增 test job（ubuntu，dotnet test ForkPlus.sln 全量 ~4300 用例；OxyPlot nupkg 与 build
#   job 同源下载到 third_party/nuget/，需装 gitflow-avh（模块17）+ git-lfs（模块18，runner 预装））；
# ③ AskPass/RI.Tests 的 ExePath 修了平台命名（原硬编码 .exe，Linux 全挂）。
# PAT workflow scope：2026-09-06 早先记录"无 workflow scope 推不动 build.yml"，当日稍后
# 实证推送成功（e846ec1/611a245/fdab343 均含 build.yml 改动）——当前凭据可直接推；若遇
# "refusing to allow a Personal Access Token to create or update workflow" 拒绝，换带
# workflow scope 的 PAT 即可。

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

## 命令行可用、GUI 报 git: 'mm' is not a git command（2026-09-07）

用户报告：linux 上 git-mm 已装好，命令行可以运行 `git mm sync` 之类命令，界面里反而报
`git: 'mm' is not a git command`。沙盒造场景实证（fake git-mm 脚本装进系统 git 的
git-core 目录，GUI 发起 `git mm version`）：复现成功。

根因：git 查找自定义子命令（`git-foo`）沿"**自身 exec-path + 进程 PATH**"两路，而
GUI 与命令行在这两处都可能不同：
1. **exec-path 不同**：GUI 优先用自带 git 实例（`gitInstance/2.50.1`，exec-path 指向
   自己的 git-core），企业 git-mm 常装在**系统 git** 的 git-core 目录——只有系统 git
   （命令行）找得到；
2. **PATH 不同**：桌面启动的 GUI 进程 PATH 可能缺 `~/.local/bin` 等用户 bin（这些只
   在 shell rc 里追加）——命令行 git 找得到，GUI 的 git 找不到。

任一处差异都造成"命令行可用、GUI not a git command"。另有一个叠加放大器：原版
`App.GitMmPath` 硬编码 `git-mm.exe`，Unix 上根本无此文件名——即使 git-mm 在 GUI 进程
PATH 里，ForkPlus 侧也解析不到（与 git-ai 的跨平台命名同款坑）。

修复（三层：`App.axaml.cs` / `GitRequest.cs` / `GitUserControl.axaml.cs`）：
1. **跨平台可执行名**：`App.GitMmExecutableName`（Windows `git-mm.exe`、Unix
   `git-mm`），PATH 查找/同目录探测/偏好设置列表全部改用；
2. **系统位置兜底探测**：`App.GitMmPathFromSystemLocations`（带缓存）= PATH 中各 git
   的 `--exec-path`（企业 git-mm 常见安装位）+ 用户 bin（`~/.local/bin`、`~/bin`），
   作为 `GitMmPath` 解析链最后一步；偏好设置 Git 实例下拉同源列出（标注来源路径）；
3. **git 子进程 PATH 注入**：`App.PrependGitMmDirectoryToPath`，`GitRequest` 两条执行
   路径（Process StartInfo / Bt env 数组）把 git-mm 所在目录前置进子进程 PATH——封堵
   "GUI 用哪个 git 实例 + 进程 PATH 初始如何"的所有组合。幂等（目录已在 PATH 不注入）；
   git 执行 git-foo 时自身 exec-path 前置优先级更高，自带实例的既有命令不受影响；
   每次 git 请求都过（热路径），全走缓存/字符串操作，无子进程开销。

教训：
- WPF 原版硬编码 `git-mm.exe` 是 Windows-only 假设；跨平台审计除了路径拼接，
  "可执行文件名字符串"同样要过一遍（`SystemEnvironment.GitExecutableName` 模式）。
- GUI 与命令行的差异不止 git 本身：**git 子命令查找 = 自身 exec-path + 进程 PATH**，
  两处都要对账。桌面进程的 PATH 是登录会话的子集，凡"shell 里装的工具"（git 扩展、
  CLI hook）都可能在 GUI 里找不到——依赖外部工具要么全路径解析，要么显式注入 PATH。
- 2026-09-06 的 gitflow-avh 探针已实证"exec-path 方案无效（git 找 git-flow 走 PATH）"；
  本问题同源——**注入 PATH 才是通用解**，第 3 层即为这类外部子命令兜底。

回归防线：`GitMmSubcommandPathTests`（5 用例）：
- `GitMmExecutableName_IsPlatformCorrect`：平台可执行名正确
- `PrependGitMmDirectoryToPath_InjectsWhenDirMissing` / `IdempotentWhenDirPresent` /
  `EmptyPathGetsDirOnly`：注入/幂等/空 PATH（段级比较，防目录名互为子串误判）
- `GitRequest_Execute_GitMm_FindsExecutableOutsidePath`：端到端——fake git-mm 装在
  测试进程 PATH 之外的目录，`GitRequest("mm","version")` 经注入后真实执行成功
  （修复前红：git: 'mm' is not a git command）

⚠️ 测试环境残留坑（2026-09-07 实录）：造场景时装进 `/usr/lib/git-core/git-mm`
（系统 git 2.34 的 exec-path）的 fake 会打破"沙箱无 git-mm CLI"前提——
`GitMmWorkspace_TabOpensAndWarnsMissingCli`（E2e17）期望 missing 警告弹窗，探测层
找到系统位置的 git-mm 后不再弹（恰是修复生效的证据，但断言前提被污染）。跑测试前
`rm -f /usr/lib/git-core/git-mm` 清残留。同类坑（手工造场景用 fake 跑过真实
`git mm init` 后）：workspace 会写进 settings 的 `GitMm.Workspaces` 且目标目录留有
`.mm`——`RepositoryManagerUserControl.Refresh/ctor` 的 `ImportKnownGitMmWorkspaces`
按存在性把这些目录重新导入仓库列表，让 E2e01（空态视图）/E2e27（单仓库选中末项）
凭空多出条目而红。清理：删掉残留目录 + 从 `~/.local/share/ForkPlus/settings.json`
的 `GitMm.Workspaces` 移除 fpe2e_ 条目（提交的测试套自身不会造成此污染：守卫用例被
MessageBox 拦截不真跑 init，临时 ws 用例 finally 删目录、残留条目被存在性过滤，
CI 新沙箱每轮干净）。GUI 冒烟取证（修复后工作区 tab 无警告 + `git mm sync` 成功）：
`verification/gitmm-fix-workspace-cli-found.png`。

## 复制按钮缺 x:Name：测试引用拿不到控件、HEAD 编译不过（2026-09-07）

上轮 a240bcd"四弹窗补复制按钮"提交的 XAML 里 Button 只挂了 `Click` 事件没给
`x:Name`，而 E2e17 测试已按强类型属性 `dialog.CommandPreviewCopyButton`（Init 弹窗）
与可视树 `b.Name == "CommandPreviewCopyButton"`（Start/Sync/Upload）断言——Avalonia
的 x:Name 才生成控件字段，缺失即 CS1061，HEAD 处于编译不过的中间态。
补 `x:Name="CommandPreviewCopyButton"`（四弹窗），编译恢复、E2e17 全绿。
教训：**XAML 控件补丁与引用它的测试必须同一提交**——x:Name/事件双改点（Click 与
Name）一个都不能漏，否则仓库在任何 checkout 点都无法构建。

## CI 红灯：清单缺登记 + 手势测试在 CI 环境不稳定（2026-09-07）

test job（ubuntu）4 项红，两类根因：

1. **`SourceFileCoverageManifestTests` / `ClassCoverageManifestTests` 各 1 红**：
   1b43544（fsmonitor 修复）新增 `src/ForkPlus/Git/Commands/ReliableGitFlags.cs`
   未登记进两个 coverage manifest（本仓纪律：生产源文件/类型必须注册，且每个类型
   至少 1 个 AutomatedCase）。按字母序补两处条目。顺带清理 manifest 里的重复条目
   （`Coder` 5 份 / `Keys` 3 份 / `ServiceResult` 2 份——同名嵌套类每声明一处曾被
   重复登记，xUnit theory 生成 duplicate case ID 告警）。去重教训：**不能
   `awk '!seen[$0]++'` 全局去重**——清单里 `},` 等结构行同内容不同位置会误删，
   直接把文件结构打坏（CS1514）；必须只对 `new ClassCoverageEntry(` 行去重。
2. **`E2e05bDoubleClickStageTests` 2 红**：真实指针手势序列（GetPosition 模拟链）
   的双击 stage 测试。本地全绿、CI 红且"双击后列表完全无变化"（手势识别未触发），
   fsmonitor 变体在本地也暴露 `UiClick.WaitFor` 条件对 `Items == null`（状态装配
   未完成）解引用 NRE 的测试自身缺陷。stage 功能本身已被 `ReliableGitFlags` 修复
   （`E2e05` 合成事件测试覆盖），真实手势序列测试属多余且跨环境不稳定——整文件
   删除（用户拍板）。

## 凭据管理器三档记忆：自动记账号 / 记住密码 / 记住密码+不再弹出（2026-09-07，语义修正版）

设计见 `docs/credential-popup-unification.md` Layer D 一节（在 Layer A/B/C 之后）。
三档语义（用户 2026-09-07 拍板）：
- 第一档：**自动记住上次输入的账号**——默认行为，无勾选框；Username 弹窗预填；
- 第二档："记住密码"（勾选）——密码落盘，**下次弹窗仍出现但密码框自动预填**；
- 第三档："记住密码 + 不再弹出"（勾选）——credential get（App IPC 主路径）与
  askpass（`ShowAskPassWindowCommand` 兜底）全链路静默回填，完全不弹窗；凭据
  失效被 erase 后快速失败；偏好设置 > Credentials 的"不再弹出"ToggleSwitch 可
  随时重新打开（`SetNeverAsk`），也可提前录入账号密码并设置不再弹出（`Upsert`）。
`SavedCredentialStore`（credentials.json，跨平台）补齐 Layer C 在 Linux/macOS
的持久化空操作（第三档静默命中查询 = `TryGetSilentCredential`，仅 password +
NeverAskAgain 皆备）；erase 联动 `ForgetPassword` 防旧密码死循环。测试隔离用
`SwapForTests`（internal，InternalsVisibleTo 已有），36 项专项测试
（`SavedCredentialStoreTests` + `CredentialsRememberUiTests`）。

## git-ai stats 报 git: 'stats' is not a git command——argv[0] 代理模式（2026-09-07）

用户报告（截图）：统计页执行 `git-ai stats 'fe8e3ea..HEAD' --json`，stderr 报
`git: 'stats' is not a git command. See 'git --help'.`。沙盒以 git-ai 1.7.2 实证复现：
同一二进制复制为非标准名（`git-ai-renamed`）调用 `stats` → 原样报错；以标准名
`git-ai` 调用 → 正常输出统计（复现/对照双证）。

根因：**git-ai 二进制按 argv[0] 的文件名分发**——文件名为 `git-ai`（Windows 为
`git-ai.exe`）才走原生命令分支（stats/diff/blame/checkpoint），其他任何文件名
（手动下载的 `git-ai-linux-x64`、带版本号的副本、改名安装）一律进入 **git 透明代理
模式**，把参数原样转发给真 git：`git-ai stats ...` 变成 `git stats ...` → 报 not a
git command。非标准名调 `--version` 输出 "git version 2.50.1"（真 git 的版本串）
即代理铁证。这与 git-mm 的"路径可见性"问题同症状不同根因——git-mm 是**找不到**
可执行文件，git-ai 是**找到了但执行名不对**。

修复（三防线：`App.axaml.cs` / `GitAiVersionChecker.cs` / `GetGitAiStatsGitCommand.cs`）：
1. **staging 符号链接（核心根治）**：`App.EnsureGitAiExecutionPath`——`GitAiPath`
   解析结果文件名非标准时，在 ForkPlus 数据目录 `git-ai-staging/` 下建标准名符号
   链接指向原文件，返回链接路径执行（argv[0] basename = git-ai → 原生命令分支）。
   幂等（目标未变且链接有效零 IO 复用；悬空/被删自动重建）；建链失败（Windows 无
   符号链接权限）降级返回原路径，不劣于修复前。`GitAiResolvedPath` 保留解析链
   原样结果供偏好设置 UI 匹配（staging 链接路径对用户无意义）；
2. **系统位置兜底探测**：`App.GitAiPathFromSystemLocations`（解析链最后一步）——
   git-ai 官方 install.sh 装到 `~/.git-ai/bin` 且只把 PATH 写进 shell rc，桌面启动
   的 GUI 进程两处都看不到；按"命令行会看到什么"的口径再探一轮（各 git 的
   exec-path / 用户 bin / 用户 shell 环境，与 git-mm 2026-09-07 修复同模式）；
3. **代理症状识别与翻译**：`GitAiVersionChecker.LooksLikeGitProxyVersionOutput`
   （代理模式 `--version` 输出 "git version x.y.z"，不识别会解析成假版本 ≥1.0.0、
   检查通过，掩盖配置问题）+ `GetGitAiStatsGitCommand.IsGitProxyForwardingSymptom`
   （staging 降级的残余场景把 git 原始报错翻译成可定位的提示，不再一脸懵）。

教训：
- **argv[0] 分发是二进制工具的常见设计**（busybox 同款）：可执行文件名即子命令选择器，
  跨平台迁移凡是"复制/链接/重命名后调用外部工具"的路径都要核对最终 argv[0]。
- `File.CreateSymbolicLink` 在 Windows 需开发者模式/管理员权限——所有建链逻辑必须
  有 try-catch 降级路径，不能假设建链必然成功。
- 症状相同的两个 bug（git-mm 找不到 vs git-ai 执行名不对）根因可能完全不同：
  "is not a git command"在 git 生态里至少有三种成因（子命令不在 exec-path/PATH、
  被代理转发、拼写错误），修复前先沙盒复现定位根因层。

回归防线：`GitAiSubcommandPathTests`（12 用例）：
- staging 行为五件套：标准名直通（不建目录）/ 空目标原样返回 / 非标准名建标准名链接
  （LinkTarget 指向原文件）/ 幂等（marker 文件占位证明零 IO 复用）/ 目标变更重定向 +
  链接被删自动重建
- 代理识别：`LooksLikeGitProxyVersionOutput` 形态判别（"git version" true、"1.7.2"/
  "git-ai version" false）/ fake 代理二进制 `GetVersion` 返回 null（修复前解析出
  假版本 2.50.1）/ fake 原生二进制正常解析 1.7.2
- stats 症状：用户截图原文 + 多行完整 stderr 命中 / 超时等无关错误不误判
- 端到端：fake 二进制复刻 argv[0] 分发（basename=git-ai → 原生分支，否则输出
  "git version 2.50.1" 代理形态）——非标准名直跑进代理分支、经 staging 链接跑进
  原生分支（修复核心机制的直接证明）

环境备注（2026-09-07 沙盒实录）：git-ai 1.7.2 装在 `/root/.git-ai/bin/git-ai`
（install.sh 产物），`~/.local/bin/git-ai` 是其符号链接；`dotnet` 不在默认 PATH，
需 `export PATH="/root/.dotnet:$PATH"`。`third_party/nuget` 目录不存在时 NuGet 对
Tests 工程直接报 NU1301（先于主工程的 RestoreOxyPlotAvalonia target 执行）——
`mkdir -p third_party/nuget` 后 target 自动下载 fork nupkg。


- 工作目录：`/data/user/work/ForkPlus-Next`（主仓库）；图表库源码仓库 `/data/user/work/oxyplot-avalonia`（hebin123456 fork，用于发 nupkg，主仓库已改为 PackageReference 消费其 release 产物，不再本地引用）
- 进度截图统一放 `verification/`（仓根），有进展及时提交推送，不攒批
- 构建产物不入库（bin/obj 已在 .gitignore；publish/ 已于 2026-09-02 清除，CI 产物走 release artifact）
