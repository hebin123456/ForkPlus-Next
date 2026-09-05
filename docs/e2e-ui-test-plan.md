# ForkPlus-Next 全功能 UI 自动化测试计划

> 固化日期：2026-09-05。本文件是全功能测试的唯一进度真相源（single source of truth）。
> 新 agent 接手：读「接手指南」→ 按「进度追踪」表找到下一个待做模块 → 按「测试基建约定」写测试。

## 接手指南

1. 进度以本文档「进度追踪」表为准：`未开始` → `进行中` → `完成`（每完成一个模块立即更新本表并 git push）。
2. 所有测试代码放 `src/ForkPlus.Tests/`，文件命名 `E2e<模块名>Tests.cs`。
3. 截图证据存 `docs/evidence/e2e/<两位模块号>-<模块短名>/<场景>.png`，随代码入库。
4. 测试写法严格遵循「测试基建约定」；禁止截图识别定位控件（用控件树查找 + RaiseEvent）。
5. 一次提交只做一个模块（或基建步骤），commit message 格式：`test(e2e): <模块> <一句话>`。
6. 每个模块完成后：跑该测试类 → 全绿 → 截图入库 → 更新本表 → commit+push（凭据见仓库 remote，勿写入代码）。
7. 全部完成后生成 `docs/evidence/e2e/index.html` 截图索引报告。

## 测试基建约定（阶段 0 已建）

### 已提供的基础设施

| 类 | 文件 | 用途 |
|----|------|------|
| `HeadlessAppBootstrap` | `HeadlessAppBootstrap.cs`（已有） | 无头 App 启动，真实主题资源 + 真 Skia 渲染 |
| `TestRepoFactory` | `TestRepoFactory.cs`（新建） | 构造各形态临时 git 仓库（多提交/分支/tag/stash/冲突/LFS/bare 远程…） |
| `ScreenshotHelper` | `ScreenshotHelper.cs`（新建） | 统一截图入库路径 + 非空白像素断言 + 前后差异断言 |
| `UiClick` | `UiClick.cs`（新建） | 控件树查找（按类型/名字）、点击（RaiseEvent）、输入、等待渲染 |

### 标准测试骨架

```csharp
[Collection("HeadlessAvalonia")]
public class E2eXxxTests
{
    [Fact]
    public void Xxx_Scenario_Yyy()
    {
        HeadlessAppBootstrap.EnsureStarted();
        string repo = TestRepoFactory.CreateBasic(); // 按需选形态
        try
        {
            HeadlessAppBootstrap.Run(delegate
            {
                // 1. 构造/打开真实控件
                // 2. UiClick.Find<T>(root, "Name") / UiClick.Click(btn) 交互
                // 3. ScreenshotHelper.Snap(window, "01-初始", "01-sidebar");
            });
            // 断言状态变化（不只截图）
        }
        finally { TestRepoFactory.Cleanup(repo); }
    }
}
```

### 铁律

- 点击用 `RaiseEvent(new RoutedEventArgs(Button.ClickEvent))` 或 headless 输入，**不用**截图识别。
- 每张截图必须配至少一条状态断言（截图非空白 + 交互效果）。
- 涉及真实 git 执行的用例，仓库建在临时目录，`finally` 清理。
- 不可自动化项（账号登录/公网）降级为 UI 冒烟：窗口能开、表单校验、取消路径。

### 截图口径（2026-09-05 用户约定，自模块 10 落地）

- **统一 1920×1280 最大化截图**：`ScreenshotHelper.Snap` 内部自动"放大窗口到 1920×1280 → 布局 → 截帧 → 复原窗口尺寸与全部滚动容器偏移"，调用方无感知。模块 1-9 的证据已随模块 10 的全量回归按新口径重生成（96/99 张精确 1920×1280；其余 3 张为高度受内容约束的窗口——欢迎页条形窗 1920×350、丢弃确认小弹窗 1920×156——宽度放大到 1920、高度保持内容自然高度，属"最大化"语义下受限窗口的真实形态）。
- 复原滚动偏移是硬要求：放大瞬间 viewport 超过文档 extent 会把 ScrollViewer.Offset 钳到 0（AvaloniaEdit TextView 布局期钳制），不复原会破坏截图点之后仍要断言滚动位置的用例。
- **宽度封顶窗口（模块 11 补充）**：`SizeToContent=WidthAndHeight` 的窗口（如 `CheckoutBranchWindow`，`MaxWidth=670`，WPF 原仓同款）内容驱动宽度会无视显式 Width 回缩自然宽——截图时临时降为 `Height` 让显式 Width 生效，仍受窗口自身 `MaxWidth` 钳制（= 窗口允许的最大宽，670×自然高）。既定口径下"受限窗口按自身极限渲染"的又一形态。

### Bug 修复策略（2026-09-05 用户约定）

