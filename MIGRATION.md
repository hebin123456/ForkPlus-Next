# ForkPlus-Next — WPF → Avalonia 迁移

> 本仓库是 [ForkPlus](https://github.com/hebin123456/ForkPlus)（WPF 版）向 Avalonia 12 的迁移目标。
> 基线由 [WpfToAvalonia](https://github.com/hebin123456/WpfToAvalonia)（`wpf2ava`）自动转换生成。
>
> **本文档是交接文档**：下一个 agent 请从「当前状态」和「下一步行动」开始读，避免重复踩坑。

## 基线信息

- 源版本：ForkPlus `v3.12.3`（commit `498b4ca`）
- 转换工具：wpf2ava @ `82c462c`（Avalonia 12.1.1 / net10.0）
- 转换报告：`docs-conv-report.md`（INFO 7348 / WARN 413 / TODO 844）

## 环境与构建（重要）

```bash
# dotnet 不在默认 PATH，必须先 export（沙盒环境重置后 SDK 装在 ~/.dotnet）
export PATH="$HOME/.dotnet:$PATH"
export DOTNET_ROOT="$HOME/.dotnet"
# 若 ~/.dotnet 只有 sentinel 没有 binary（沙盒重置），用 /data/user/work/dotnet-install.sh 重装：
# bash /data/user/work/dotnet-install.sh --channel 10.0 --version 10.0.400 --install-dir $HOME/.dotnet

# 编译主工程（在 /data/user/work/migration/ForkPlus-Next/src/ForkPlus 下）
dotnet build --no-restore -v q -nologo 2>&1 | grep -E "error CS" | sed -E 's/ \[.*//' | sort -u

# 编译整个解决方案并收集 AVLN 错误（XAML 批量修复的标准命令）
cd /data/user/work/migration/ForkPlus-Next/src && dotnet build ForkPlus.sln -clp:ErrorsOnly -nologo 2>&1 | tee /tmp/build_outN.txt | tail -3
grep -oP 'error AVLN\d+' /tmp/build_outN.txt | sort | uniq -c | sort -rn   # 错误码分布
grep -oP 'error AVLN\d+.*' /tmp/build_outN.txt | sort -u | wc -l          # 唯一错误数

# 查看源生成器产物（调试 x:Name 字段问题时极其有用）
dotnet build --no-restore -v q -nologo -p:EmitCompilerGeneratedFiles=true
# 产物位于 obj/Debug/net10.0/generated/Avalonia.Generators/Avalonia.Generators.NameGenerator.AvaloniaNameIncrementalGenerator/

# git 推送（凭据已配置在 remote url 中）
git push origin HEAD
```

- 工作目录：`/data/user/work/migration/ForkPlus-Next`（主仓库）、`/data/user/work/migration/WpfToAvalonia`（转换工具）、`/data/user/work/migration/ForkPlus`（WPF 源仓库，对照用）
- API 查证工具：`/data/user/work/apicheck`（`dotnet run --no-build -- "类型名" [成员过滤]` 可列出 Avalonia 12 类型和成员，避免瞎猜 API）
- 错误清单快照：`/data/user/work/errors.txt`（历史）；最新口径用上面构建命令实时生成

## 当前状态

**🎉 里程碑：Windows-only P/Invoke 崩溃簇清零——跨平台兼容层落地（2026-08-30 本轮5）。**
- `dotnet build` 0 错误；AVLN XAML 错误 0；运行零未处理异常。
- **完整冒烟通过（1920×1280）**：启动 → Git 版本警告对话框 → 主窗口 → 自动恢复 .nvm 仓库会话（提交列表/分支图/标签徽章/侧栏分支标签远程子模块树全渲染）→ 点击提交行 → 蓝色高亮选中 + 详情面板完整填充（作者/日期/完整 SHA/父提交/提交说明/13 个变更文件树）。verification/21-commit-select-details-1920x1280.png + 21a-repo-restored。
- 本轮清零的 Unix 崩溃源（全部 DllNotFoundException，均有 fork.log 实证）：`StrCmpLogicalW`(shlwapi)、`StrFormatByteSize`(shlwapi)、`SHGetFileInfo`(shell32)、`GetCursorPos`(user32)、`ShellExecute` 打开文件。修复模式统一：`OperatingSystem.IsWindows()` 分支 + Unix 托管等价实现/占位。
- MouseHelper 特殊：Avalonia 无全局 GetCursorPos 等价 API（InputManager internal、RawEventArgs 成员 protected，反射实证），改 X11 `XQueryPointer`（libX11，硬件级查询，拖放中也有效）。
- **提交列表自动渲染**（无需任何兜底/诊断代码）：verification/18-commitlist-final-no-diag.png + 18a-zoom（.nvm 唯一提交行：v0.40.2 标签徽章[引用模板生效] + Jordan Harband 头像/姓名 + ffec9fe + 11 Mar 2025 20:30）。
- 根因（本轮4 最终实证）：`RevisionsDataSource` 的 `IEnumerable.GetEnumerator()` 返回 `_decoratedRevisions`（仅"已物化"行，Reload 后为空）而 `Count` 返回 `_visualGraph.Count` —— **枚举器与 Count 契约违背**。WPF ItemContainerGenerator 用 Count+IList 索引器取项（惰性物化），从不枚举 → 潜伏 10 年无影响；Avalonia 12 `PanelContainerGenerator.OnItemsChanged` Reset 分支用 `foreach` 枚举 ItemsView（`ItemCollection.GetEnumerator → Source.GetEnumerator`）生成容器 → 枚举空 = 0 容器 = 列表空白。修复：GetEnumerator 改按行惰性物化枚举（与索引器语义对齐），一行修复即恢复整条链。
- [probe] 排查方法论（反射挂 `PostCollectionChanged` + 快照生成器状态）定位到"生成器订阅在、处理器执行完、无异常、foreach 却枚举 0 项"——排除法收敛到源集合契约，可直接复用到后续同类问题。
- 上一轮里程碑：仓库打开链路全通（2026-08-29 本轮3，verification/15-*.png）。

错误数轨迹（按唯一错误去重统计）：

| 提交 | 唯一错误数 | 内容 |
|---|---:|---|
| `0e16c1a` | ~970 | 迁移进度快照 + WpfCompat 扩展 |
| `51bb07f` | 912 | WpfCompat 兼容层自身编译清零 |
| `67f0b76` | 708 | 大批量机械修复 + 兼容层扩展 |
| `4db2c98` | 644 | 事件适配与绘制簇修复 |
| （未提交） | 526 | XAML 编译恢复（OxyPlot xmlns 修复） |
| `39077cc` | 252 | 恢复 PlotView 元素，字段生成恢复 |
| `0036598` | 320→242 | Owner/WindowStartupLocation 对象初始化器簇 + DoubleAnimation 构造器/Completed 事件 |
| `f61cf65` | 242→154 | 文件对话框 shim + ContainerFromElement/ContentPresenter/DataTemplate/StartDrag 簇 + Button.ClickEvent + OverrideMetadata 移除 |
| `fccada2` | 154→57 | Visual 可视树遍历 + GetParent/HitTest/Run 模式簇 + SshPassphrase/GitMmStart/Clipboard/Dispatcher.Post 修复 |
| `0922795` | 57 | docs: 迁移文档刷新（纯文档提交） |
| `3c3837c` | 57→0 | IBrush/Rect 不可变簇 + 滚轮转发 + WeakEventManagerBase 4 泛型 + PointerPressed 合成 + BinaryDiff 绘制簇等 20 类修复 |
| `3335377` | C# 0 / AVLN 1198 | IPC 管道修复 + 文档记录 XAML 编译静默失败根因 |
| `a64e8e2` | AVLN 1198→390 | xamlpass1-4 批量 XAML 修复（详见下节「XAML 批量修复方法论」） |
| `a526bfc` | 390 | docs: 390 错误模式分组 + pass4 未生效复盘 |
| `415db4d` | AVLN 390→103 | xamlpass5-6：错误驱动精确修复（FocusVisualStyle 块删、ItemContainerTheme→Style、IsCheckable→ToggleType、PreviewKeyDown→KeyDown、ViewportWidth 等 TemplateBinding 簇、Resources on Style 删） |
| `e6466dd` | AVLN 103→1 | xamlpass7 + 定向手工修复：typed property、事件签名、PART_TextPresenter、ItemContainerTheme 块、TemplateKey 花括号、IsMouseOver 删、误删 HorizontalContentAlignment 回滚 |
| （本轮） | AVLN 1→**0** + 运行时推进 | App 生命周期迁移 + 运行时冒烟修复链（资产大小写/BitmapImage shim/代理 owner 窗口/ColorConverter/对话框 NRE，详见「运行时冒烟已修复的问题」） |
| （本轮2） | 运行时：**主窗口渲染成功** | StyleKeyOverride 根因修复 + TabControl ItemsPanel FuncTemplate + SelectionChanged 初始化时序 NRE + 16 文件样式修复（详见「运行时修复链 2」） |
| （本轮3） | 运行时：**仓库打开链路全通** | ClosableTabControl 选卡事件断链 + 侧栏早期 SelectionChanged NRE + DataTemplateKey→App.DataTemplates 57 个模板迁移 + ReferencePanel ControlTheme + SelectableTextBlock（详见「运行时修复链 3」） |
| （本轮4） | 运行时：**提交列表自动渲染根因修复** | RevisionsDataSource 枚举器/Count 契约违背（WPF 索引器生成 vs Avalonia foreach 枚举生成）+ GridView→ItemTemplate 行模板 + CreateContainerForItemOverride 容器链 + TextField Inlines 触发链 + ContentPresenter ContentTemplate 补绑（详见「运行时修复链 4」） |
| （本轮5） | 运行时：**跨平台 P/Invoke 崩溃簇清零** | shlwapi/shell32/user32 五处 DllNotFoundException 修复（托管等价实现/占位图标/X11 XQueryPointer），会话恢复 + 提交选中详情联动 1920×1280 完整冒烟实证（详见「运行时修复链 5」） |

| 阶段 | 状态 | 说明 |
|---|---|---|
| 0 基线导入 | ✅ 完成 | 全量转换产物入库 |
| 1 C# 编译清零 | ✅ **完成** | 主工程 + AskPass + RI + 4 个测试工程全部 0 错误 |
| 2 XAML (AVLN) 清零 | ✅ **完成** | 0 错误，XAML IL 重写恢复，`CompiledAvaloniaXaml.*` 已生成 |
| 3 运行时验证 | 🔄 进行中（**约 85%**） | **核心链路全通**：首启向导、仓库打开、提交列表渲染、提交选中详情联动、会话恢复、跨平台 P/Invoke 兼容层；待验证：次级窗口（FileHistory/Blame/Merge）、菜单命令执行、DataTrigger 视觉状态、大仓库性能（虚拟化） |
| 4 已知遗留 | 📋 见下文 | AutomationTests 的 FlaUI 依赖等 |

**整体进度估算：约 85%**。编译两大阶段（C#/XAML 清零）已 100% 完成；运行时验证核心链路（启动→开仓→列表→详情）已通，剩余为边缘交互（次级窗口/菜单命令/视觉状态补齐）与工程收尾（FlaUI 隔离、WpfCompat 死代码清理、性能虚拟化）。

## 运行时阻塞：Textblock.axaml StaticResource（✅ 已解决，存档备考）

**现象**：`dotnet run` 启动时抛
```
System.Collections.Generic.KeyNotFoundException: Static resource 'Avalonia.Controls.TextBlock' not found.
   at StaticResourceExtension.ProvideValue ...
   at CompiledAvaloniaXaml.!AvaloniaResources.XamlClosure_43.Build_3 in Theme/Styles/Textblock.axaml:line 12
```

**触发链**（从堆栈反推）：某个 TextBlock 的属性挂 `{DynamicResource Xxx}` → 主题变体解析触发 `DynamicResourceExpression.PublishValue` → `FindResource` 沿 Application 资源链查找 → 命中 `Generic.axaml` 合并的 `Textblock.axaml` 字典里**延迟构建的 ControlTheme**（ControlTheme 存资源字典是 deferred content，首次访问才构建）→ 构建时求值 `BasedOn="{StaticResource {x:Type TextBlock}}"` → 抛异常。

**根因分析**：`{x:Type TextBlock}` 的 ControlTheme 定义在 `Button.axaml`（Generic.axaml 合并顺序在 Textblock.axaml 之前），但**延迟构建内部的 StaticResource 查找范围不含跨字典的兄弟/祖先合并字典**（编译期把 `StaticResourceExtension` 求值绑定到 deferred builder 的局部 resolver，不是运行时全资源链）。WPF 的 StaticResource 沿 merge 顺序向上找得到，Avalonia 的 deferred 场景找不到。

**实际修复**：`fix_basedon.py` 批量移除 15 处失效 `BasedOn="{StaticResource {x:Type ...}}"` 引用（Textblock.axaml 等文件），派生主题内联基类 Setter。**全仓同类引用**：`grep -rn 'BasedOn="{StaticResource' --include='*.axaml'` 检查跨文件引用时同样按此思路处理。

## 本轮运行时修复链（2026-08-29，AVLN 清零后冒烟推进）

按出现顺序，每个都是「启动 → 抛异常 → 修 → 下一个」推进出来的：

1. **资产目录大小写（FileNotFoundException: avares://ForkPlus/Assets/ForkPlusIcon.png）**：物理目录是小写 `assets/`、文件名全小写，XAML 里引用 PascalCase。Linux 区分大小写直接炸。**修复**：`git mv` 把 193 个资产重命名为 PascalCase（`assets/` → `Assets/`），脚本批量改 324 处引用。**教训：pack URI 大小写敏感，跨平台必须统一 PascalCase。**
2. **对话框 NRE（ConfigureGitInstanceWindow.get_IsSubmitAllowed）**：`OnContentChanged` 里立刻 `InitializeDialogChrome` 访问 x:Name 字段，但字段尚未生成。**修复**：`Dispatcher.UIThread.Post(InitializeDialogChrome)` 延迟到模板应用后。
3. **启动期对话框 owner（InvalidOperationException: Cannot show window with non-visible owner）**：启动流程里弹对话框时 MainWindow 还没 Show。**修复**：`WindowDialogCompat` 里无可见 owner 时建 1x1 透明代理窗口兜底（`ForkPlusDialogWindow.cs`）。
4. **颜色字符串（FormatException: Invalid color string '##54A353'）**：ColorConverter shim 的 `ConvertFromString` 给已带 `#` 的串又加了一遍前缀。**修复**：仅在缺失时补 `#`。
5. **图片资源类型（InvalidCastException: String → IImage）**：`Images.Light/Dark.axaml` 里 256 个 `x:String` 存 URI，`Image.Source="{DynamicResource XxxIcon}"` 绑定时炸。**修复**：`WpfCompat.Batch2.cs` 新增 `BitmapImage : IImage` shim（可 XAML 声明 + UriSource 属性内部 `AssetLoader.Open` 加载），两文件 128x2 条全部转 `wpf:BitmapImage`。
6. **App 生命周期**：`OnStartup`/`OnExit` 是 WPF 风格方法永远不会被调。**修复**：主体搬进 `OnFrameworkInitializationCompleted`（desktop lifetime 分支），`App()` 构造补 `AvaloniaXamlLoader.Load(this)`（同时消掉 AVLN3000）。
7. **重复资源键**：`Textbox.axaml`/`Listview.axaml` 存在同 key ControlTheme（Avalonia 合并时后写的会覆盖但不报错，行为不可控）。**修复**：`scan_dup_keys.py` 扫描去重。
8. **ThemeTypeExtensions**：主题切换动态加载的 URI 后缀还是 `.xaml`，改成 `.axaml`。

## 运行时修复链 2（2026-08-29 本轮：主窗口渲染成功）

上一轮遗留的终极阻塞：`ForkWindow_Loaded` 抛 NRE（`_menuManager` 为 null）→ 根因是 **PART_MainMenu 模板部件找不到 → 自定义窗口模板从未应用**。诊断证据：`OnApplyTemplate` 时 `Theme` 已正确等于 `MainWindowStyle`，但 `Template` 是 `FuncControlTemplate`、可视树只有 1 个 `ContentPresenter`。

1. **【根因】CustomWindow implicit ControlTheme 查找失败 → `StyleKeyOverride` 修复**：
   - Avalonia 查找 implicit ControlTheme 的链路（反编译 Avalonia 12.1.1 `StyledElement.GetEffectiveTheme`）：`Theme` 属性为 null 时 → `TryFindResource(StyleKey)` —— 用 **StyleKey** 作 key 精确匹配资源。
   - `Window` 基类自带 `protected override Type StyleKeyOverride => typeof(Window)`，因此 `MainWindow.StyleKey == typeof(Window)`；而 `Window.axaml` 里 ControlTheme 的 key 是 `{x:Type ui:CustomWindow}`（= `typeof(CustomWindow)`）——**key 永远对不上，隐式主题永不应用**，模板退化为 ContentControl 默认模板。
   - **修复**：`CustomWindow.cs` 加 `protected override Type StyleKeyOverride => typeof(CustomWindow);`。所有 CustomWindow 子类（MainWindow/ReflogWindow/各 Dialog）随即全部命中 `Window.axaml` 的 ControlTheme。显式 `Theme="{DynamicResource MainWindowStyle}"` 优先级高于隐式主题，BasedOn 链不受影响。
   - **验证效果**：visCount 1→19，shape 出现完整窗口 chrome（Border/DockPanel/ToggleButton/Popup/Button×4），PART_MainMenu FOUND，_menuManager SET。
2. **TabControl 模板 `UniformGrid IsItemsHost="True"`（MethodAccessException）**：`Panel.IsItemsHost` setter 是 internal，XAML 设置运行时炸。**修复**：`ClosableTabControl` 构造函数用 `FuncTemplate<Panel>` 设 `ItemsPanel`（`new UniformGrid { Rows = 1 }`），模板里改 `<ItemsPresenter ItemsPanel="{TemplateBinding ItemsPanel}"/>`，原背景由样式选择器 `^ /template/ ItemsPresenter > UniformGrid` 设置。
3. **XAML EndInit 期 SelectionChanged NRE**：Avalonia `TabControl` 在 XAML `EndInit` 就触发 `SelectionChanged`（WPF 需交互后才触发），此时 `RepositoryDetailsUserControl` 构造函数还没执行到 `_updatePreviewAction = new DelayedAction(...)`。**修复**：处理器加 `_updatePreviewAction != null` 防御。**模式：XAML 事件处理器一律假设"字段可能未初始化"**。
4. **16 个样式文件微修**（本轮提交可见）：BooleanToVisibilityConverter 残留引用删除、ReflogWindow/BinaryContentUserControl 的属性迁移等。
5. **【本轮2 核心】模板内裸 ContentPresenter 导致按钮图标空白 + 主内容区空白**：
   - 现象：窗口控制按钮（PART_MinimizeButton 等）位置正确（46x26、可见）但 Path 图标不渲染；CustomWindow 模板主 ContentPresenter 尺寸 0x0 → 整个窗口内容区空白。
   - 根因：**WPF 模板内 `<ContentPresenter/>` 自动显示 TemplatedParent.Content；Avalonia 12 无此机制**（反编译验证：`ContentControl.RegisterContentPresenter` 只做 presenter 注册，`ContentPresenter.Content` 是独立 styled property，TemplatedParentChanged 不做数据传递）。官方 Fluent 主题全部显式写 `Content="{TemplateBinding Content}"`。
   - 修复：`/data/user/work/fix_cpresenter.py` 批量给模板内裸 ContentPresenter 补 `Content="{TemplateBinding Content}"`。已修 7 个文件：Window.axaml、MainWindow.axaml、CommitUserControl.axaml、QuickLaunchWindow.axaml、GitMmStart/Sync/UploadWindow.axaml。
   - **全仓尚有约 67 处同类未修**（扫描脚本见下，主要在 Theme/Styles/*.axaml 的各控件模板里，当前未造成崩溃但内容显示空白）。**下一个 agent 优先跑这个脚本处理剩余文件**（先备份，跑完构建验证）：
     ```bash
     cd /data/user/work/migration/ForkPlus-Next
     python3 /data/user/work/fix_cpresenter.py src/ForkPlus/Theme/Styles/*.axaml
     # 注意脚本会跳过 x:Name="PART_SelectedContentHost"（TabControl 内部机制填充的特例）
     ```
6. **IMultiValueConverter 的 UnsetValue 强转崩溃**（本轮2）：Avalonia MultiBinding 子绑定未解析时传 `Avalonia.UnsetValueType`（WPF 传 null），`(SolidColorBrush)values[0]` 直接 InvalidCastException。修复：`TabEllipseVisibilityConverter.cs` / `TabEllipseFillBrushConverter.cs` 改用 `as` + 宽松 bool 解析（try Convert.ToBoolean，失败按 false）。**模式：所有 IMultiValueConverter 的 Convert 必须防御 values 里出现 UnsetValueType/DoNothing/null**。全仓扫描命令：
   ```bash
   grep -rln "IMultiValueConverter" src/ForkPlus --include="*.cs" | xargs grep -lE '\(SolidColorBrush\)values\[|\(bool\)values\['
   # 当前输出为空（已全修），新迁移文件再出现时按同模式修
   ```

**冒烟方法**（验证 UI 渲染）：
```bash
export DISPLAY=:99   # 需先 Xvfb :99 -screen 0 1920x1080x24
timeout 30 dotnet run --project src/ForkPlus/ForkPlus.csproj 2>&1 | grep -c "Unhandled"  # 预期 0
# 截图（import 来自 imagemagick）：import -window root /tmp/ui.png
```

## 运行时修复链 3（2026-08-29 本轮3：仓库打开链路全通）

上一轮终点：打开仓库后永远停在"正在加载..."（toolbar 显示 loading、侧栏/主区全空）。逐个排查修复：

1. **【根因A】ClosableTabControl 选卡事件断链**：
   - WPF `TabControl.OnSelectionChanged` 是**框架调用的虚方法重写**，转换后变成普通方法（Avalonia 无此虚方法）→ 永不被调 → `SelectedTabItemChanged` 事件永不触发 → `TabManager.TabControl_SelectedTabItemChanged`（排队仓库刷新任务）整条链路断裂。
   - **修复**：构造函数里 `base.SelectionChanged += (s,e) => OnSelectionChanged(e)`，订阅 Avalonia 路由事件转发到原方法（保留 `StopSelectionChangedEventWhileDropInProgress` 门控）。**模式：WPF 框架回调虚方法（OnXxx 重写）迁移后全部是死代码，必须找到等价事件显式接回。**
2. **【根因B】XAML EndInit 期早期 SelectionChanged NRE（SidebarUserControl）**：
   - Avalonia `SelectingItemsControl` 在 XAML `EndInit`（XamlIlPopulate 过程中）就初始化选区并触发 SelectionChanged；WPF 首次选卡发生在 Load 之后（x:Name 字段已赋值）。早期触发时 `ServiceTabItem`/`ServiceRadioButton`/`BranchesTabItem` 均为 null → `UpdateVisibleTabs` NRE 崩掉开仓链路。
   - **修复**：`TabControl_SelectionChanged` 开头加 null 守卫直接 return（此时 `_repositoryData` 还是 Empty 无业务意义；真实数据到达后 `UpdateRepositoryData` 会再调 `UpdateVisibleTabs`）。**模式已在修复链 2 #3 出现过（RepositoryDetailsUserControl 同款）——XAML 事件处理器一律假设字段未初始化。**
3. **【根因C】DataTemplateKey 按类型查找失效（侧栏树渲染 ToString 类型名）**：
   - WPF：资源字典里 `x:Key="{DataTemplateKey {x:Type T}}"` 的 DataTemplate 可被 ContentPresenter 按类型隐式命中；转换后 key 降级成字符串，**Avalonia 12 ContentPresenter 只走 `FindDataTemplate`（逻辑树 Control.DataTemplates → 全局 IGlobalDataTemplates），资源字典字符串 key 永不命中** → 侧栏树/引用徽章/ReferencePanel 全渲染 ToString()。
   - **修复**：`move_dtkey.py` / `move_sidebar_dt.py` 脚本把 DataTemplate 搬进 `App.axaml` `<Application.DataTemplates>`（IGlobalDataTemplates，FindDataTemplate 最后兜底）。共迁移：Sidebar.axaml 13 个（SidebarGroupItem/Remote/FilterableRemote/Folder/FilterableFolder/Truncate/Tag/LocalBranch/MainWorktree/RemoteBranch/Stash/Submodule/Worktree SidebarItem）+ Listview.axaml 9 个（ReferencesDataTemplates 的 Tag/LocalBranch/RemoteBranch/BisectMark/Stash ReferenceViewModel + ReferencePanelStyle ItemTemplate 的 ReferencePanel{Tag,LocalBranch,RemoteBranch,BisectMark}ViewModel）。
   - **模板绑定属性已逐项核对存在**（BranchViewModel.BorderBrush/BackgroundBrush、HighlightableTextBlock.Text/HighlightString、RemoteIcon: IImage、ReflogName 等），画刷资源（RevisionList.*/RevisionSummary.*/Reference*）在 Brushes.axaml 均有定义。
   - **已知丢失**：WPF DataTrigger 视觉状态（IsActive 加粗、IsWorktree 工作树图标、BisectGood 换色）无模板级等价物，TODO 注释保留原片段，待后续 VM 计算属性 + Classes 选择器补齐。
4. **ReferencePanelStyle ControlTheme 修复（KeyNotFoundException）**：
   - 转换误加 `BasedOn="{StaticResource {x:Type controls:ReferencePanel}}"`（WPF 原版无 BasedOn）→ Avalonia 无对应隐式 ControlTheme → KeyNotFoundException。已移除；并补 `Template`（WPF 依赖内置 ItemsControl 默认模板，Avalonia ControlTheme 无模板则条目不渲染）→ `<ItemsPresenter/>`。
5. **SelectableTextBlock 反射崩溃（TypeInitializationException）**：
   - 原实现反射调 PresentationFramework 内部 TextEditor（WPF-only）让 TextBlock 支持选择/复制；Avalonia 无此类型，Type.GetType null → 静态 cctor NRE（开仓库页签时 CommitUserControl → FilePathTextBlock → 此类）。**修复**：直接继承 `Avalonia.Controls.SelectableTextBlock`（自带选择/复制），删全部反射包装，`Focusable = true`。

**验证方法**（本轮实际执行路径）：`Xvfb :98` + `interact4.sh`（launch/list/shot/click/type/key 分阶段驱动）→ 依次点"继续"→"确定"→"完成"→"打开" → 每步 `import -window` 截图 → 与 verification/ 编号截图对比。应用进程存活 + /tmp/run_log.txt 无 Unhandled = 通过。

## 运行时修复链 4（2026-08-30 本轮4：提交列表渲染）

上轮终点：仓库打开成功但提交列表空白（.nvm 有 1 个提交）。期间曾靠诊断代码里的 ManualRefresh（反射调 `ItemsPresenter.Refresh`）渲染成功过一次——清理诊断后回归空白，说明自动链仍有断点。逐层定位修复：

1. **【根因·终局】RevisionsDataSource 枚举器与 Count 契约违背**（一行修复恢复整条链）：
   - 原实现：`IEnumerator IEnumerable.GetEnumerator() => _decoratedRevisions.GetEnumerator()`（仅"已物化"行，`Reload` 里 `_decoratedRevisions = new List<DecoratedRevision>(Count)` 后为空）；`Count => _visualGraph.Count`（=1）；`IList.this[i] => GetDecoratedRevisionAtRow(i)`（惰性分页物化）。
   - **WPF 为何正常**：WPF ItemContainerGenerator 生成容器走 `Count + IList 索引器`（索引器按需物化，WPF 虚拟化），**从不枚举** → 契约违背潜伏无影响。
   - **Avalonia 12 为何断**：`PanelContainerGenerator.OnItemsChanged` 的 Reset 分支 `Add(0, ItemsView)` → `foreach (object item in items)`（`ItemCollection.GetEnumerator → Source.GetEnumerator`）→ 枚举到空列表 → 0 容器 → 列表空白。
   - **修复**：GetEnumerator 改为 `for (i=0; i<Count; i++) yield return GetDecoratedRevisionAtRow(i)`（与索引器语义对齐）。**模式：自定义 IList 数据源迁移后，务必校验 GetEnumerator 与 Count/索引器的一致性——WPF 框架不枚举，Avalonia 枚举，潜伏契约违背会浮出。**
   - **[probe] 实证链**（排除法收敛过程，可直接复用）：① Changed 阶段探针（`Items.CollectionChanged`）确认弱事件链通、viewCount=1；② 反射快照确认生成器在、Panel 在、`PostCollectionChanged` 订阅在（注意 `_postCollectionChanged` 声明在基类 `ItemsSourceView`，反射必须 `GetType().BaseType.GetField`）；③ 反射挂自己的处理器到内部 `PostCollectionChanged`（`GetAddMethod(nonPublic:true).Invoke`，`AddEventHandler` 会拒 internal 事件）——订阅序在生成器之后，触发即证明生成器处理器执行完且无异常，但 `panelChildrenAfterGenerator=0` → 断定 foreach 枚举 0 项 → 收敛到源集合契约。
2. **GridView → ItemTemplate 行模板重建**：WPF `ListView.View=GridView`（CellTemplate 列体系）在 Avalonia 无等价物；将注释保留的 CellTemplate 转成两个 `DataTemplate`（SingleRowRevisionTemplate 8 列 Grid / DoubleRowRevisionTemplate 双行 Grid），`RefreshRevisionListViewTemplate` 按 `GetAvailableWidth()>500` 切换 `ListBox.ItemTemplate`。
3. **容器生成虚方法修正**（DragAndDropListView/MultiselectionListView/DragAndDropListBox/MultiselectionTreeView）：WPF `GetContainerForItemOverride`/`IsItemItsOwnContainerOverride` 在 Avalonia 12 由 `CreateContainerForItemOverride(item,index,recycleKey)`/`NeedsContainerOverride` 取代——原非 override 的 WPF 方法名是死代码，容器落到默认 ListBoxItem，`PrepareContainerForItemOverride` 强转 null（改 `?.` 防 NRE）。
4. **GraphCellView 渲染期布局**：WPF 版在 `OnRender` 里 `Width = cellWidth * lines.Length`；Avalonia 渲染期使布局失效抛 `InvalidOperationException("Visual was invalidated during the render pass")` → 移到 `MeasureOverride` 返回期望尺寸，`OnDataContextChanged` 时手动失效量测（容器复用换绑）。
5. **TextField/RevisionSubjectTextField Inlines 触发链**：WPF `DependencyProperty.Register(PropertyChangedCallback→RefreshInlines)` 转换丢回调 → Inlines 永不填充 → 主题文本空白。`GetObservable(...).Subscribe(AnonymousObserver<string>(RefreshInlines))` 补回（StringValue/HighlightString 基类 + IsParentSelected/HasBody 派生类）。注意 AnonymousObserver 显式泛型（匿名方法不能直转 IObserver）。
6. **模板内 ContentPresenter ContentTemplate 补绑**：WPF ContentPresenter 自动继承 ContentTemplate；Avalonia 需显式 `ContentTemplate="{TemplateBinding ContentTemplate}"`（fix_ct_template2.py 批量，注释感知版）。
7. **MultiselectionTreeViewItemCollection 已排查无恙**：Count/枚举器同源于 `_items`，与 RevisionsDataSource 不同源，无契约违背。

## 运行时修复链 5（2026-08-30 本轮5：跨平台 P/Invoke 崩溃簇）

上轮终点：仓库能打开但细节交互路径里埋着多个 Windows-only P/Invoke，Unix 上一触即崩（DllNotFoundException）。逐个排查修复（fork.log 路径 `/root/.local/share/ForkPlus/logs/fork.log`）：

1. **【崩溃源1】NaturalStringComparer.StrCmpLogicalW（shlwapi.dll）**：
   - 活路径：`RepositoryReferences.New` 排序引用 → `RefreshRepositoryData` 整体失败 → 主界面永久"加载中"。**这是上轮"仓库加载失败"的真凶之一**。
   - 修复：`OperatingSystem.IsWindows()` 分支，Unix 走 `NumericIgnoreCaseStringComparer.CompareLogicalOrdinalIgnoreCase`（纯托管"逻辑排序"等价实现：数字段按数值比较）。与 `NumericIgnoreCaseStringComparer.cs` 同一处理模式（该文件本轮也补了平台守卫）。
2. **【崩溃源2】FileSizeFormatter.StrFormatByteSize（shlwapi.dll）**：
   - 活路径：BinaryContentUserControl（二进制差异视图）、FileHelper、GitLfsProgressHandler（LFS 进度）。
   - 修复：Unix 走 `FormatManaged`：1024 进制、3 位有效数字（<10 两位小数/<100 一位/其余取整），输出风格与 Windows 一致（"18 bytes"/"1.85 KB"/"1.18 MB"）。
3. **【崩溃源3】IconTools.SHGetFileInfo（shell32.dll）**：
   - 活路径：点击提交行 → RevisionDetails 文件列表建图标缓存 → 崩溃整个应用。
   - 修复：Unix 无系统图标 API，`GetIconForFile` 返回 null；`GetImageSourceForExtension` 提供 `BinaryFile.png` 内置占位图标（LRU 缓存）。后续可按 freedesktop 图标主题（xdg-icon-resource）实现按扩展名取真实图标。
4. **【崩溃源4】MouseHelper.GetCursorPos（user32.dll）**：
   - 活路径：Treemap.UpdateTooltipPosition（鼠标悬停提示）、DragAndDropListViewItem.OnGiveFeedback（拖放跟随）。
   - **踩坑记录（重要，后续 agent 勿重复探索）**：先尝试订阅 `Avalonia.Input.InputManager.Instance.Process`（IObservable<RawInputEventArgs>）跟踪指针事件——编译报 CS0122/CS1061。用 MetadataLoadContext 反射 ref 程序集实证：`InputManager` 类是 **internal**（`public=False`）、`RawInputEventArgs.Root` 与 `RawPointerEventArgs.Position` 均为 **protected**（公开属性集为空）——**Avalonia 12 没有公开的全局光标位置 API**。
   - 最终方案：X11 `XQueryPointer`（P/Invoke libX11.so.6，ldconfig 确认存在）：`XOpenDisplay(IntPtr.Zero)` 开独立连接读 `$DISPLAY`，`XDefaultRootWindow` 取根窗口，`XQueryPointer` 的 rootX/rootY 即屏幕像素坐标——与 `GetCursorPos` 语义完全等价的**硬件级查询**，不依赖 Avalonia 内部状态，**拖放进行中（指针事件被拖放循环接管）时依然有效**（订阅方案覆盖不了的场景）。macOS 暂返回 (0,0)（后续可补 NSEvent.mouseLocation）。
   - 反射查证 API 可见性的工具：`/data/user/work/apicheck2`（MetadataLoadContext + PathAssemblyResolver，注意 coreDir 要用 `Path.GetDirectoryName(typeof(object).Assembly.Location)` 提供 core assembly）。
5. **【崩溃源5】OpenFileInDefaultEditorCommand（ShellExecute/Shlwapi）**：
   - 修复：Unix 用 `Process.Start(new ProcessStartInfo(path){ UseShellExecute = true })` 打开（xdg-open 语义）。
6. **【本轮冒烟实证】（1920×1280，全链路零异常）**：
   - 启动 → "Git 版本过旧"对话框（窗口几何 `xdotool getwindowgeometry --shell` 定位 + 读图定按钮中心 (553,126) 点击）→ 主窗口 → **自动恢复上次 .nvm 会话**（无需重新打开仓库）→ 提交列表/分支图/标签徽章/侧栏分支标签远程子模块树全渲染 → 点击提交行（精确坐标：裁剪读图定位行高 30px，行3中心 (500,250)）→ 蓝色高亮 + 详情面板完整填充（作者 Jordan Harband <ljharb@gmail.com>/日期/完整 SHA/父提交/说明/13 文件树）。
   - 截图：verification/21-commit-select-details-1920x1280.png + 21a-repo-restored-1920x1280.png。

## pass7 修复记录（103→1，错误清单已全部消灭）

原 A-I 分组的 103 个错误已全部修复，关键修复模式（后续 agent 遇同类问题直接套用）：

**C# 侧（定向手改，XAML 编译器对类型要求）**：
- **typed property（AVLN3000 "doesn't inherit from AvaloniaProperty<T>"）**：`CustomWindow.cs` 的 5 个属性字段从 `AvaloniaProperty` 收紧为 `StyledProperty<double>/<bool>/<Thickness>`——XAML 里 `{Binding ui:CustomWindow.Xxx}` 引用属性时编译器要求 typed 字段。
- **RegisterAttached → Register**：`HighlightableTextBlock.cs`、`TextField.cs` 原转换用 `RegisterAttached<..., AvaloniaObject, ...>`，XAML 属性元素语法 `<controls:X.HighlightString><Binding/></...>` 解析不了附加属性形式；改为普通 `AvaloniaProperty.Register<控件, string>` + 实例 CLR 属性包装。
- **事件签名（AVLN3000 空消息 = 签名错配）**：`Window_Closing` 改 `WindowClosingEventArgs`、`Slider_ValueChanged` 改 `RangeBaseValueChangedEventArgs`（RangeBase.ValueChanged）。

**XAML 侧（xamlpass7.py 批量 + 手改）**：
- **ItemContainerTheme 块内 ControlTheme 不能带 x:Key** → 删块内 x:Key（AVLN3000 "No suitable setter or adder for ItemContainerTheme"）。
- **`{DynamicResource {XxxTemplateKey}}` 双花括号** → `{DynamicResource XxxTemplateKey}`（内层花括号被当类型解析，AVLN3000 "Unable to convert :.{Unknown type}"）。
- **ControlTheme 无 x:Key（无 TargetType 匹配的 Theme 查找）** → 加 `x:Key="{x:Type Xxx}"`。
- **RepeatButton.IsVisible 含 MenuScrollingVisibilityConverter 块** → 整块替换为 `False`。
- **`IsEnabled="{TemplateBinding IsMouseOver}"` on ScrollViewer 模板部件** → 删（ScrollBar 无 IsMouseOver 属性，AVLN2000）。
- **`BasedOn="{StaticResource {x:Type ListView}}"`** → `{x:Type ListBox}`（Avalonia ListView 不存在，继承链基于 ListBox）。
- **TextBox 子类模板缺 `PART_TextPresenter`（AVLN2205）** → CommandTextBox 模板加隐藏 `<TextPresenter x:Name="PART_TextPresenter" IsVisible="False"/>`。
- **`SelectionMode="Extended"`** → `"Multiple"`；**PasswordBox** → TextBox + `PasswordChar="●"`；**`VerticalAlignment="top"`** → `"Top"`（枚举区分大小写）；**`Height="Auto"` on 非 GridLength 属性** → 删。
- **误删回滚教训**：pass4 曾按错误行删 `HorizontalContentAlignment`，但错误行号漂移误删了 18 处 ContentControl 派生元素上的合法用法；`fix_hca.py` 恢复（仅 ListBox/MultiselectionTreeView 上的删除保留，那两类确实无此属性）。**批量删属性时必须限定元素类型白名单**。


## XAML 编译静默失败（关键机制，必读）

**现象**：`dotnet run` 报 `XamlLoadException: No precompiled XAML found for ForkPlus.UI.MainWindow`，即使 .axaml 文件存在、x:Class 正确、!AvaloniaResources 正确嵌入 dll。

**根因**（已反编译验证）：
1. Avalonia 12 的 `AvaloniaXamlLoader.Load(object)` 是**必定抛异常的桩**（反编译 Avalonia.Markup.Xaml.dll 确认）。
2. 正常流程：XAML 编译器（`CompileAvaloniaXamlTask`）编译 .axaml 后**重写主程序 IL**，把 `AvaloniaXamlLoader.Load(this)` 调用替换成编译好的填充方法。
3. 本仓库有 ~1198 个去重 AVLN 错误（3172 条带重复计），XAML 编译器**中止了 IL 重写**，但**不使 build 失败**（AVLN 错误被降级，`dotnet build` 仍报 0 Error）。
4. 结果：dll 里 **0 个 `CompiledAvaloniaXaml.*` 类型**，所有 `InitializeComponent()` 运行时全炸。

**验证方法**：
```bash
# 触发完整 XAML 重编译（增量构建会跳过，必须 Rebuild 或先改文件）
dotnet build -t:Rebuild -v n -nologo 2>&1 | grep "error AVLN" | wc -l

# 检查 dll 里是否有编译产物（0 = XAML 编译失败）
ilspycmd -l c bin/Debug/net10.0/ForkPlus.dll | grep -c CompiledAvaloniaXaml
```

**错误分布**（本轮修复后剩 ~390 去重，重点文件）：
| 文件 | 主要问题 |
|---|---|
| Theme/Styles/Tabcontrol.axaml | TabPanel 未解析（43 处，全仓最大簇）、ControlTheme 嵌套问题 |
| Theme/Styles/Menu.axaml | ComponentResourceKey 未解析（25 处全在此 + Window.axaml）、ControlTheme 直接放 ResourceDictionary |
| UI/UserControls/GitMmUserControl.axaml | FocusVisualStyle/AllowDrop 等属性未解析 |
| UI/Dialogs/InteractiveRebaseWindow.axaml | 同类属性簇 |

**剩余错误类别**（2026-08-29 最新统计，按唯一消息数，共 390；原始日志 964 行含跨工程重复）：
- AVLN2000 类型未解析（160）：`TabPanel`(86，Tabcontrol.axaml:57/410 + GitMmUserControl.axaml:54 两大簇) / `ComponentResourceKey`(50，全在 Menu.axaml:590-619) / `Hyperlink`(12) / `ListViewItem`(10) / `ListView`(6) / `Stylus`(6) / `StaticExtension`(6)
- AVLN2000 属性未解析（~250）：`FocusVisualStyle`(58) / `Name`(46，x:Name 挂在 TranslateTransform/RowDefinition 等非 StyledElement 上) / `AllowDrop`(18) / `TextDecorations`(16，TextBlock 主题里) / `Resources`(16，Style/ControlTheme 上) / `HorizontalContentAlignment`(14)/`VerticalContentAlignment`(12) / `StaysOpen`(10)/`PopupAnimation`(10)/`AllowsTransparency`(10) / `Increase/DecreaseRepeatButton`(20，Track) / `View`(8，NoUIAutomationListView) / `Uid`(8) / `PreviewKeyDown`(10) / 零散（ZIndex/TabIndex/PlacementRectangle/ToolTip/Margin/TargetType/Style/Foreground）
- `Cannot find 'IsSelectionActive'`(26+16)：ListBoxItem/TreeViewControlItem 模板里 TemplateBinding 引用 WPF-only 属性 → 改删该 TemplateBinding
- AVLN2200（48）：值转换 `{Unknown type}`——`ClickMode="Hover"`（Menu.axaml:79，Avalonia ClickMode 无 Hover）、`BasedOn="{StaticResource {x:Type Hyperlink}}"`（Hyperlink 类型不存在）、AllowDrop Setter
- AVLN1000（36）：`'Auto'` 传数字属性(16)、`IsVisible="Collapsed"/"Hidden"` 值未跟着转换(20)
- AVLN3000（120）：`SelectionMode` 传 String(14+14，WPF "Extended"→Avalonia 无此值)、ControlTheme 直接放 ResourceDictionary 子元素、Popup Placement 字符串、`Avalonia.UnsetValueType`(22)/`BindingBase`(20) 参数不匹配（多为 XAML 编译器级联报错，上游修掉后会消失）
- AVLN2005（16）：`Unable to parse "NaN" as a grid length`——pass3 把 RowDefinition/ColumnDefinition 的 Auto 误转成 NaN，须转回
- AVLN2205（10）：`PART_TextPresenter` 未定义于 PlaceholderTextBox（模板缺部件）+ AVLN2203（8）：重复 Setter（VerticalContentAlignment/Height 重复声明，删一个）

**修复策略**：
1. 先修 Theme/Styles/*.axaml（错误乘数最高——每个错误会被所有引用它的入口文件重复报告）
2. WPF Trigger/Storyboard/EventSetter 块整体删除或转 Avalonia 选择器（`:pointerover`/`:pressed`/`:focus` 等）；转换器本应注释掉的块残留了
3. 每修一批就 Rebuild 验证 `CompiledAvaloniaXaml` 类型数 > 0，最终目标是全部 XAML 编译通过

## XAML 批量修复方法论（xamlpass1-7，已验证有效）

对 1198 个去重 AVLN 错误逐个手改不现实。已验证的批量路径：**用 Python 脚本处理 .axaml 文本，每次 Rebuild 收集新错误清单再迭代**。脚本在 `/data/user/work/xamlpass1.py` ~ `xamlpass7.py`，要点：

1. **XML 注释状态机**（所有 pass 共用）：XAML 里有 `<!-- ... -->` 注释块（含多行 Storyboard 注释化产物），绝不能把注释里的内容再改一遍。`split_active_regions()` 按 `<!--`/`-->` 切分，只处理非注释区。注释体内含 `--` 会破坏 XML，删除时用不带 `--` 的说明文本。
2. **错误驱动**：`dotnet build -clp:ErrorsOnly` 输出 → tee 到 /tmp/build_outN.txt → 脚本解析 `path(line,col): error AVLN2000: ...` 建立 `{file: {line: [(kind, detail)]}}` 索引 → 只改报错行。盲改全仓风险高。
3. **各 pass 覆盖**：
   - pass1：Trigger/Condition 块、WPF-only Setter 删除
   - pass2：Storyboard 块注释化、ResizeGrip 删、EventSetter 删、SystemCommands 属性删、事件名/签名错配（扫 code-behind 的 handler 参数类型反推）
   - pass3：核心 API 映射 —— `Visibility`→`IsVisible`（值转换）、`ToolTip`→`ToolTip.Tip`、`ListView`→`ListBox`/`ListViewItem`→`ListBoxItem`、`Auto`→`NaN`（区分 GridLength）、`PART_ContentHost` ScrollViewer→TextPresenter(PART_TextPresenter)、Track 的 `Increase/DecreaseRepeatButton`→`Increase/DecreaseButton`、Popup `Placement="Mouse"`→`"Pointer"`
   - pass4：错误驱动单行修复（按行号定位删 Setter/属性）+ 控件级整块处理
4. **验证闭环**：`dotnet build src/ForkPlus.sln -clp:ErrorsOnly 2>&1 | tee /tmp/build_outN.txt` → 统计 `grep -oP 'error AVLN\d+' | sort | uniq -c`。每轮预期降 200-400。
5. **注意事项**：删除块时留下的 `<!-- TODO 迁移：... -->` 注释要单行（多行嵌套 `--` 会炸）；`git diff` 抽查再提交。

## 运行时冒烟已修复的问题

1. **IPC 命名管道消息模式（PlatformNotSupportedException）**：`IpcServer.cs:26` 的 `PipeTransmissionMode.Message` 仅 Windows 支持。协议本身用 4 字节长度前缀分帧（`PipeStreamExtensions.ReadString`），不依赖消息边界，已改为 `OperatingSystem.IsWindows() ? Message : Byte`。
2. **沙盒无显示服务器**：`apt-get install -y xvfb` 后 `Xvfb :99 -screen 0 1920x1080x24` + `export DISPLAY=:99` 即可跑 GUI 冒烟。

## 下一步行动（按优先级，2026-08-30 本轮5 更新）

1. ~~提交选中与详情联动~~ ✅ **已完成**（本轮5 实证：verification/21-commit-select-details-1920x1280.png，蓝色高亮 + 详情面板完整填充 + 13 文件树）。
2. ~~侧栏分支/标签树数据验证~~ ✅ **已完成**（本轮5 读图实证：侧栏分支/标签/远程/子模块分组树正常渲染）。
3. **次级窗口冒烟**：右键文件 → FileHistoryWindow / BlameWindow / SideBySideMergeWindow 打开验证（这三个窗口本轮改过构造链）。
4. **菜单命令执行**：File 菜单展开 → 命令触发（退出/打开仓库/Preferences 对话框）。
5. **DataTrigger 视觉状态补齐**：IsActive 加粗 / IsWorktree 图标 / BisectGood 换色（原片段已注释保留在 App.axaml 模板旁），用 VM 计算属性 + Classes 选择器实现。
6. **性能 TODO**：当前提交列表 ItemsPanel 为非虚拟化 StackPanel，GetEnumerator 全量物化在大仓库有代价 → 切 VirtualizingStackPanel（Avalonia 虚拟化面板走 ItemContainerGenerator 按可见范围生成，兼容惰性源）。
7. **FlaUI 替换**：`ForkPlus.AutomationTests` 用了 FlaUI.UIA3（NU1701，net461 兼容包），在非 Windows/Avalonia 下不可用，需评估替换或隔离。
8. **WpfCompat 死代码清理**：`RemoveContextMenuOpeningHandler` 等空实现、`Freeze` 直通方法等，编译已过但语义是占位的，运行时验证后决定补实现还是删。
9. **macOS 光标位置**：MouseHelper 的 X11 方案在 macOS 返回 (0,0)，后续补 NSEvent.mouseLocation（P/Invoke libAppKit 或 ObjCRuntime）。

## 本轮新增的已验证修复模式（57→0 直接套用）

21. **WPF `Rect.X/Y/Width/Height` 属性赋值** → Avalonia `Rect` 是不可变结构体，整体 `new Rect(x, y, w, h)` 重建。
22. **`GetHighlightBrush` 返回 `IBrush`** → 接收变量/参数类型从 `Brush` 改 `IBrush`（Avalonia 绘制 API 均收 IBrush）。
23. **WPF `new MouseWheelEventArgs(...)` 事件转发** → Avalonia 12 构造器需 rootVisual 等复杂参数，**直接复用原事件参数**：先 `e.Handled = false` 再 `target.RaiseEvent(e)`，最后恢复 `e.Handled = true`。
24. **`OnMouseLeftButtonUp` 里调 `base.OnMouseLeftButtonDown`（WPF 拖选技巧）** → Avalonia 需合成 `PointerPressedEventArgs(this, e.Pointer, root, e.GetPosition(root), e.Timestamp, e.Properties, e.KeyModifiers, 1)`（root 用 `TopLevel.GetTopLevel(this)` 取）再调 `base.OnPointerPressed`。
25. **AvaloniaEdit `WeakEventManagerBase<TMgr,TSrc>.AddListener`（WPF 式）** → AvaloniaEdit 12 是 4 泛型 + `AddHandler(source, handler)`：`TextViewWeakEventManager.ScrollOffsetChanged.AddHandler(textView, handler)`（注意命名空间 `AvaloniaEdit.Rendering`，TextView 的 ScrollOffsetChanged 是 `EventHandler`）。
26. **`IsVisibleChanged` 事件** → `_textEditor.GetPropertyChangedObservable(global::Avalonia.Visual.IsVisibleProperty).Subscribe(e => handler(...))`（lambda 包装，方法组不能直接转 IObserver）。
27. **`TextEditor.Viewport.Height`** → AvaloniaEdit 12 直接有 `ViewportHeight`/`ViewportWidth`/`ExtentHeight`/`ExtentWidth` double 属性。
28. **`SearchPanel.Install(TextArea)`** → `Install(this)`（收 TextEditor，不是 TextArea）。
29. **`Clipboard.SetText` 静态** → `global::ForkPlus.UI.WpfCompat.Clipboard.SetText`（兼容层）；**实例 `Clipboard.SetTextAsync(s)`** → 加 `using Avalonia.Input.Platform;`（Avalonia 12 把它挪到 `ClipboardExtensions` 扩展方法了）。
30. **`TransformToAncestor(ancestor).Transform(p)`** → `var m = c.TransformToVisual(ancestor); p2 = m?.Transform(p) ?? default`（返回 `Matrix?`）。
31. **`DrawRoundedRectangle(brush, pen, rect, rx, ry)`** → `DrawRectangle(brush, pen, rect, rx, ry)`（Avalonia 12 DrawRectangle 自带圆角重载）。
32. **`DrawImage(imageSource: x, rectangle: y)` 命名参数** → 位置参数 `DrawImage(x, y)`（2 参重载存在，参数名是 source/rect）。
33. **`BitmapImage(new Uri("avares://..."))`** → `new global::Avalonia.Media.Imaging.Bitmap(global::Avalonia.Platform.AssetLoader.Open(uri))`（全仓统一模式）。
34. **`PointCollection`** → `new System.Collections.Generic.List<global::Avalonia.Point>`（PolyLineSegment.Points 是 `IList<Point>`）。
35. **`FormattedText(..., pixelsPerDip)` 7 参** → 去掉第 7 参（Avalonia 无 DPI 缩放参）。其余 6 参 (text, culture, flowDirection, typeface, fontSize, brush) 签名兼容。
36. **`Visual is Run` 模式匹配（CS8121）** → Avalonia 的 Run 是 Inline 非 Visual，指针事件源永远不会是 Run。分支删除，改为检查遍历元素 `Control { DataContext: XxxViewModel }`。
37. **`PushClip(RectangleGeometry)` + `using (... : null)` 条件表达式（CS0173）** → `PushGeometryClip(geometry)`；PushedState 是 struct，`: null` 改 `: default(global::Avalonia.Media.DrawingContext.PushedState)`（default.Dispose() 判空安全，无副作用）。
38. **`Visibility != 0`（WPF 枚举 int 比较）** → `!IsVisible`（bool）。
39. **`BitmapSource.PixelHeight`** → `Bitmap.PixelSize.Height`。
40. **`IImage.CanFreeze`** → 删除（Avalonia 无 Freezable，Bitmap 天然不可变）。
41. **`InsertLayer(x as InputElement, ...)`** → 参数是 `Control`，cast 到 `global::Avalonia.Controls.Control`。
42. **对话框 `ShowDialog(this)`（FileDialog shim）** → shim 的 `ShowDialog()` 无参（自动取活动窗口）。
43. **`SplitTextDiffControl.ScrollToVerticalOffsetCompat`** → 控件自带 `ScrollToVerticalOffset(double)` 实例方法，直接调。
44. **AutoML 误写 `global::Avalonia.Media.IImage = x`** → 是属性赋值被写成了类型名，恢复 `ImageSource = x`（转换器把 WPF 属性名 ImageSource 误映射）。
45. **`AdornerLayer` 二义性（CS0104）** → 文件同时 using 了 `Avalonia.Controls.Primitives` 与 `ForkPlus.UI.WpfCompat`，显式写 `global::ForkPlus.UI.WpfCompat.AdornerLayer`。
46. **字段加错位置**：`typeface/emSize` 原基类字段补声明时加进了嵌套 struct → 移到外围类的实例字段区。

## 早期已验证的修复模式（1-20）

1. **WPF `new Window { Owner = x, WindowStartupLocation = CenterOwner }.ShowDialog()`** → 
   `new Window(...).SetOwnerAndCenter(x).ShowDialog().GetValueOrDefault()`（链式扩展，`WindowOwnerCompat` in `WpfCompat.Batch2.cs`）。
   注意 LFS 场景的 `list[i].Owner = value` 是 ViewModel 字符串属性，不是 Window Owner，直接赋值即可。
2. **`global::Avalonia.Controls.WindowState = ...`（CS0118）** → 自动转换把属性名写成了全限定类型名，恢复为 `WindowState = ...`。
3. **WPF `FileDialog`** → `Microsoft.Win32` 命名空间 shim（`WpfCompat.FileDialogs.cs`，基于 StorageProvider 阻塞等待）。`OverwritePrompt` 已知无对应（Avalonia 12 无 `ShowOverwriteConfirmation`）。
4. **`ItemsControl.ContainerFromElement(itemsControl, element)`（静态双参）** → 扩展方法实例调用 `(sender as ListBox)?.ContainerFromElement(e.Source as Visual)`。
5. **可视树遍历（最常见）**：`GetVisualParent` 需要 `Visual` 接收者。WPF `DependencyObject` 循环改写为：
   ```csharp
   global::Avalonia.Visual dependencyObject = args.Source as global::Avalonia.Visual;
   while (dependencyObject != null && !(dependencyObject is ListBoxItem))
       dependencyObject = global::Avalonia.VisualTree.VisualExtensions.GetVisualParent(dependencyObject);
   ```
   注意 `Run`（Documents.Inline）的 `Parent` 返回 `Inline`，需 `as Visual` 中转。
6. **`ButtonBase`** → Avalonia 12 里用 `global::Avalonia.Controls.Button.ClickEvent`（不在 Primitives）。
7. **`OverrideMetadata`（CS0122/私有）** → 移除静态构造器中的调用，改构造函数里设属性（如 `Focusable = true`）或附加属性。
8. **`Keyboard.Focus(ctrl)`** → `ctrl.Focus()`（已有的 Focus 调用保留一个即可，勿重复）。
9. **`Clipboard.SetText(s)`** → `Clipboard.SetTextAsync(s).GetAwaiter().GetResult()`（IClipboard 无 SetText）。
10. **`Dispatcher.BeginInvoke(priority, action)`** → `Dispatcher.Post(action, priority)`（参数顺序相反）。
11. **`MenuItem.IsCheckable = true`** → `ToggleType = global::Avalonia.Controls.MenuItemToggleType.CheckBox`。
12. **`ContentPresenter`** → `global::Avalonia.Controls.Presenters.ContentPresenter`（Presenters 命名空间）。
13. **`(DataTemplate)Resources["Key"]`** → `Resources["Key"] as global::Avalonia.Controls.Templates.IDataTemplate`。
14. **WPF 密码框方法被误转成静态**：`global::Avalonia.Controls.TextBox.Focus()` → `PasswordBox.Focus()`（实例方法）。
15. **`TranslatePoint` 返回 `Point?`** → `?? new Point(0.0, 0.0)` 兜底。
16. **`Formatt`edText 7 参构造不存在** → Avalonia 用 `FormattedText(text, fontFamily, fontSize, textStyle, foreground)`（5 参），pixelsPerDip 已并入。
17. **双击/单击事件 `MouseButtonEventArgs`** → `TappedEventArgs`；对应 helper 需要 `TappedEventArgs` 重载。
18. **WPF Label XAML 已改 TextBlock**：C# 里签名 `Label` → `global::Avalonia.Controls.Control` 放宽。
19. **`StartDrag(DependencyObject, ...)`** → `StartDrag(global::Avalonia.Input.InputElement, ...)`（DoDragDrop 需要 InputElement）。
20. **`BringIntoView()`** → Avalonia 原生 `ControlExtensions.BringIntoView(control)`（`Avalonia.Controls`），无需 shim；扩展方法裸调用报 CS7036 时加 `this.` 接收者。

## 重大发现（必读，避免重复排查）

1. **OxyPlot：用 `OxyPlot.Avalonia` 2.1.0-avalonia11，xmlns 是 `http://oxyplot.org/avalonia`**（不是 `/wpf`）。控件名与 WPF 一致（`PlotView`）。
   - 转换工具当初因引用缺失把所有 `<oxy:PlotView>` 元素**注释成 TODO 块**；引用补齐后必须把这些注释块恢复为真实元素，否则源生成器不生成 `LinePlot` 等字段，连锁产生大量 CS0103。
   - 已恢复 `StatisticsUserControl.axaml` 的 5 个 PlotView（39077cc）。**全仓还有其他文件可能存在同类注释块**，如果再遇到「x:Name 在 XAML 里存在但 CS0103 找不到」，先查 `TODO(wpf2avalonia)` 注释块。

2. **Avalonia NameGenerator 只为 `StyledElement` 派生类生成 x:Name 字段**。`TranslateTransform`、`ColumnDefinition`、`RowDefinition` 等非 StyledElement 的 x:Name **不会生成字段**，必须手动声明并从可视树取值。已修 4 处，模式如下：
   ```csharp
   // 字段声明（与 XAML x:Name 同名）
   internal global::Avalonia.Media.TranslateTransform TitleContainerTranslateTransform;
   // 构造函数 InitializeComponent() 之后取值
   TitleContainerTranslateTransform = (global::Avalonia.Media.TranslateTransform)TitleContainer.RenderTransform;
   ```
   已修：`StatusUserControl`（TranslateTransform）、`SideBySideMergeWindow`（2 个 ColumnDefinition，按 ColumnDefinitions 索引取）、`CodeEditorSearchPanelUserControl` / `RevisionSearchPanelUserControl`（根 Border 的 RenderTransform）。

3. **WPF → Avalonia 的关键 API 映射**（WpfCompat 层已实现的部分见 `src/ForkPlus/UI/WpfCompat/`）：
   - `RoutedCommand`、`Adorner/AdornerLayer`、`IWeakEventListener`、`ContextMenuEventHandler`、`WindowChrome`、`WindowInteropHelper`、`WpfDataObject`（双模式：读 DataTransfer / 写 SetData）等均已 shim。
   - `IScrollInfo` → `Avalonia.Controls.IScrollable`（Extent/Viewport 为 Size）。
   - `Typeface` 构造：3 参（family, style, weight）；`FormattedText` 构造：无 pixelsPerDip。
   - `ListCollectionView`：用 `WpfCompat.ListCollectionView`（支持 Filter + Reset 通知）。

4. **Avalonia 12 剪贴板破坏性变更**：`IClipboard` 接口只剩 `ClearAsync/FlushAsync/SetDataAsync/TryGetDataAsync`；`SetTextAsync`/`TryGetTextAsync` 挪到了 `Avalonia.Input.Platform.ClipboardExtensions` 静态扩展方法。实例调用 `Clipboard.SetTextAsync(s)` 只需补 `using Avalonia.Input.Platform;`。

5. **AvaloniaEdit 12（TextEditor/AvalonEdit 移植）与 WPF AvalonEdit 的差异**：
   - `WeakEventManagerBase` 从 WPF 的 2 泛型 `AddListener(source, IWeakEventListener)` 变成 4 泛型 `<TMgr, TSrc, THandler, TArgs>` + `AddHandler(source, handler)`。
   - `TextView` 有 `ScrollOffsetChanged`（EventHandler）普通事件，直接 `+=` 也行。
   - TextEditor 有 `ViewportHeight/ViewportWidth/ExtentHeight/ExtentWidth` 直达属性。
6. **`PipeTransmissionMode.Message`（Linux PlatformNotSupportedException）**：非 Windows 平台 NamedPipe 不支持消息模式。协议若自带分帧（如本项目的 4 字节长度前缀）直接降级 `PipeTransmissionMode.Byte` 即可，用 `OperatingSystem.IsWindows()` 分支。

7. **【本轮最重要】Avalonia implicit ControlTheme 的 StyleKey 匹配机制（反编译验证）**：
   - 控件查找隐式 ControlTheme 的链路：`GetEffectiveTheme()` → `Theme` 属性非 null 则直接用 → 否则 `TryFindResource(StyleKey)` **以 StyleKey 为 key 精确匹配资源字典**。
   - `StyleKey = StyleKeyOverride`，基类 `Window` 已声明 `=> typeof(Window)`。**自定义控件基类的 ControlTheme（`x:Key="{x:Type MyControlBase}"`）若想被派生类隐式命中，基类必须 override `StyleKeyOverride => typeof(MyControlBase)`**，否则派生类的 StyleKey 还是 `typeof(Window)`，与 `{x:Type MyControlBase}` key 对不上——症状是模板静默退化为 ContentControl 默认模板、所有 PART 部件找不到（OnApplyTemplate 里 GetTemplateChild 全 null）。
   - 这类问题**编译期零报错**，且 `Theme` 属性值正确（`Theme="{DynamicResource Xxx}"` 的资源能找到），极具迷惑性。诊断方法：`OnApplyTemplate` 里打印 `Template.GetType().Name`（默认模板是 `FuncControlTemplate`）+ `GetVisualDescendants(this).Count()`（默认模板只有 1 个 ContentPresenter）。
   - 排查工具链（已验证有效）：`ilspycmd`（`~/.dotnet/tools`，注意 `DOTNET_ROOT=$HOME/.dotnet` 必须设）反编译 `~/.nuget/packages/avalonia/12.1.1/lib/net8.0/Avalonia.Base.dll` 看 `StyledElement.GetEffectiveTheme/ApplyControlTheme` 实现。

8. **Avalonia 样式应用时序**（`ApplyStyling` 调用点，反编译）：`EndInit()`（XAML 加载完）、`OnAttachedToLogicalTreeCore`、`Layoutable.MeasureCore`（每次布局测量）。**XAML 事件（如 SelectionChanged）可能在 EndInit 期间触发**——早于构造函数后续语句，code-behind 处理器必须容忍字段未初始化。

9. **【本轮2 最重要】模板内 ContentPresenter 不会自动继承 TemplatedParent.Content**：
   - WPF：`<ControlTemplate TargetType="Button"><ContentPresenter/></ControlTemplate>` 裸 presenter 自动显示 Button.Content。
   - Avalonia 12：**必须显式 `Content="{TemplateBinding Content}"`**（官方 Fluent 主题的写法，GitHub 源码可查：`src/Avalonia.Themes.Fluent/Controls/Button.xaml`）。裸 ContentPresenter 的 Content 为 null → 渲染空白。
   - 唯一特例：TabControl 模板的 `<ContentPresenter x:Name="PART_SelectedContentHost"/>` 由内部机制填充（官方也不写绑定）。
   - 症状识别：控件"占位但空"（如按钮有背景无边框内图标）、模板主内容区 0x0。
   - wpf2ava 转换器**不会**自动补这个绑定，全仓 74 处模板受影响（已修 7 文件，剩约 67 处在 Theme/Styles/*.axaml）。

10. **MultiBinding 初始化期传 UnsetValueType**：子绑定未解析时 WPF 传 null，Avalonia 传 `Avalonia.UnsetValueType`（结构体）。所有 IMultiValueConverter 的强转（`(SolidColorBrush)values[0]`、`(bool)values[1]`）在模板初始化期必炸。防御模式：`as` 类型转换 + `is bool` 模式匹配 / try-Convert。

## 修复方法论（已验证有效）

1. **先 shim 后改代码**：缺类型/缺成员优先往 `src/ForkPlus/UI/WpfCompat/` 加 shim（按现有分文件模式：`WpfCompat.cs` / `WpfCompat.Controls.cs` / `WpfCompat.More.cs` / `WpfCompat.Batch2.cs`），一次 shim 消掉一簇错误。
2. **机械重写用 fixer**：`/data/user/work/fixer/Program.cs` 是 Roslyn 重写器（已跑 4 轮 pass），同类改动手写正则容易错，往 fixer 加规则跑一遍更稳。
3. **小步提交**：每消掉一簇就 `git add -A && git commit && git push`，绝不攒大招（上下文丢失风险）。
4. **XAML 字段类错误先查生成器产物**：`-p:EmitCompilerGeneratedFiles=true` 后看 `.g.cs` 是否含该字段，不含则是元素类型不是 StyledElement 或元素被注释。
5. **查 API 用 apicheck 而不是猜**：`cd /data/user/work/apicheck && dotnet run --no-build -- "TypeName" [成员过滤]`；查接口成员/构造器签名/扩展方法都行。构造器参数顺序拿不准时反射 dump（参考 /tmp/checkns 一次性脚本思路）。

## 迁移守则

- 转换工具可同步增强（在 WpfToAvalonia 仓提交），修一处回填一处映射
- 模板触发器被注释化处（XAML-TEMPLATE-TRIGGER-ORPHAN ×28 等）在编译清零后按页面恢复
- 每个手动修改处保留 `// TODO 迁移：` 注释说明原委，便于回溯
- bin/obj 不入库（.gitignore 已配置）
- **每次推送前刷新本文档**（当前状态/错误轨迹/修复模式），为后续 agent 提供指导
