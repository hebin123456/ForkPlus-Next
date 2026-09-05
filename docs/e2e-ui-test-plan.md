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
| 3 | 侧边栏 | 分支/标签/stash/远程/子模块分组展开收起、过滤、右键菜单 | ~12 | 未开始 |
| 4 | 修订详情 | 变更 tab、文件树 tab、摘要、reflog/tags 隐藏开关 | ~8 | 未开始 |
| 5 | 变更与提交 | 暂存/取消暂存、行级 chunk stage/discard 浮窗、提交消息自动补全 | ~15 | 未开始 |
| 6 | 工具栏 | push/pull/fetch/stash 按钮状态（领先/落后角标） | ~6 | 未开始 |

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
