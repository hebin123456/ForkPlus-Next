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

# 编译主工程（在 /data/user/work/ForkPlus-Next/src/ForkPlus 下）
dotnet build --no-restore -v q -nologo 2>&1 | grep -E "error CS" | sed -E 's/ \[.*//' | sort -u

# 查看源生成器产物（调试 x:Name 字段问题时极其有用）
dotnet build --no-restore -v q -nologo -p:EmitCompilerGeneratedFiles=true
# 产物位于 obj/Debug/net10.0/generated/Avalonia.Generators/Avalonia.Generators.NameGenerator.AvaloniaNameIncrementalGenerator/

# git 推送（凭据已配置在 remote url 中）
git push origin HEAD
```

- 工作目录：`/data/user/work/ForkPlus-Next`（主仓库）、`/data/user/work/WpfToAvalonia`（转换工具）、`/data/user/work/fixer`（Roslyn 机械重写工具）
- 错误清单快照：`/data/user/work/errors.txt`（最近一次构建的 238 个唯一错误）

## 当前状态

**阶段 1（C# 编译清零）进行中：唯一错误数 238**（起始 186 是初次构建口径；按唯一错误去重统计后的轨迹如下）

| 提交 | 唯一错误数 | 内容 |
|---|---:|---|
| `0e16c1a` | ~970 | 迁移进度快照 + WpfCompat 扩展 |
| `51bb07f` | 912 | WpfCompat 兼容层自身编译清零 |
| `67f0b76` | 708 | 大批量机械修复 + 兼容层扩展 |
| `4db2c98` | 644 | 事件适配与绘制簇修复 |
| （未提交） | 526 | XAML 编译恢复（OxyPlot xmlns 修复） |
| `39077cc` | 252 | 恢复 PlotView 元素，字段生成恢复 |
| **本次** | **238** | 非 StyledElement 的 x:Name 字段手动补声明 |

| 阶段 | 状态 | 说明 |
|---|---|---|
| 0 基线导入 | ✅ 完成 | 全量转换产物入库 |
| 1 C# 编译清零 | 🔄 进行中 | 唯一错误 238 个，见下方分组 |
| 2 XAML (AVLN) 清零 | ⛔ 未开始 | 大部分 XAML 已能编译（PlotView 恢复后）；剩余 AVLN 警告待量化 |
| 3 附属工程收尾 | ⛔ 未开始 | AskPass / RI / 测试工程 |
| 4 运行时验证 | ⛔ 未开始 | 启动、渲染、交互冒烟 |

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

## 剩余 238 错误分组与策略（按修复优先级）

### A. 高频机械簇（约 100+ 处，性价比最高）

| 簇 | 数量 | 修复方式 |
|---|---:|---|
| `IBrush → Brush` | 7 | shim `BrushProxy` 或逐处加 `((Brush)x)` 包装；更简单：WpfCompat 加隐式转换扩展 |
| `InputElement → Control` / `AvaloniaObject → Visual/InputElement` | 9 | 逐处 cast（大部分是 sender 类型） |
| `PointerReleasedEventArgs → PointerPressedEventArgs` | 4+ | WPF 的 PreviewMouseLeftButtonDown 迁移后处理器签名错误，改绑 Avalonia 对应事件 |
| `TextBlock/ContentControl → Label` | 8 | 迁移工具把 Label 换成了别的控件，改回或加 cast |
| `DispatcherPriority ↔ Action` 参数顺序 | 4 | `Dispatcher.Invoke(priority, action)` 与 `(action, priority)` 参数顺序差异 |
| `Rect.X/Y/Width/Height` 只读赋值（CS0200） | 12 | Avalonia Rect 是不可变结构，改 `new Rect(x, y, w, h)` 重建 |
| `CS0118: X is a type but used like a variable` | 12 | 多为 `SomeType.SomeStaticMember` 迁移后错位，逐处看 |
| `StyledPropertyMetadata` 泛型参数（CS0305） | 12 | `new StyledPropertyMetadata<T>(...)` 需补泛型参数 |

### B. 缺失成员/属性 shim（约 40 处，往 WpfCompat 加扩展方法）

| 缺失 | 处数 | 建议 |
|---|---:|---|
| `ListBoxItem.IsPointerCaptured` | 4 | WpfCompat 扩展属性 |
| `SetOwnerCompat`（CustomWindow/LfsFileViewModel 上） | 4 | WpfCompat 里加 `WindowOwnerCompat.SetOwner(window, owner)`，Avalonia 用 `window.Owner = ownerWindow`（Show(owner) 已是主流） |
| `Popup.PopupAnimation/StaysOpen/AllowsTransparency/PlacementRectangle` | 4 | Popup shim 扩展（存值 + no-op） |
| `CommitDescriptionTextBox.SelectionLength` 等 TextBox 扩展 | 3 | `TextBoxCompat` 扩展（SelectionStart/SelectionEnd 计算） |
| `ItemsControl.DragOver/DragLeave/Drop` 事件 | 3 | `DragDropCompat.AddDragOverHandler(...)` 等 |
| `Control.FocusVisualStyleProperty` / `DefaultStyleKeyProperty` | 2 | no-op 附加属性 |
| `TappedEventArgs.IsClickedOnScrollbar` / `PointerEventArgs.LeftButton` / `ChangedButton` | 3 | shim 扩展方法 |
| 其余单发成员（OnTextChanged 虚方法、ToolTipOpening、IsVisibleChanged、Viewport、SnapsToDevicePixels 等） | ~15 | 逐处：事件用 Avalonia 等价事件或 WpfCompat 转发 |

### C. 集中在少数文件的深水区（先读文件再动手）

| 文件 | 错数 | 问题性质 |
|---|---:|---|
| `CenteredDockPanel.cs` | 12 | 自定义布局：ArrangeOverride/MeasureOverride 的 WPF 子元素遍历 API |
| `CustomWindow.cs` | 9 | 窗口 chrome 自绘 + StyledProperty 注册（CS0305） |
| `ForkPlusDialogWindow.cs` | 7 | 对话框基类：Owner/启动位置/OnActivated |
| `MergeCodeEditorBackgroundColorizer.cs` / `DiffBackgroundColorizer.cs` / `DiffLineNumberMargin.cs` | 15 | AvalonEdit 渲染层：DrawingContext/FormattedText/Typeface |
| `AutoCompleteTextBox.cs` | 5 | 自定义文本框（Popup + 键盘导航） |
| `ConfigureWorkspacesWindow.axaml.cs` | 5 | Window Owner + DataGrid |
| `CommitUserControl.axaml.cs` | 5 | 拖放 + 文本框 |
| `RevisionListViewUserControl.axaml.cs` / `SidebarUserControl.axaml.cs` | 10 | 列表控件事件 |

### D. 二义性/访问性（少量）

- `CS0104` 二义性引用 3 处（WpfCompat 类型与 Avalonia 内置重名，用全限定名或调整 using）
- `CS1540` protected 成员跨继承链访问 6 处（多在渲染基类，加 protected 转发或改调用）
- `CS0747` 初始化器成员错误 6 处（集合初始化器语法差异）

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
