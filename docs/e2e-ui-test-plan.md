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
| 7 | 文本 Diff | Split/SideBySide 切换、垂直+水平滚动同步（**横向滚动条弹动修复回归**） | ~10 | 未开始 |
| 8 | DiffPopupWindow | 提交/变更/文件树三 tab、横向滚动条、弹窗关闭 | ~6 | 未开始 |
| 9 | 二进制 Diff | Hex 视图、图片对比（OnionSkin/Swipe 滑块） | ~6 | 未开始 |
| 10 | 合并冲突窗口 | 三方编辑器、冲突块选择、全选 ours/theirs、滚动同步回归 | ~10 | 未开始 |

### P2 Git 工作流对话框

| # | 模块 | 覆盖内容 | 预计截图 | 状态 |
|---|------|---------|---------|------|
| 11 | 分支操作 | 创建/检出/重命名/删除/跟踪/多分支推送，9 窗口 | ~20 | 未开始 |
| 12 | 标签操作 | 创建/删除/推送/多标签推送/详情，5 窗口 | ~10 | 未开始 |
| 13 | 历史改写 | 合并/变基/交互式变基真实仓库端到端/拣选/还原/重置/Reflog | ~18 | 未开始 |
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
