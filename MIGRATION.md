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
# dotnet 不在默认 PATH，必须先 export（沙盒环境）
export PATH="/opt/dotnet:$PATH"
export DOTNET_ROOT="/opt/dotnet"

# 编译主工程（在 /data/user/work/migration/ForkPlus-Next/src/ForkPlus 下）
dotnet build --no-restore -v q -nologo 2>&1 | grep -E "error CS" | sed -E 's/ \[.*//' | sort -u

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

**阶段 1（C# 编译清零）进行中：唯一错误数 57**（按唯一错误去重统计后的轨迹如下）

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

| 阶段 | 状态 | 说明 |
|---|---|---|
| 0 基线导入 | ✅ 完成 | 全量转换产物入库 |
| 1 C# 编译清零 | 🔄 进行中 | 唯一错误 57 个，集中在编辑器渲染层 |
| 2 XAML (AVLN) 清零 | ⛔ 未开始 | 大部分 XAML 已能编译（PlotView 恢复后）；剩余 AVLN 警告待量化 |
| 3 附属工程收尾 | ⛔ 未开始 | AskPass / RI / 测试工程 |
| 4 运行时验证 | ⛔ 未开始 | 启动、渲染、交互冒烟 |

## 本轮已验证的修复模式（后续 agent 直接套用）

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

## 剩余 57 错误分组与策略（按修复优先级）

### A. 编辑器渲染层（约 25 处，最大簇）

| 文件 | 错数 | 问题性质 |
|---|---:|---|
| `UI/Controls/Editor/Merge/MergeCodeEditorBackgroundColorizer.cs` | 6 | DrawingContext 绘制 + FormattedText 构造 |
| `UI/Controls/Editor/Diff/DiffBackgroundColorizer.cs` | 5 | 同上 |
| `UI/Controls/Editor/Diff/DiffLineNumberMargin.cs` | 4 | 行号渲染：FormattedText/Typeface |
| `UI/Controls/Editor/ChunkSelectionLayer.cs` | 3 | 自绘层 |
| `UI/Controls/FileDiffControl.cs` | 4 | ContextMenuEventArgs 事件签名 + PointerWheelEventArgs 构造 |
| `UI/Controls/FileContentControl.cs` | 2 | ContextMenuEventArgs 事件签名 |

**策略**：`FormattedText` 7 参构造（CS1729）需改 5 参签名；`ContextMenuEventArgs` 兼容层事件与 Avalonia `ContextRequestedEventArgs` 的 lambda 参数类型对不上（CS1661/CS1678），统一改用 Avalonia 原生事件签名；`PointerWheelEventArgs` 构造需要 `rootVisualPosition` 参数（CS7036）。

### B. 图片/二进制 diff（约 8 处）

| 文件 | 错数 | 问题性质 |
|---|---:|---|
| `UI/UserControls/BinaryDiff/OverlayImageControl.cs` | 3 | `Control.Background`（CS0117）+ `DrawingContext.PushedState` 条件表达式 + `RectangleGeometry → RoundedRect` |
| `UI/UserControls/BinaryDiff/BinaryContentUserControl.axaml.cs` | 3 | `Bitmap.PixelHeight`（CS1061，用 `Size`/`PixelSize`）+ `bool != int`（CS0019，WPF int 语义） |
| `UI/UserControls/BinaryDiff/ImageData.cs` | 1 | `IImage` 当变量用（CS0118） |

### C. 自定义控件（约 12 处）

