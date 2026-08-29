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
| `3335377` | C# 0 / AVLN 1198 | IPC 管道修复 + 文档记录 XAML 编译静默失败根因 |
| `a64e8e2` | AVLN 1198→390 | xamlpass1-4 批量 XAML 修复（详见下节「XAML 批量修复方法论」） |
| `a526bfc` | 390 | docs: 390 错误模式分组 + pass4 未生效复盘 |
| （本轮） | AVLN 390→103 | xamlpass5-6：错误驱动精确修复（FocusVisualStyle 块删、ItemContainerTheme→Style、IsCheckable→ToggleType、PreviewKeyDown→KeyDown、ViewportWidth 等 TemplateBinding 簇、Resources on Style 删） |

**当前精确统计（2026-08-29）**：**103 唯一错误消息 / 92 build 汇总错误**。剩余错误已全部定位到 file:line，清单见下文「剩余 103 错误精确清单」。

| 阶段 | 状态 | 说明 |
|---|---|---|
| 0 基线导入 | ✅ 完成 | 全量转换产物入库 |
| 1 C# 编译清零 | ✅ **完成** | 主工程 + AskPass + RI + 4 个测试工程全部 0 错误 |
| 2 XAML (AVLN) 清零 | 🔄 **进行中** | 1198→390→103（唯一错误），剩余已逐条定位，见下文清单 |
| 3 运行时验证 | 🔄 进行中 | 已跑通：App 构造 + IPC；卡在：MainWindow XAML 加载（依赖阶段 2） |
| 4 已知遗留 | 📋 见下文 | AutomationTests 的 FlaUI 依赖等 |

## 剩余 103 错误精确清单（2026-08-29，已逐条定位）

**按修复策略分组**（后续 agent 按组处理即可，全部为 XAML 层面，无需动 C# 编译）：

### A. Style 选择器里的 WPF 属性（~20 处，删/改选择器）
- `[IsDefaulted=true]`：Button.axaml:119/562（WPF Button 默认按钮态，Avalonia 无 → 删整个 Style 块）
- `[HasContent=true]`：Button.axaml:446、Checkbox.axaml:45（→ 删块或改 `^:not(:empty)`，建议直接删）
- `[IsKeyboardFocused=False]`：Combobox.axaml:49/61（→ Avalonia 用 `:focus` 伪类，如 `^:not(:focus):pointerover`）
- `[IsEditable=true]`：Combobox.axaml:105/417 + Combobox.axaml:122/157/177/199 的 `<Binding Path="IsEditable">`（Avalonia ComboBox 无 IsEditable → IsEditable ComboBox 需后期单独实现，先删）
- `[IsSelectionActive]`：Textbox.axaml:27/62（→ 删）
- `[HasDropShadow=True]`：Commonresources.axaml:192/195（→ 删块）
- `[IsPressed]` Binding：Combobox.axaml:132/152/172 `<Binding Path="IsPressed">` Self（→ `:pressed` 伪类做不到 Binding，删这些 Setter 或改 TemplateBinding 等价物）
- `^[Role=TopLevelHeader]`/`^[Role=TopLevelItem]`/`^[Role=SubmenuHeader]`：Menu.axaml:609/617/624（Avalonia MenuItem 无 Role → 用 `/template/` 或 Menu 的 ItemContainerTheme 区分，先删）
- `^[ResizeMode=CanResizeWithGrip].../template/ ResizeGrip#ResizeGrip`：Window.axaml:208-210/262-264（ResizeGrip 不存在 → 删块）

### B. DynamicResource 里的 WPF TemplateKey（5 处，Menu.axaml）
`Value="{DynamicResource {SubmenuItemTemplateKey}}"` 这类（Menu.axaml:596/606/614/621/625）——XAML 把 `{SubmenuItemTemplateKey}` 当类型解析。修复：去掉内层花括号，改 `{DynamicResource SubmenuItemTemplateKey}` 字符串键；同时 Menu.axaml 内部定义模板处（原 ComponentResourceKey 定义）保持同名字符串键。若模板定义已删则引用处也要删。