- **测试用例发现生产代码 bug → 必须修复源码**（不是绕过或放宽断言）；修复随该模块的测试提交一起入库，并在变更日志里记"生产 bug 修复"条目（含根因）。
- 判定标准：与 WPF 原仓（`ForkPlus`）行为不一致 = 迁移 bug，修源码；与原仓一致 = 原始行为，测试按原版语义断言并在用例注释里注明"非迁移回归"（对照原仓文件路径/行为依据）。已有先例：模块 5 自动补全浮层渲染类名（模板 Resources 迁移缺失，修源码）、模块 7 横向滚动条弹动（防抖缺失，修源码）；模块 10 的 Next/Prev 最小滚动分数抑制（AvalonEdit/AvaloniaEdit 同源算法，原版行为，测试适配）。

## 模块规划与进度追踪

优先级 P0（主界面高频路径）→ P1（Diff 系统，含回归）→ P2（Git 工作流对话框）→ P3（设置与辅助）。

### 阶段 0：基建

| # | 任务 | 状态 |
|---|------|------|
| 0.1 | TestRepoFactory（基础/多分支/冲突/stash/bare远程/LFS 形态） | 完成 |
| 0.2 | ScreenshotHelper + UiClick | 完成 |
| 0.3 | 计划文档（本文件） | 完成 |

### P0 核心主界面

| # | 模块 | 覆盖内容 | 预计截图 | 状态 |
|---|------|---------|---------|------|
| 1 | 启动与仓库管理 | 欢迎页、仓库列表器（分组/搜索）、打开仓库、Tab 增删切换 | ~10 | 完成 |
| 2 | 提交历史视图 | 图表渲染、行选择/多选、搜索面板、tooltip、滚动虚拟化 | ~15 | 完成 |
| 3 | 侧边栏 | 分支/标签/stash/远程/子模块分组展开收起、过滤、右键菜单 | ~12 | 完成 |
| 4 | 修订详情 | 变更 tab、文件树 tab、摘要、reflog/tags 隐藏开关 | ~8 | 完成 |
| 5 | 变更与提交 | 暂存/取消暂存、行级 chunk stage/discard 浮窗、提交消息自动补全 | ~15 | 完成 |
| 6 | 工具栏 | push/pull/fetch/stash 按钮状态（领先/落后角标） | ~6 | 完成 |

### P1 Diff 系统（近期修复集中区）

| # | 模块 | 覆盖内容 | 预计截图 | 状态 |
|---|------|---------|---------|------|
| 7 | 文本 Diff | Split/SideBySide 切换、垂直+水平滚动同步（**横向滚动条弹动修复回归**） | ~10 | 完成 |
| 8 | DiffPopupWindow | 提交/变更两入口（文件树 tab 无弹窗入口，为内联预览）、横向滚动条、Escape/Space 关闭、上下键换文件 | ~6 | 完成 |
| 9 | 二进制 Diff | Hex 视图（图片内切换 + 非图片二进制直发两路径）、图片对比（SideBySide/Swipe 拖分割线/OnionSkin 滑块/HighlightPixels） | ~8 | 完成 |
| 10 | 合并冲突窗口 | 三方编辑器、冲突块选择、全选 ours/theirs、滚动同步回归 | ~10 | 完成 |

### P2 Git 工作流对话框

| # | 模块 | 覆盖内容 | 预计截图 | 状态 |
|---|------|---------|---------|------|
| 11 | 分支操作 | 创建/检出/重命名/删除/跟踪/多分支推送，9 窗口 | ~20 | 完成 |
| 12 | 标签操作 | 创建/删除/推送/多标签推送/详情，5 窗口 | ~10 | 完成 |
| 13 | 历史改写 | 合并/变基/交互式变基真实仓库端到端/拣选/还原/重置/Reflog | ~18 | 完成 |
| 14 | Stash | 保存/部分保存/应用/删除/重命名，5 窗口 | ~10 | 未开始 |
| 15 | 远程交互 | Fetch/Pull/Push/编辑远程/自定义 refspec（对本地 bare 真实执行） | ~15 | 未开始 |
| 16 | 子模块与 Worktree | 添加/删除子模块、子模块 diff、worktree 三窗口 | ~12 | 未开始 |
| 17 | 工作流套件 | GitFlow（init+start/finish×3）、GitMm、LeanBranching | ~20 | 未开始 |
| 18 | Git LFS | track/status/fetch/pull，4 窗口 | ~6 | 未开始 |
| 19 | 补丁与快照 | 保存为 patch/应用 patch/快照，3 窗口 | ~6 | 未开始 |
| 20 | 查看类窗口 | Blame/文件历史/仓库概览/统计/修订详情独立窗/GoToLine | ~12 | 未开始 |

### P3 设置与辅助

