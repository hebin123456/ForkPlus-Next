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

# 编译整个解决方案（7 个工程：主程序 + AskPass + RI + 4 个测试工程）
cd /data/user/work/migration/ForkPlus-Next/src && dotnet build ForkPlus.sln -v q -nologo

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

**🎉 里程碑：C# 编译全清零（2026-08-29）。整个解决方案 7 个工程 `dotnet build ForkPlus.sln` 全部成功，0 错误。**

**⚠️ 2026-08-29 运行时冒烟重大发现：XAML 编译静默失败（详见「XAML 编译静默失败」一节）。**

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
| （本轮） | C# 保持 0 | 运行时冒烟：IPC 管道修复；发现 XAML 编译静默失败（1198 去重错误为运行时阻塞项） |

| 阶段 | 状态 | 说明 |
|---|---|---|
| 0 基线导入 | ✅ 完成 | 全量转换产物入库 |
| 1 C# 编译清零 | ✅ **完成** | 主工程 + AskPass + RI + 4 个测试工程全部 0 错误 |
| 2 XAML (AVLN) 清零 | 🔴 **运行时阻塞项** | XAML 编译静默失败，AVLN 错误必须清零（见下文分析） |
| 3 运行时验证 | 🔄 进行中 | 已跑通：App 构造 + IPC；卡在：MainWindow XAML 加载 |
| 4 已知遗留 | 📋 见下文 | AutomationTests 的 FlaUI 依赖等 |

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

**错误分布**（去重后 ~1198 处，按文件）：
| 文件 | 错误数 | 主要问题 |
|---|---:|---|
| Theme/Styles/Listview.axaml | 66 | Trigger/Condition/EventSetter 等 WPF 结构 |
| Theme/Styles/Menu.axaml | 56 | 同上 |
| UI/UserControls/FileControlHeaderUserControl.axaml | 52 | 同上 |
| Theme/Styles/Tabcontrol.axaml | 45 | TabPanel 类型未解析等 |
| Theme/Styles/Window.axaml | 42 | ResizeGrip 未解析等 |
| UI/MainWindow.axaml | 35 | 见下文 |
| 其余 ~190 个文件 | 各 2-28 | 同类问题 |

**错误类别**：
- AVLN2000 "Unable to resolve type X"（最多）：WPF-only 类型残留在 XAML —— `ResizeGrip`、`TabPanel`、`Condition`、`Trigger`、`Storyboard`、`EventSetter`、`EasingDoubleKeyFrame`、`DataTemplateKey`
- AVLN2000 "Unable to resolve property X on type Y"：WPF 属性在 Avalonia 类型上不存在 —— `IsMouseOver`（→ `:pointerover` 选择器）、`HasContent`、`IsDefaulted`、`AllowsTransparency`、`ContentTemplateSelector`、`ContentStringFormat`、`Style.Resources`、`ControlTemplate.Resources`、`Border.Foreground`、`VisualBrush.Viewport/ViewportUnits`
- AVLN1000（18）：格式转换错误（如 `'Auto'` 传给数字属性）
- AVLN2200（60）：属性值无法转换
- AVLN3000（262）：需确认（疑似 x:DataType/绑定相关）

**修复策略**：
1. 先修 Theme/Styles/*.axaml（错误乘数最高——每个错误会被所有引用它的入口文件重复报告）
2. WPF Trigger/Storyboard/EventSetter 块整体删除或转 Avalonia 选择器（`:pointerover`/`:pressed`/`:focus` 等）；转换器本应注释掉的块残留了
3. 每修一批就 Rebuild 验证 `CompiledAvaloniaXaml` 类型数 > 0，最终目标是全部 XAML 编译通过

## 运行时冒烟已修复的问题

1. **IPC 命名管道消息模式（PlatformNotSupportedException）**：`IpcServer.cs:26` 的 `PipeTransmissionMode.Message` 仅 Windows 支持。协议本身用 4 字节长度前缀分帧（`PipeStreamExtensions.ReadString`），不依赖消息边界，已改为 `OperatingSystem.IsWindows() ? Message : Byte`。
2. **沙盒无显示服务器**：`apt-get install -y xvfb` 后 `Xvfb :99 -screen 0 1920x1080x24` + `export DISPLAY=:99` 即可跑 GUI 冒烟。

## 下一步行动（按优先级）

1. **XAML (AVLN) 错误清零**（运行时阻塞项，见上节策略）。修复顺序：Theme/Styles → 主窗口链路（MainWindow.axaml + CustomWindow + TabManager 相关）→ 其余窗口。可用 `dotnet build -t:Rebuild -v n 2>&1 | grep "error AVLN"` 实时统计。
2. **运行时冒烟继续**：XAML 清零前 MainWindow 无法加载。清零后重跑 `DISPLAY=:99 dotnet run` 逐个修运行时异常。
3. **FlaUI 替换**：`ForkPlus.AutomationTests` 用了 FlaUI.UIA3（NU1701，net461 兼容包），在非 Windows/Avalonia 下不可用，需评估替换或隔离。
4. **WpfCompat 死代码清理**：`RemoveContextMenuOpeningHandler` 等空实现、`Freeze` 直通方法等，编译已过但语义是占位的，运行时验证后决定补实现还是删。

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