### C. 事件处理器签名错配（4 处，XAML 引用 + code-behind 签名要改）
- MainWindow.axaml:1 `Closing="Window_Closing"` → code-behind 改 `EventHandler<WindowClosingEventArgs>`（WPF 是 CancelEventHandler）
- OnionSkinImageDiffUserControl.axaml:85 `ValueChanged="Slider_ValueChanged"` → 改 `EventHandler<RangeBaseValueChangedEventArgs>`
- InteractiveRebaseWindow.axaml:70（SizeChanged/DoubleTapped 簇）→ 对照 code-behind 签名逐个改
- EditCustomCommandUIControlsWindow.axaml:25（同上，Position 25,14 空消息错误）
- **通用法**：`dotnet build` 的 AVLN3000 空消息/EventHandler`1 错误 = 事件签名不匹配；去 code-behind 找 handler，把参数改成 Avalonia 事件参数类型

### D. 大小写/枚举值错（1 处）
- RemoveRemoteBranchWindow.axaml:21 `VerticalAlignment="top"` → `"Top"`（Avalonia 枚举解析区分大小写，WPF 不区分）

### E. HighlightString 用 `<Binding>` 赋 CLR 属性（2 处）
- SearchTabItem.axaml:23/71 `<controls:RevisionSubjectTextField.HighlightString><Binding Path="SearchString"/></...>`——HighlightString 是普通 CLR 属性收不了 Binding。修复：在控件 C# 里改成 `StyledProperty<string>`（TextBlock 的 HighlightableTextBlock / TextField 同理），或删该 property-element 块

### F. ControlTheme 缺 x:Key / 用错位置（4 处）
- Commonresources.axaml:77 `<ControlTheme TargetType="GridSplitter">` 无 x:Key → 加 `x:Key="{x:Type GridSplitter}"`
- Tabcontrol.axaml:29 `<ControlTheme TargetType="TabControl">` 无 x:Key → 同上
- InteractiveRebaseWindow.axaml:70 + EditCustomCommandUIControlsWindow.axaml:25：`<X.ItemContainerTheme><ControlTheme x:Key="{x:Type ...}">`——属性元素值里的 ControlTheme 不能带 x:Key → 删 x:Key

### G. 无对应属性的 Setter（~15 处，直接删 Setter + TODO 注释）
- `TextDecorations` on HyperlinkButton：Textblock.axaml:4/14/60/69/82/91/102/111（9 处，HyperlinkButton 无下划线概念 → 删；运行期下划线视觉损失可后续用 TextBlock 内容+TextDecorations 补）
- `HorizontalContentAlignment` on ListBox：Listview.axaml:46/50/114/158、StatisticsUserControl.axaml:50/114/158、GeneralUserControl.axaml:46（→ 删，或改 ItemsPresenter 的 HorizontalAlignment）
- `HorizontalContentAlignment` on MultiselectionTreeView：Multiselectiontreeview.axaml:209
- `CommandTarget` on RepeatButton：Scrollviewer.axaml:108/145
- `CalendarDayButtonStyle`/`CalendarButtonStyle` on Calendar：Calendar.axaml:239/240
- `PanningMode` on ScrollViewer：Scrollviewer.axaml:54
- `LayoutTransform` on ContextMenu：某文件:19
- `IsMainMenu` on Menu：Menu.axaml:45
- `HasDropShadow` Setter：Commonresources.axaml:174
- `Template` on TextBlock：Textblock.axaml:29/47（TextBlock 无 Template → 删 Setter；LfsLabel 需改用 ContentControl 或 Label 主题）
- `Viewport` on VisualBrush：某文件:2

### H. 'Auto' 传 double 属性（8 处）
- Button.axaml:666 `Height="Auto"` Setter → 删或 NaN
- Multiselectiontreeview.axaml:122/200、Scrollviewer.axaml:130/264 → 同类
- CloneWindow.axaml:64、EditRemoteWindow.axaml:90、NotificationBarUserControl.axaml:24 → 看具体属性（多为 Width/Height="Auto" → 删属性）

### I. 其余单点（~10 处）
- `SelectionMode="Extended"` → `"Multiple"`（Listview.axaml 某处）
- `PasswordBox` 类型不存在 → TextBox + `PasswordChar="●"`（Textbox.axaml:52 附近）
- `MenuScrollingVisibilityConverter`：Menu.axaml:119/156 → 删该 Converter 资源及引用
- `ListViewItem`/`ListView` TargetType 残留：Listview.axaml:42/53/91/127/172/196/206/238、Textblock.axaml 若干 → ListBoxItem/ListBox
- `PART_Indicator` on ProgressBar（1 处）：模板里加 `<Border x:Name="PART_Indicator">`
- `WindowResizeBorderThicknessProperty`/`HideMinimizeMaximizeButtonsProperty` typed property 错误：Window.axaml:8/163、MainWindow.axaml:8——XAML 里 `{Binding XxxClass.YyyProperty}` 引用了静态字段 → 改成属性名 `{Binding WindowResizeBorderThickness}`（无 Property 后缀）或 TemplateBinding
- `IsSelectionActive` on TextBox：Textbox.axaml:27/62

### 修复优先级
1. Theme/Styles/*.axaml 先修（错误乘数高）
2. 然后 MainWindow.axaml + Dialogs（单点）
3. 每修一批 `dotnet build` 验证，目标 0 AVLN 后跑运行时冒烟


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

## XAML 批量修复方法论（xamlpass1-6，已验证有效）

对 1198 个去重 AVLN 错误逐个手改不现实。已验证的批量路径：**用 Python 脚本处理 .axaml 文本，每次 Rebuild 收集新错误清单再迭代**。脚本在 `/data/user/work/xamlpass1.py` ~ `xamlpass6.py`，要点：

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

## 下一步行动（按优先级）

1. **XAML (AVLN) 错误继续清零**（103→0，运行时阻塞项）：全部错误已按修复策略分 A-I 组列在「剩余 103 错误精确清单」一节，逐组处理即可。xamlpass5/6 的脚本在 `/data/user/work/xamlpass5.py`、`/data/user/work/xamlpass6.py`，模式可复用。
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