| # | 模块 | 覆盖内容 | 预计截图 | 状态 |
|---|------|---------|---------|------|
| 21 | 偏好设置 | General/Git/Commit/Integration/ImportExport/CustomCommands/AI | ~10 | 未开始 |
| 22 | 仓库设置 | General/IssueTracker/CommitTemplate | ~5 | 未开始 |
| 23 | SSH 与环境 | SSH 密钥/生成密钥/Git 实例/Workspace | ~6 | 未开始 |
| 24 | AI 功能 | AI 提交/代码审查/开发助手（UI + stub） | ~6 | 未开始 |
| 25 | 通用对话框 | MessageBox/Error/AskPass/颜色/快捷键/About/更新/诊断/通知 | ~14 | 未开始 |

### 明确降级项（UI 冒烟即可）

真实账号登录窗（GitHub/GitLab/Bitbucket×2/Gitea/GitHubEnterprise/OpenAI 七个）、公网 push/pull、PR/Issue 集成、应用更新下载 —— 只验证窗口打开 + 表单校验 + 取消路径。

## 变更日志

- 2026-09-05：计划固化为本文档；阶段 0 基建（TestRepoFactory/ScreenshotHelper/UiClick）完成。
- 2026-09-05：模块 1 完成（E2e01WelcomeAndRepoManagerTests：欢迎页表单、仓库列表选中/空态、Tab 增删选切换；截图 01-welcome/）。
- 2026-09-05：模块 2 完成（E2e02RevisionListTests：真实 git 管线加载、行选中、上下文搜索匹配标记/清空恢复、方向切换；截图 02-revisionlist/）。基建补强：HeadlessAppBootstrap 补 ServiceLocator 初始化（修复 headless 下 DelayedAction 回调被静默丢弃）、UiClick 新增 WaitFor 轮询等待。
- 2026-09-05：模块 3 完成（E2e03SidebarTests：分组结构断言（main 活跃分支/feature 嵌套文件夹/tags）、文件夹与分组展开收起（生产 IsExpanded setter 完整管线）、过滤防抖→Refilter→IsHidden、清空恢复、分支右键菜单（真实 PointerPressed 管线）、分组右键菜单、stash 分组；截图 03-sidebar/，9 张）。
- 2026-09-05：模块 4 完成（E2e04RevisionDetailsTests：三 tab（Commit 摘要/Changes 变更/File Tree 文件树）真实 git 数据加载与切换、摘要字段断言、diff 内容加载（ParsedDiffContent）、文件树选择→内容预览、Range 双提交对比（Commit/FileTree 禁用+自动切 Changes）、reflog 开关（状态栏文案语言无关断言）、tag 引用过滤（Filtered by 状态栏）、HideTags 开关往返；截图 04-revisiondetails/，8 张）。测试经验：行选中须用 RevisionListViewUserControl.Select（补发 NotifySelectionChangedFromCurrentItems），直接调 DragAndDropListView.Select 会被 IsMultiselectionInProgress 吞掉通知；同秒提交行序不保证，按 Subject 扫行定位。
- 2026-09-05：模块 5 第一批（E2e05ChangesAndCommitTests 三个用例 + E2eMainWindowHarness 基建）：真实 MainWindow 生产路径打开仓库（TabManager.OpenRepository，解决 headless 下 IsActiveRepository()==null 导致 Commit 视图状态刷新被 _pendingRepositoryStatusUiRefresh 推迟的核心阻塞）、工作区状态装配（修改/删除/未跟踪/已暂存四形态）、选中文件 working dir diff 加载、Stage/Unstage 选中文件（UI 列表 + git index 双重验证）、StageAllButton 智能切换 Stage All ↔ Unstage All；截图 05-changescommit/ 前 5 张。基建新增：TestRepoFactory.CreateWorkingDir（工作区四形态仓库）+ GitOutput（git stdout 断言助手）、E2eMainWindowHarness（真实 MainWindow 挂具：OpenRepository/CloseRepositoryTab/Tr/TrFormat）。测试经验：① 绝不能 Close() MainWindow（Closed→lifetime.Shutdown 会关停 headless App），收尾只 CloseRepositoryTab；② SelectFile 对已选中项会重复 Add 进 TreeView.SelectedItems（生产怪癖），数量断言会得 2，须用去重路径集合；③ Commit 按钮文案走 FormatCurrent（键含 {0} 占位符），断言用 TrFormat("Commit {0} File", n) 而非 Tr("Commit 1 File")；④ 取消暂存 c.txt 后它仍相对 HEAD 有改动 → 回到未暂存列表，Unstage All 期望是 4 项不是 3 项。
- 2026-09-05：模块 5 第二批（同文件再增三个用例，模块完成）：行级 chunk stage 浮窗（Select+CaptureRenderedFrame 逼真渲染帧 → DiffSelectionLayer 悬浮按钮 Stage/Discard... 出现 → 点 Stage → ApplyChunkCommand → git apply --cached 部分补丁，UI+`git diff --cached` 双验证：仅 line4 入暂存、line5 留工作区）；行级 discard 浮窗（点 Discard... → MessageBoxWindow.ShowDialog DispatcherFrame 模态泵，Post 泵内驱动：找确认框→截图→点"丢弃此行"→反向补丁生效，a.txt 内容 + git diff 双断言）；提交消息自动补全（描述框输入 Co-auth → 30ms 防抖 → Popup 弹出 Co-authored-by: 建议 → Tab 选中替换 → caret 定位/浮层关闭/FullCommitMessage 拼装）。截图 05-changescommit/ 补至 11 张。测试经验：① git diff 断言必须用 `+行文本` 前缀匹配（context 行也含原文本，纯 Contains 会误判）；② Popup 内容挂独立 PopupRoot，ListBox 从 popup.Child 直取（视觉树搜索找不到）；③ FullCommitMessage setter 走 DisableUpdates 静默写不触发建议，须对 AutoCompleteTextBox.Text 直接赋值模拟输入；④ 建议浮层的 ItemTemplate 断言防类名渲染回归。
- 2026-09-05（生产 bug 修复，E2e05 发现）：自动补全建议浮层显示类名 `AutoCompleteSuggestion` 而非建议文本。根因：原 WPF 三个按 DataType 的 DataTemplate 放在 ListBoxItem ControlTemplate.Resources 内，Avalonia ControlTemplate 无 Resources，迁移时模板被注释化但代码侧查找键 "AutocompleteListBoxItemTemplate" 保留 → ItemTemplate=null → ToString() 渲染。修复：Listview.axaml 恢复三个模板为顶层资源 + 新增 AutoCompleteSuggestionTemplateSelector（IDataTemplate 按运行时类型路由，Gitmoji 条目/用户身份带图标）。同批修复测试基建：E2eMainWindowHarness 收尾 Hide+清 lifetime.MainWindow——残留可见 MainWindow 参与后续主题测试布局时 ModernTabControl 模板重建抛 "already has a visual parent"（E2e05 先跑则 ThemeSystemIntegrityTests 必崩，实证为既有交互缺陷非本次回归）。
- 2026-09-05：模块 6 完成（E2e06ToolbarTests 三个用例）：无仓库 tab 全部 git 操作按钮禁用 + Undo/Redo 可见性跟随设置 + 角标隐藏（RefreshToolbar isEnabled=false 分支）、无 upstream 仓库按钮全启用 + 角标隐藏、ahead/behind 仓库 PullBadge "1"/PushBadge "2" 数字 + Canvas 定位断言（RefreshRepositoryData → UpdateRepositoryData → RefreshToolbarBadges 全链路真实 git 管线，侧边栏 2↑1↓ 与角标一致）；截图 06-toolbar/ 3 张。基建新增：TestRepoFactory.CreateAheadBehind（bare 远程 + 双克隆交叉推送：ahead 2 + behind 1，UpstreamStatus 基于本地 remote-tracking ref 无需网络）、harness CreateWindow（不开仓库）+ DetachWindow（窗口摘除）+ RemoveTestReposFromManager（测试仓库从"最近"列表摘除——OpenRepository 经 AddOrUpdateLastOpened 持久化到用户配置，残留 fpe2e_* 条目会污染真实用户机器；顺带清历史残留）。
- 2026-09-05：模块 7 完成（E2e07TextDiffTests 两个用例）：Split ↔ SideBySide 布局切换（生产入口：diff 头部 DiffLayoutModeToggleButton 真实点击序 IsChecked+Click → 设置 + NotificationCenter.DiffLayoutModeChanged → TextDiffControl.RefreshLayout 重建 → SideBySideCommitTextDiffControl 双编辑器 old/new + VisualPatch 内容迁移断言）、SideBySide 垂直滚动同步（右滚 100px → ScrollOffsetChanged → OnScrollOffsetChanged → 左跟随，偏移差 <1px）+ 水平滚动同步 + 交替滚动弹动回归（2026-09-05"点击横向滚动条界面弹动"修复：防抖 100ms + 差值检查，交替滚 5 轮收敛一致无循环）；截图 07-textdiff/ 5 张。测试经验：① 滚动编辑器必须用 ScrollToVerticalOffsetCompat/ScrollToHorizontalOffsetCompat（AvaloniaEdit 12.x 的 TextEditor.ScrollTo*Offset 是空操作，生产同步也走 compat）；② 测试仓库构造要懂 diff 语义——Commit diff 默认 hunk 视图，只改 1 行时 diff 仅 7 行无垂直滚动范围（须整文件大改动造大 hunk），宽行要放文件顶部（AvaloniaEdit TextView 宽度 extent 只由可见行决定，宽行滚出视野时水平 extent 塌缩、水平滚动范围消失）；③ **持久化设置污染**：测试改 ForkPlusSettings 后 harness 会把修改值落盘（CloseRepositoryTab→SaveSession），finally 只恢复内存不落盘 → 磁盘残留 SideBySide → 后续运行 E2e05 行级测试拿到 SideBySide 左侧旧内容编辑器而失败——修复：测试 finally 恢复后调 ForkPlusSettings.Default.Save() 落盘，且对布局敏感的测试（E2e05 行级）开头显式固定 Split。
- 2026-09-05：模块 8 完成（E2e08DiffPopupWindowTests 四个用例）：Commit 视图 Space 打开弹窗（生产入口：文件列表 KeyDown Tunnel → ShowDiffPopup 事件 → CommitUserControl.ShowDiffPopup → CreateCommitDiff → ShowAtCenter(父窗 90%) → UpdateDiff 同步行内内容；断言 Title=路径、CommitFileDiffControl、弹窗 90% 尺寸、CommitCodeEditor+VisualPatch 装配）+ Escape 关闭（Tunnel KeyDown）+ Space 重开（Closed → _diffPopupWindow 置空）+ 弹窗内 Space 关闭（bubble CLR KeyDown 路径）；长行弹窗水平滚动范围（PART_ScrollViewer.ScrollBarMaximum.X > 100）+ ScrollToHorizontalOffsetCompat 滚动到位；弹窗内 Down/Up 换文件（Tunnel → SelectNext/SelectPrevious → TreeView 选择变化 → 异步 git diff → popup.UpdateDiff → Title/内容/主视图选择三重同步断言）；变更视图（修订详情 Changes tab）Space 打开修订弹窗（CreateRevisionDiff → 基类 FileDiffControl 而非 CommitFileDiffControl、DiffCodeEditor 非空文档）。计划修正：文件树 tab 无弹窗入口（内联预览，模块 4 已覆盖）。截图 08-diffpopup/ 6 张。测试经验：**等待条件不能是"状态 != 初值"**——SelectNextFile 先 TreeView.SelectedItems.Clear()（同步 SelectionChanged → UpdateDiff(null) → Title 瞬时变 "File Preview"）再 SelectAndFocus(下一文件)，"Title != a.txt" 在全量负载时会抓到瞬时中间态而单独跑时碰不到（时序依赖假绿）——必须等"状态 == 期望终值"；另：弹窗泄漏进 lifetime.Windows 会殃及后续用例，finally 必须 CloseLeftoverPopups。
- 2026-09-05：模块 9 完成（E2e09BinaryDiffTests 两个用例）：图片对比全链路（Commit 视图选 img.png → FileDiffControl 二进制+IsImagePath 分发 → BinaryDiffUserControl；360x240 绿→橙大图，四视图逐一切换）——Side-by-Side 默认（old/new 双内容控件 + DiffImageSource 品红差异图生成 → header HighlightPixels 开关启用）、Swipe 分割线**真实窗口级指针拖拽**（MouseDown/Move/MouseUp → GridSplitter → 列宽 → SizeChanged → RefreshClipX → OverlayImage.ClipX 左拖后显著减小）、OnionSkin 透明度滑块（Slider.Value=0.5 → ValueChanged → NewOpacity=0.5；截图实证橄榄绿=50%绿+50%橙正确混合）、HighlightPixels（header 开关生产点击序 IsChecked+Click → 设置 + NotificationCenter.ImageDiffHighlightPixelsChanged → 双视图 OverlayImage.HighlightImageDiff 同步）、图片切 Hex（懒创建 HexDiffUserControl + 双 HexEditor 字节不同）；非图片二进制（data.bin 256→512B）→ FileDiffControl 直发 HexDiffUserControl（CanLoadHexDiff 小文件路径，无 BinaryDiffUserControl）。截图 09-binarydiff/ 8 张。**测试基建 bug 修复（探针实证）**：TestRepoFactory 的 PNG 生成器（MakeSolidPng 及新增 MakePngBytes）块序为 [type][length] 且 CRC 覆盖 length+data，违反 PNG 规范 [length][type][data][CRC]（CRC 覆盖 type+data）——Skia "Unable to load bitmap from provided data" 拒绝解码；从未暴露因为此前没有任何测试真正解码过生成图片。已同步修复两个生成器（IEND 常量的正确序即规范印证）。经验：**手写二进制格式必须用解码探针闭环验证**（生成→真实解码器消费），否则坏数据混进测试资产直到第一个真正解码的测试出现才炸。
- 2026-09-05：模块 10 第一批（E2e10MergeConflictTests 五个用例，5 落地 2 过 3 失败根因已定位）：MergeConflictUserControl 装配与 ResolveButton 三态（Merge/Choose {0}/禁用）✔、Choose theirs 主入口解决 ✔（checkout-index --stage=3 + git add → "M " 暂存迁移 UI+git 双验证）；进行中：三方编辑器模态泵（全选 ours → Resolve 提交）、冲突块选择（多块 1/2→2/2 部分解决状态机）、滚动同步回归。**测试基建 bug 修复（探针实证）**：CreateConflictMulti 的块间上下文仅 1 行，git 合并（marker size=7）把相邻变更并进同一冲突 hunk → 实际 1 块而非 2 块，"0/2" 断言失败；已改为 7 行 sep 分隔（探针复测 2 块）。**模态泵死锁防护**：handler 内断言失败 → 窗口滞留 → DispatcherFrame 永不退出 → dotnet test 无限挂起（CPU 0% 实证）——catch 必须强制 Close(false) 让错误以测试失败形式浮出（已给全部 3 个模态用例加防护）。**git 语义教训（探针实证）**：全选 ours 的解析内容 = HEAD → checkout-index stage=2 + add 后 porcelain 无任何条目（无差异不显示），文件从两个列表都消失而非"移入已暂存"——验证暂存迁移必须选 theirs（stage=3 ≠ HEAD），两条路径互补（用例 2 改 theirs、用例 3 覆盖 ours 无条目语义）。
- 2026-09-05：模块 10 完成（五用例全绿，截图 10-mergeconflict/ 12 张，**首启用 1920×1280 最大化截图口径**）：① ResolveButton 三态状态机（Merge/Choose {ours}/Choose {theirs}/禁用往返）；② Choose theirs 主入口（ResolveConflictGitCommand → checkout-index stage=3 + add，UI 暂存迁移 + git 双验证）；③ 三方编辑器模态泵（三编辑器装配、0/1→1/1、全选 ours 提交 → 内容=HEAD 无状态条目、文件双列表消失）；④ 冲突块选择（行级 OnMergeLineAdded 按块 1/2→2/2，block1 ours + block2 theirs 混合落盘 "M " 暂存）；⑤ 滚动同步回归 + 冲突块导航 + 布局方向切换（Vertical↔Horizontal Merged 编辑器 ColumnSpan 迁移）。**两项测试基建修复（本轮根因闭环）**：a) `UiClick.Toggle` 升级为生产点击序（赋 IsChecked + raise Click）——MergeConflictUserControl 的 Local/Remote CheckBox 与 WPF 原仓同为 `Click="MergeCheckBox_Changed"` 绑定（ForkPlus 原仓 MergeConflictUserControl.xaml 247/282 行实证），仅赋 IsChecked 不触发 Click 处理器；补发 Click 对 IsCheckedChanged 绑定的 CheckBox（SideBySideMergeWindow 全选框）是无害空操作。b) 用例 2 断言改 TrFormat("Choose {0}") 全量等值——本地化字典只有格式键 "Choose {0}"，裸键 Tr("Choose") 回退英文原文，中文文案不含它（首跑实证：按钮已是"选择 theirs"却断言失败，纯断言口径错误）。**Next/Prev 导航语义澄清（探针 + 原仓对照，非迁移回归）**：ScrollToLine 走 AvaloniaEdit `ScrollTo(line,-1)` → `MinimumScrollFraction=0.3` 守卫，目标与当前偏移差 < 0.3×视口高时抑制滚动；首块在文件顶部时从顶点 Next 差 ~52px 被抑制且中线行不动（原版同款行为）——测试模拟"用户已聚焦首块"（compat 直设偏移把首块行滚到中线）后 Next→第二块（大位移过守卫）→Prev→回首块，断言稳健（中线落行 11-13 的容差内 FindNextChunk 均跳过首块）。**基建新增**：ScreenshotHelper.Snap 内置"1920×1280 放大 → 截帧 → 复原尺寸+全部 ScrollViewer 偏移"（用户截图口径约定），全量回归实证模块 1-9 不受影响；行高估算口径 = extent 高/行数（探针实证与 ScrollToLine 中心定位吻合，误差 <0.3%）。
- 2026-09-05：模块 11 完成（E2e11BranchOperationsTests 十用例全绿，截图 11-branchops/ 13 张）：① 创建分支（空名/重复名/非法名三态禁用 + checkout 开关命令预览 `branch`↔`checkout -b` 切换 + 真实建支切支）；② 检出分支（命令预览 + 真实切支 + 工作区文件迁移）；③ 重命名本地分支（预填当前名/同名/重复名禁用 + `-m` 预览 + 真实改名）；④ 删除本地分支（非活跃 `-D` 预览 + 真实删除）；⑤ 删除远程分支单/多两模式（单分支 GitPointView 视图 + 多分支 GitPoints 列表装配，`push --delete` 真实删远程，ls-remote 直查 bare 验证）；⑥ 跟踪远程分支（预填 ShortName + `checkout -b` 跟踪预览 + 真实建本地跟踪分支（`branch.<name>.remote` 配置验证））；⑦ 多分支推送（列表装配含 "(new)" 上游名 + `--set-upstream --atomic` 真实推送）；⑧ worktree 检出（默认路径 `<repo>-worktrees/<branch>` 预填 + `worktree add` 真实创建 + 新 tab 开启收尾关闭）；⑨ Lean 分支流程（Start 建支切支含空名/重复名校验 → 单提交后 Finish 合回 main fast-forward，含 `You must sync` 系列前置校验的同步对齐）。**三项测试口径修正（非生产 bug，按 bug 修复策略原仓对照判定）**：a) 非法分支名用例以 `:` 为准——两仓 `ReferenceNameValidator` 规则集相同（`HasIllegalCharacters` 只拦 `?`/`*`/`[`，控制字符拦 `~`/`^`/`:`），空格虽被 git 本体拒绝但两仓校验器均不拦（原始行为，非迁移回归）；b) RemoveRemoteBranch 单分支模式断言 `GitPointView.Value`（构造器按数量分流：单=视图、多=列表，`GitPoints.ItemsSource` 单模式本就不装配）；c) PushMultipleBranches `OnSubmit` 是"入队即关"模式（`JobQueue.Add` 后立即 `Close()`，与 AddUndoable 型"命令完成才关"不同）——终态断言须轮询 `ls-remote` 而非弹窗关闭即查（shell 手工复现 push 命令成功排除生产嫌疑后定位为时序口径）。**截图口径补充（宽度封顶窗口）**：`SizeToContent=WidthAndHeight` 的 `CheckoutBranchWindow`（`MaxWidth=670`，WPF 原仓同款）内容驱动宽回缩自然宽，Snap 临时降为 `Height` 后按窗口允许的最大宽 670 渲染（其余 12 张：PushMultipleBranchesWindow 无 SizeToContent 全量 1920×1280；SizeToContent=Height 型弹窗 1920×自然高）。**本地化口径**：CreateBranchWindow 重复名警告是全串拼接直传 `SetStatus`，Translate 内置 TranslatePattern 格式键模式匹配回退命中 `Branch '{0}' already exists` → zh-Hans 输出，按 `TrFormat` 断言（与 LeanBranchingStart/RenameLocal/TrackRemote 直接 `string.Format(Translate(fmt))` 同效）。
- 2026-09-05：模块 12 完成（E2e12TagOperationsTests 七用例全绿，截图 12-tagops/ 10 张）：① 创建标签（空名/重复名（`Tag '{0}' already exists` 格式键回退，PreferencesLocalization 显式 ReplacePattern）/非法名 `:` 三态禁用 + 无消息 `git tag -a <name> <commit>` ↔ 有消息 `-m "<msg>"` 加引号预览 + 真实建附注标签（`cat-file -t`=tag、`%(contents)` 消息、`^{}` 指向 main HEAD 三重验证））；② 创建并推送（推送开关预览追加 `git push <remote> refs/tags/<name>` 行 + 本地/远程双端验证 + 既有远程标签不动）；③ 删除单标签（单模式 GitPointView 装配 + 本地 `tag -d` ↔ 勾选"从远程删除"追加 `push --delete` 两态预览 + 本地/远程（ls-remote 直查 bare）双端删除）；④ 删除多标签（列表模式 GitPoints 装配 + `tag -d a b` 批删）；⑤ 单标签推送（ComboBox 自动选中 origin（upstream 推导）+ `push origin refs/tags/rel-3` 真实推送 + 未选标签不上远程）；⑥ 多标签推送（名字列表装配 + 单命令两 refspec 推送）；⑦ 标签详情（附注 + 轻量两形态）。**TagDetailsWindow 语义判定（探针实证，非迁移回归）**：tagger 三字段空、消息区=tagger 行+空行+tag 消息全文（附注）或提交消息（轻量）是两仓一致的原始行为——`bt_get_tag_details` 只接受 tag 对象 oid（探针三种输入直测：tag对象sha 成功返回结构化 tagger，剥壳 sha/轻量 sha 报 NotFound），而侧栏/提交列表的 Tag 由 `RepositoryReferences.New` 装配、其 dereferencedShaString 恒传同一个（剥壳）sha（两仓 `RepositoryReferences.cs`/`Reference.cs` 逐字节 diff 一致）→ `TargetObjectSha` 永远=剥壳 sha → if 分支侧栏链路两仓均不可达，恒走 for-each-ref 回退；且 Windows 原版 `bt_get_references` 也必返回剥壳 sha（否则 `ReferencesBySha` 键不上 commit sha、附注标签挂不到提交图节点，Fork 核心功能不可坏）。测试期望值用与生产完全相同的 for-each-ref 命令计算（`GitPsi` 与 `GitRequest` 同为单 Arguments 串 → 同一 .NET argv 解析 → 输出逐字节一致；.NET 按剥引号规则解析 `--format="..."`，与探针输出无引号互证）。**时序口径沿用**：CreateTag/RemoveTag 是"命令完成才关"（SubmitAndWaitClose），PushTag/PushMultipleTags 是"入队即关"（关窗 ≠ 推送完成，轮询 ls-remote——模块 11 教训）。**设置污染防护**：`CreateTag_Push` 在 OnSubmit 落盘，创建类用例 finally 快照恢复 + Save（模块 7 持久化教训）。
- 2026-09-05：模块 13 完成（E2e13HistoryRewritingTests 九用例全绿，截图 13-historyrewrite/ 16 张）：① 合并（干净：merge-tree 构造期预检 "without conflicts" + Fast-forward/No Fast-Forward 命令预览切换 + 真实 `--no-ff` 合并（合并提交双亲 + 6 主题 + 5 文件工作区）；冲突预检：警告图标 + "will cause conflicts" + 取消路径仓库零变更）；② 变基（构造期预检 + `git rebase refs/heads/main`（Reference.ObjectName=FullReference 口径）+ autostash 开关预览往返 + 真实变基：3 提交重放线性化 + merge-base=main + 文件迁移）；③ 拣选单提交（单视图折叠 + `--no-commit`/`-x` 选项预览 + no-commit 时 -x 开关禁用 + 真实执行：仅 f2.txt 落地）；④ 拣选多提交（列表模式 + 预览旧→新反转 + 两提交顺序重放）；⑤ 还原（`--no-commit` 预览 + 真实 revert：`Revert "base two"` 顶提交 + b.txt 消失）；⑥ 重置（Soft/Mixed/Hard 三态预览（SelectedIndex 0/1/2 生产 SelectionChanged 管线）+ 真实 hard reset：main 移动 + b.txt 连工作区丢弃）；⑦ Reflog（条目装配 + `{0} entries loaded.` 状态栏 + Jump 模态确认泵（模块 5 同款 DispatcherFrame）→ 真实 reset --hard 到所选状态 + Refresh 后新 reflog 条目入列）；⑧ 交互式变基（**真实 RI 辅助进程全链路**：构造即启动真实 `git rebase -i`（sequence.editor=ForkPlus.RI）→ RI 经命名管道 IPC 回传 todo → GetRebaseTodoListCommand 解析装配 3 提交 → 行内 ComboBox 生产路径选 Drop → OnSubmit 写回 todo → RI 放行 → git 完成变基 → feature 线性重放 f1+f3（f2 丢弃）+ main 成祖先）。**生产 bug 修复（迁移回归，探针实证）**：ResetBranchWindow "Reset type" ComboBox 打开即空——axaml 中 Mixed `ComboBoxItem IsSelected="True"` 在 Avalonia 下于用户首次展开下拉前不生效（关闭态 SelectedIndex=-1、SelectedItem=null，而命令预览却默认 `--mixed`，用户所见与将执行的不一致；WPF ComboBox 加载即生成全部容器故原版正常，Avalonia 容器在下拉 Popup 内延迟物化）。探针三段实证：show 后 -1/null → 开下拉变 1（容器物化才处理 IsSelected）→ 关下拉保持 1。修复：构造器编程式 `ResetTypeCombobox.SelectedIndex = 1`（与 `_resetType` 默认值 Mixed 一致，沿用 CherryPick/Revert 窗口同款模式），全仓扫描确认仅此一处 ComboBoxItem 受影响（另一处 IsSelected=True 是 TabItem，TabControl 容器即时机化无此问题）。**基建新增**：TestRepoFactory.CreateHistoryRewrite（main/feature 分叉无重叠：干净合并/变基/拣选/还原/重置）+ CreateHistoryConflict（同行修改：merge-tree 冲突预检）+ 测试内 EnsureDotnetRootForRiHelper（沙箱 DOTNET_ROOT 未设时 RI apphost 解析不到 .NET 运行时——"You must install .NET"探针实证，从当前运行时目录上推三级注入进程环境，git→RI 继承）。**时序口径**：Merge/Rebase/CherryPick/Revert/Reset/InteractiveRebase 均为 AddUndoable 型"命令完成才关"（SubmitAndWaitClose 直接适用）；ReflogWindow 是 CustomWindow 且 Jump 不关窗（终态轮询 rev-parse；确认框走模块 5 同款模态泵）。**git 语义教训（探针实证）**：`git log` 默认按提交日期序而非拓扑序——factory 中 main 的 base two 晚于 feature 的 f3 提交，合并后主题序是 [merge, base two, f3, f2, f1, base one]，跨分支断言除"最新必在顶"外应按包含集而非位置断言。**交互式变基兜底**：失败收尾走反射调 StopRebaseInteractiveProcess("cancel") 停 RI/git 进程再 Dispose（绕过 OnClosing 确认框防模态泵挂死），防进程泄漏殃及后续用例。