| 文件 | 错数 | 问题性质 |
|---|---:|---|
| `UI/Controls/EditableTextBlock.cs` | 3 | 编辑态切换的成员缺失 |
| `UI/Controls/RevisionTimeLine.cs` | 2 | `PointCollection` 找不到（CS0246，Avalonia 用 `List<Point>`/`PolylineGeometry`）+ FormattedText |
| `UI/Controls/DragAndDropListViewItem.cs` | 2 | 拖放事件签名 |
| `UI/Controls/TreeViewControlItem.cs` | 1 | `OnPointerPressed(PointerReleasedEventArgs)` 签名错 |
| `UI/Controls/Editor/Hex/HexEditor.cs` | 2 | 自绘 |
| `UI/Controls/Editor/Merge/MergeLineNumberMargin.cs` | 1 | `IBrush → Brush` cast |
| `UI/Controls/Editor/FloatingButton.cs` | 1 | 单发 |
| `UI/Controls/Editor/Diff/CommitCodeEditor.cs` | 1 | 单发 |

### D. 对话框与散点（约 12 处）

| 文件 | 错数 | 问题性质 |
|---|---:|---|
| `UI/Dialogs/RepositoryOverviewWindow.axaml.cs` | 2 | `DrawImage(rectangle:)` 命名参数（CS1739）+ `DrawRoundedRectangle` 不存在（改 `DrawRectangle` + `PathGeometry` 或 `RoundedRect`） |
| `UI/Dialogs/EditRemoteWindow.axaml.cs` | 2 | `BitmapImage` 找不到（CS0246，Avalonia 用 `Bitmap` + `IStorageProvider` 加载） |
| `UI/Dialogs/InteractiveRebaseWindow.axaml.cs` | 1 | `TransformToAncestor`（CS1061，用 `TranslatePoint` 或 `TransformToVisual`） |
| `UI/Dialogs/ConfigureGitInstanceWindow.axaml.cs` | 1 | `ShowDialog(1)` 无重载（CS1501，Avalonia ShowDialog 需要 Task result 类型参数） |
| `UI/Dialogs/BlameWindow.axaml.cs` | 1 | `ScrollToVerticalOffsetCompat` 接收者类型不符（CS1929） |
| `UI/Dialogs/AiTextResultWindow.axaml.cs` | 1 | 已知修复模式 9（Clipboard.SetTextAsync） |
| `UI/Theme.cs` | 1 | `ImageSource` 找不到（CS0246） |
| `UI/Helpers/TextGuidelineHelper.cs` | 1 | FormattedText 7 参构造 |
| `UI/Helpers/ListViewScrollbarDoubleClickHelper.cs` | 1 | `Visual → Run` cast（CS0039，先 `is Run` 判断再转换） |
| `UI/Extensions/BridgeExtensions.cs` | 1 | `IImage.CanFreeze`（CS1061，Avalonia 无冻结概念，删掉该分支） |
| `UI/UserControls/RevisionListViewUserControl.axaml.cs` | 1 | `Visual` 模式匹配 `Run`（CS8121，先 `is Run` 判断） |

## 修复方法论（已验证有效）

1. **先 shim 后改代码**：缺类型/缺成员优先往 `src/ForkPlus/UI/WpfCompat/` 加 shim（按现有分文件模式：`WpfCompat.cs` / `WpfCompat.Controls.cs` / `WpfCompat.More.cs` / `WpfCompat.Batch2.cs`），一次 shim 消掉一簇错误。
2. **机械重写用 fixer**：`/data/user/work/fixer/Program.cs` 是 Roslyn 重写器（已跑 4 轮 pass），同类改动手写正则容易错，往 fixer 加规则跑一遍更稳。
3. **小步提交**：每消掉一簇就 `git add -A && git commit && git push`，绝不攒大招（上下文丢失风险）。
4. **XAML 字段类错误先查生成器产物**：`-p:EmitCompilerGeneratedFiles=true` 后看 `.g.cs` 是否含该字段，不含则是元素类型不是 StyledElement 或元素被注释。

## 迁移守则

- 转换工具可同步增强（在 WpfToAvalonia 仓提交），修一处回填一处映射
- 模板触发器被注释化处（XAML-TEMPLATE-TRIGGER-ORPHAN ×28 等）在编译清零后按页面恢复
- 每个手动修改处保留 `// TODO 迁移：` 注释说明原委，便于回溯
- bin/obj 不入库（.gitignore 已配置）
