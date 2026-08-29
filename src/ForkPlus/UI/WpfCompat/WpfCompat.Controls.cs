// WPF → Avalonia 迁移兼容层 第二部分：控件/输入/系统级 shim 与扩展
// 全部通过 ForkPlus.csproj 的 <Using Include="ForkPlus.UI.WpfCompat" /> 全局引入，
// 迁移期尽量少改业务代码；每处均带 TODO 迁移标记，后续替换为原生实现。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace ForkPlus.UI.WpfCompat
{
    // ===== 应用级：WPF WpfApp.MainWindow / .Windows =====

    /// <summary>
    /// WPF WpfApp.MainWindow / WpfApp.Windows 的等价物。
    /// Avalonia 把窗口集合放在 IClassicDesktopStyleApplicationLifetime 上。
    /// </summary>
    public static class WpfApp
    {
        public static IClassicDesktopStyleApplicationLifetime Lifetime
            => Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;

        public static Window MainWindow => Lifetime?.MainWindow;

        public static IReadOnlyList<Window> Windows => (IReadOnlyList<Window>)Lifetime?.Windows ?? Array.Empty<Window>();

        public static void Shutdown(int exitCode = 0) => Lifetime?.Shutdown(exitCode);

        /// <summary>排除 self 后的"活动窗口"（做 ShowDialog 的 owner 用）。</summary>
        public static Window ActiveWindow(Window self)
        {
            var wins = Windows.Where(w => w != self && w.IsVisible).ToList();
            return wins.FirstOrDefault(w => w.IsActive) ?? wins.FirstOrDefault();
        }
    }

    /// <summary>
    /// WPF Window.ShowDialog()（无参、阻塞、返回 bool?）的同步等价物。
    /// Avalonia 的 ShowDialog 是 async + 必须传 owner；这里用 Dispatcher.PushFrame
    /// 跑嵌套消息循环 —— 与 WPF ShowDialog 的实现方式一致（模态期间 UI 线程继续泵消息）。
    /// TODO 迁移：新代码请直接用 await ShowDialog(owner)。
    /// </summary>
    public static class WindowDialogCompat
    {
        public static bool? ShowDialog(this Window self)
        {
            if (self == null) return null;
            var owner = WindowOwnerCompat.TryGetOwner(self) ?? WpfApp.ActiveWindow(self);
            if (owner == null)
            {
                // 没有可用 owner（启动早期）：退化为非模态 Show，立即返回 null
                self.Show();
                return null;
            }
            Task<bool?> task = self.ShowDialog<bool?>(owner);
            if (task.IsCompleted) return task.Result;
            var frame = new DispatcherFrame();
            task.ContinueWith(_ => Dispatcher.UIThread.Post(() => frame.Continue = false),
                TaskScheduler.Default);
            Dispatcher.UIThread.PushFrame(frame);
            return task.Status == TaskStatus.RanToCompletion ? task.Result : null;
        }
    }

    // ===== 字体族：WPF FontWeights / FontStyles / FontStretches =====

    public static class FontWeights
    {
        public static FontWeight Thin => FontWeight.Thin;
        public static FontWeight ExtraLight => FontWeight.ExtraLight;
        public static FontWeight UltraLight => FontWeight.UltraLight;
        public static FontWeight Light => FontWeight.Light;
        public static FontWeight SemiLight => FontWeight.SemiLight;
        public static FontWeight Normal => FontWeight.Normal;
        public static FontWeight Regular => FontWeight.Regular;
        public static FontWeight Medium => FontWeight.Medium;
        public static FontWeight DemiBold => FontWeight.DemiBold;
        public static FontWeight SemiBold => FontWeight.SemiBold;
        public static FontWeight Bold => FontWeight.Bold;
        public static FontWeight ExtraBold => FontWeight.ExtraBold;
        public static FontWeight UltraBold => FontWeight.UltraBold;
        public static FontWeight Black => FontWeight.Black;
        public static FontWeight Heavy => FontWeight.Heavy;
        public static FontWeight ExtraBlack => FontWeight.ExtraBlack;
        public static FontWeight UltraBlack => FontWeight.UltraBlack;
    }

    public static class FontStyles
    {
        public static FontStyle Normal => FontStyle.Normal;
        public static FontStyle Italic => FontStyle.Italic;
        public static FontStyle Oblique => FontStyle.Oblique;
    }

    public static class FontStretches
    {
        public static FontStretch Normal => FontStretch.Normal;
        public static FontStretch Condensed => FontStretch.Condensed;
        public static FontStretch Expanded => FontStretch.Expanded;
        public static FontStretch SemiCondensed => FontStretch.SemiCondensed;
        public static FontStretch SemiExpanded => FontStretch.SemiExpanded;
        public static FontStretch ExtraCondensed => FontStretch.ExtraCondensed;
        public static FontStretch ExtraExpanded => FontStretch.ExtraExpanded;
        public static FontStretch UltraCondensed => FontStretch.UltraCondensed;
        public static FontStretch UltraExpanded => FontStretch.UltraExpanded;
    }

    // ===== Visibility：Avalonia 12 移除了 Visibility 枚举，只剩 bool IsVisible =====

    /// <summary>WPF System.Windows.Visibility。Collapsed/Hidden 都映射为 IsVisible=false。</summary>
    public enum Visibility
    {
        Visible = 0,
        Hidden = 1,
        Collapsed = 2,
    }

    public static class VisibilityCompat
    {
        public static T WithVisibility<T>(this T control, Visibility visibility) where T : Visual
        {
            SetVisibility(control, visibility);
            return control;
        }

        public static void SetVisibility(this Visual visual, Visibility visibility)
            => visual.IsVisible = visibility == Visibility.Visible;

        public static Visibility GetVisibility(this Visual visual)
            => visual.IsVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    // ===== ToolTip：Avalonia 用附加属性 ToolTip.Tip，C# 侧无实例属性 =====

    public static class ToolTipCompat
    {
        /// <summary>对象初始化器链式辅助：new Button { ... }.WithTip("xx")。</summary>
        public static T WithTip<T>(this T control, object tip) where T : Visual
        {
            ToolTip.SetTip((Control)control, tip);
            return control;
        }
    }

    // ===== 键盘 / 鼠标：WPF Keyboard / Mouse 静态类 =====

    /// <summary>
    /// WPF System.Windows.Input.ModifierKeys 的等价枚举。
    /// 数值与 Avalonia.Input.KeyModifiers 对齐（None=0, Alt=1, Control=2, Shift=4, Meta=8），
    /// 以便直接与 KeyModifiers 强转比较。
    /// </summary>
    [Flags]
    public enum ModifierKeys
    {
        None = 0,
        Alt = 1,
        Control = 2,
        Shift = 4,
        Windows = 8,
    }

    /// <summary>
    /// WPF System.Windows.Input.Keyboard 静态类 shim。
    /// FocusedElement 经任意窗口的 FocusManager 查询；Modifiers TODO：Avalonia 无全局
    /// 键盘状态查询，当前返回基于最近一次 KeyDown 记录的值（由 PasteGuard 等安装点维护）。
    /// </summary>
    public static class Keyboard
    {
        // 全局记录最近一次 KeyDown 的修饰键（由 InstallGlobalKeyTracking 挂接）
        private static ModifierKeys _lastModifiers = ModifierKeys.None;

        public static void InstallGlobalKeyTracking(TopLevel topLevel)
        {
            topLevel.AddHandler(InputElement.KeyDownEvent, (s, e) =>
                _lastModifiers = (ModifierKeys)(int)e.KeyModifiers, RoutingStrategies.Tunnel);
        }

        public static InputElement FocusedElement
        {
            get
            {
                foreach (var w in WpfApp.Windows)
                {
                    var focused = w.FocusManager?.GetFocusedElement();
                    if (focused != null) return focused;
                }
                return null;
            }
        }

        public static ModifierKeys Modifiers => _lastModifiers;

        public static bool IsKeyDown(Key key) => false; // TODO 迁移：Avalonia 无全局按键状态
    }

    /// <summary>WPF System.Windows.Input.MouseButtonState。</summary>
    public enum MouseButtonState
    {
        Released = 0,
        Pressed = 1,
    }

    /// <summary>
    /// WPF System.Windows.Input.Mouse 静态类 shim。
    /// TODO 迁移：Avalonia 无全局鼠标位置查询，GetPosition 返回 (0,0)，需按事件参数逐点改造。
    /// </summary>
    public static class Mouse
    {
        public static Point GetPosition(Visual relativeTo) => new Point(0, 0);

        public static void SetCursor(InputElement element, Cursor cursor) => element.Cursor = cursor;

        public static MouseButtonState LeftButton => MouseButtonState.Released;
        public static MouseButtonState RightButton => MouseButtonState.Released;
        public static MouseButtonState MiddleButton => MouseButtonState.Released;
        public static void OverrideCursor(Cursor cursor) { }
    }

    // ===== 视觉树：WPF System.Windows.Media.VisualTreeHelper =====

    /// <summary>
    /// WPF System.Windows.Media.VisualTreeHelper 的等价物，基于
    /// Avalonia.VisualTree.VisualExtensions 实现。
    /// </summary>
    public static class VisualTreeHelper
    {
        public static int GetChildrenCount(Visual visual)
            => visual?.GetVisualChildren().Count() ?? 0;

        public static Visual GetChild(Visual visual, int index)
            => visual.GetVisualChildren().ElementAt(index);

        public static Visual GetParent(Visual visual)
            => visual.GetVisualParent();

        public static Visual GetAncestor(Visual visual, Type type)
            => visual.GetVisualAncestors().FirstOrDefault(a => type.IsInstanceOfType(a));

        public static T GetAncestor<T>(Visual visual) where T : class
            => visual.GetVisualAncestors().OfType<T>().FirstOrDefault();

        public static IEnumerable<Visual> GetDescendants(Visual visual)
            => visual.GetVisualDescendants();

        public static T GetDescendant<T>(Visual visual) where T : class
            => visual.GetVisualDescendants().OfType<T>().FirstOrDefault();

        public static Visual HitTest(Visual visual, Point point)
            => visual.GetVisualAt(point);

        /// <summary>WPF VisualTreeHelper.GetDpi。TODO 迁移：取控件实际 DPI（当前恒 1.0）。</summary>
        public static DpiScale GetDpi(Visual visual)
        {
            var tl = visual as TopLevel ?? TopLevel.GetTopLevel(visual);
            double sc = tl?.RenderScaling ?? 1.0;
            return new DpiScale(sc, sc);
        }

        /// <summary>WPF LogicalTreeHelper 常用成员顺手放这（逻辑树 = Avalonia 的 Parent 链）。</summary>
        public static StyledElement GetLogicalParent(StyledElement element) => element?.Parent as StyledElement;
    }

    // ===== 系统参数：WPF SystemParameters =====

    /// <summary>
    /// WPF SystemParameters shim。基于主窗口所在 Screen 提供 WorkArea / 屏幕尺寸。
    /// </summary>
    public static class SystemParameters
    {
        private static Avalonia.Platform.Screen PrimaryScreen
        {
            get
            {
                var mw = WpfApp.MainWindow;
                if (mw != null)
                {
                    var fromWindow = mw.Screens?.ScreenFromWindow(mw);
                    if (fromWindow != null) return fromWindow;
                }
                var any = WpfApp.Windows.FirstOrDefault();
                return any?.Screens?.Primary;
            }
        }

        /// <summary>主屏工作区（DIP）。PixelRect → Rect 换算按屏幕 Scaling。</summary>
        public static Rect WorkArea
        {
            get
            {
                var s = PrimaryScreen;
                if (s == null) return new Rect(0, 0, 1920, 1040);
                double sc = s.Scaling <= 0 ? 1 : s.Scaling;
                var wa = s.WorkingArea;
                return new Rect(wa.X / sc, wa.Y / sc, wa.Width / sc, wa.Height / sc);
            }
        }

        public static double PrimaryScreenWidth => PrimaryScreen?.Bounds.Width ?? 1920;
        public static double PrimaryScreenHeight => PrimaryScreen?.Bounds.Height ?? 1080;
        public static double FullPrimaryScreenWidth => PrimaryScreenWidth;
        public static double FullPrimaryScreenHeight => PrimaryScreenHeight;

        public const double MinimumHorizontalDragDistance = 4.0;
        public const double MinimumVerticalDragDistance = 4.0;
        public static Thickness WindowResizeBorderThickness => new Thickness(4);
        public static double HorizontalScrollBarButtonWidth => 17;
        public static double CaptionHeight => 32;

        /// <summary>WPF SystemParameters.SmallIconWidth/Height（16px 系统图标规格）。</summary>
        public static double SmallIconWidth => 16;
        public static double SmallIconHeight => 16;
        public static double HorizontalScrollBarHeight => 17;
        public static double VerticalScrollBarWidth => 17;
    }

    // ===== 剪贴板：WPF System.Windows.Clipboard =====

    /// <summary>
    /// WPF Clipboard 同步 API shim。经主窗口 TopLevel.Clipboard 异步实现，
    /// 用 PushFrame 同步等待（同 ShowDialog 思路）。
    /// </summary>
    public static class Clipboard
    {
        private static IClipboard GetClipboard() => WpfApp.Windows.FirstOrDefault()?.Clipboard;

        private static T Wait<T>(Task<T> task)
        {
            if (task.IsCompleted) return task.Result;
            var frame = new DispatcherFrame();
            task.ContinueWith(_ => Dispatcher.UIThread.Post(() => frame.Continue = false),
                TaskScheduler.Default);
            Dispatcher.UIThread.PushFrame(frame);
            return task.Status == TaskStatus.RanToCompletion ? task.Result : default;
        }

        public static string GetText() => Wait(GetClipboard()?.TryGetTextAsync()) ?? string.Empty;

        public static void SetText(string text) => GetClipboard()?.SetTextAsync(text);

        public static bool ContainsText()
        {
            var fmts = Wait(GetClipboard()?.GetDataFormatsAsync());
            return fmts?.Contains("Text") == true;
        }

        public static void Clear() => GetClipboard()?.ClearAsync();

        public static void SetFileDropList(System.Collections.IList files)
        {
            // TODO 迁移：文件列表写入剪贴板需要 IStorageItem，暂按文件名文本降级
            var text = string.Join(Environment.NewLine, files?.Cast<object>() ?? Enumerable.Empty<object>());
            if (text.Length > 0) SetText(text);
        }
    }

    // ===== 系统命令：WPF SystemCommands（窗口 chrome 命令，Avalonia 自绘标题栏直接调方法）=====

    public static class SystemCommands
    {
        private sealed class NoopCommand : System.Windows.Input.ICommand
        {
            public event EventHandler CanExecuteChanged { add { } remove { } }
            public bool CanExecute(object parameter) => true;
            public void Execute(object parameter) { }
        }

        public static System.Windows.Input.ICommand CloseWindowCommand { get; } = new NoopCommand();
        public static System.Windows.Input.ICommand MaximizeWindowCommand { get; } = new NoopCommand();
        public static System.Windows.Input.ICommand MinimizeWindowCommand { get; } = new NoopCommand();
        public static System.Windows.Input.ICommand RestoreWindowCommand { get; } = new NoopCommand();
        public static System.Windows.Input.ICommand ShowSystemMenuCommand { get; } = new NoopCommand();
    }

    // ===== WPF DataFormats 常量表（Avalonia 12 把 Avalonia.Input.DataFormats 标记为 error 级 obsolete）=====

    /// <summary>
    /// WPF System.Windows.DataFormats 的字符串常量。
    /// DnD 侧由 WpfDataObject 做格式名匹配（FileDrop ↔ Avalonia DataFormat.File）。
    /// </summary>
    public static class WpfDataFormats
    {
        public const string Text = "Text";
        public const string UnicodeText = "UnicodeText";
        public const string FileDrop = "FileDrop";
        public const string FileName = "FileName";
        public const string FileNameW = "FileNameW";
        public const string Csv = "Csv";
        public const string Html = "HTML Format";
        public const string Rtf = "Rich Text Format";
        public const string Bitmap = "Bitmap";
        public const string Xaml = "Xaml";
        public const string String = "System.String";
    }

    /// <summary>WPF System.Windows.ResizeMode 枚举 shim（Avalonia 12 Window 无 ResizeMode 属性，仅保留枚举语义）。</summary>
    public enum ResizeMode
    {
        NoResize = 0,
        CanMinimize = 1,
        CanResize = 2,
        CanResizeWithGrip = 3,
    }

    // ===== 动画枚举：WPF EasingMode / PopupAnimation =====

    /// <summary>WPF System.Windows.Media.Animation.EasingMode（Avalonia 用 Easing 类族替代）。</summary>
    public enum EasingMode
    {
        EaseIn = 0,
        EaseOut = 1,
        EaseInOut = 2,
    }

    /// <summary>WPF System.Windows.Controls.Primitives.PopupAnimation（Avalonia Popup 无动画枚举）。</summary>
    public enum PopupAnimation
    {
        None = 0,
        Fade = 1,
        Slide = 2,
    }

    /// <summary>EasingMode → Avalonia Easing 的映射辅助。</summary>
    public static class EasingCompat
    {
        public static Avalonia.Animation.Easings.Easing ToEasing(string kind, EasingMode mode)
        {
            string suffix = mode switch
            {
                EasingMode.EaseIn => "EaseIn",
                EasingMode.EaseInOut => "EaseInOut",
                _ => "EaseOut",
            };
            return Avalonia.Animation.Easings.Easing.Parse(kind + suffix);
        }
    }

    // ===== 像素格式：WPF System.Windows.Media.PixelFormats =====

    /// <summary>
    /// WPF PixelFormats shim（与 Avalonia.Platform.PixelFormats 同名，故起名 WpfPixelFormats，
    /// 由 GlobalUsings.cs 里 global using PixelFormats = ... 别名全局引入）。
    /// </summary>
    public static class WpfPixelFormats
    {
        public static global::Avalonia.Platform.PixelFormat Bgra32 => global::Avalonia.Platform.PixelFormat.Bgra8888;
        public static global::Avalonia.Platform.PixelFormat Pbgra32 => global::Avalonia.Platform.PixelFormat.Bgra8888;
        public static global::Avalonia.Platform.PixelFormat Rgb24 => global::Avalonia.Platform.PixelFormat.Rgb888;
        // Avalonia 12 无 Bgr24/Rgba64/Gray8/Bgr565，就近映射
        public static global::Avalonia.Platform.PixelFormat Bgr24 => global::Avalonia.Platform.PixelFormat.Rgb32;
        public static global::Avalonia.Platform.PixelFormat Rgba64 => global::Avalonia.Platform.PixelFormat.Rgba8888;
        public static global::Avalonia.Platform.PixelFormat Gray8 => global::Avalonia.Platform.PixelFormat.Bgra8888;
        public static global::Avalonia.Platform.PixelFormat Bgr565 => global::Avalonia.Platform.PixelFormat.Rgb565;
    }

    // ===== 资源查找：WPF TryFindResource(key) / SetResourceReference =====

    public static class ResourceCompat
    {
        /// <summary>WPF FrameworkElement.TryFindResource(object key)：找到返回资源，否则 null。</summary>
        public static object TryFindResource(this StyledElement element, object key)
        {
            if (element == null || key == null) return null;
            if (element.TryGetResource(key, element.ActualThemeVariant ?? ThemeVariant.Default, out var value))
                return value;
            return null;
        }

        /// <summary>WPF Application.Current.TryFindResource(key)（Application 不是 StyledElement，单独适配）。</summary>
        public static object TryFindResource(this Avalonia.Application app, object key)
        {
            if (app == null || key == null) return null;
            return app.TryGetResource(key, app.ActualThemeVariant ?? ThemeVariant.Default, out var value) ? value : null;
        }

        /// <summary>
        /// WPF FrameworkElement.SetResourceReference(prop, key)。
        /// TODO 迁移：当前为一次性解析赋值，不随主题切换动态更新；
        /// 如需动态更新可改绑 element.GetResourceObservable(key)（内部 API，需反射）。
        /// </summary>
        public static void SetResourceReference(this AvaloniaObject obj, AvaloniaProperty property, object key)
        {
            if (obj is StyledElement el)
            {
                var v = el.TryFindResource(key);
                if (v != null) obj.SetValue(property, v);
            }
        }
    }

    // ===== 模板子级：WPF Control.GetTemplateChild =====

    public static class TemplateCompat
    {
        /// <summary>
        /// WPF Control.GetTemplateChild(name)。模板应用后按 Name 在视觉树后代中查找。
        /// </summary>
        public static object GetTemplateChild(this TemplatedControl control, string name)
        {
            if (control == null || string.IsNullOrEmpty(name)) return null;
            return control.GetVisualDescendants()
                .OfType<StyledElement>()
                .FirstOrDefault(v => v.Name == name);
        }

        public static T GetTemplateChild<T>(this TemplatedControl control, string name) where T : class
            => control.GetTemplateChild(name) as T;
    }

    // ===== ContextMenuOpening 事件适配 =====

    public static class ContextMenuCompat
    {
        /// <summary>
        /// WPF control.ContextMenuOpening += (s, ContextMenuEventArgs e) 的安装器。
        /// Avalonia 12 无该路由事件，改挂 ContextMenu.Opening（CancelEventHandler）
        /// 并适配为 WpfCompat.ContextMenuEventArgs。
        /// </summary>
        public static void AddContextMenuOpeningHandler(this Control control,
            EventHandler<ContextMenuEventArgs> handler)
        {
            if (control?.ContextMenu == null) return; // TODO 迁移：XAML 需先给控件配 ContextMenu
            control.ContextMenu.Opening += (s, e) => handler(control, new ContextMenuEventArgs());
        }

        /// <summary>WPF control.ContextMenuOpening += new ContextMenuEventHandler(M) 形态。</summary>
        public static void AddContextMenuOpeningHandler(this Control control,
            ContextMenuEventHandler handler)
            => AddContextMenuOpeningHandler(control, (s, e) => handler(s, e));

        /// <summary>WPF control.ContextMenuClosing += ... 的安装器（挂 ContextMenu.Closing）。</summary>
        public static void AddContextMenuClosingHandler(this Control control,
            EventHandler<ContextMenuEventArgs> handler)
        {
            if (control?.ContextMenu == null) return;
            control.ContextMenu.Closing += (s, e) => handler(control, new ContextMenuEventArgs());
        }

        /// <summary>WPF control.ContextMenuClosing += new ContextMenuEventHandler(M) 形态。</summary>
        public static void AddContextMenuClosingHandler(this Control control,
            ContextMenuEventHandler handler)
            => AddContextMenuClosingHandler(control, (s, e) => handler(s, e));
    }

    // ===== Style 赋值适配：WPF control.Style = s（Avalonia Styles 只读集合）=====

    public static class StyleCompat
    {
        public static T WithStyle<T>(this T element, object style) where T : StyledElement
        {
            SetStyle(element, style);
            return element;
        }

        public static void SetStyle(StyledElement element, object style)
        {
            element.Styles.Clear();
            switch (style)
            {
                case Style s:
                    element.Styles.Add(s);
                    break;
                case System.Collections.IEnumerable en and not string:
                    foreach (var st in en)
                        if (st is Style s2)
                            element.Styles.Add(s2);
                    break;
            }
        }
    }

    // ===== ToggleButton Checked / Unchecked 事件适配 =====

    /// <summary>
    /// WPF x.Checked += h / x.Unchecked += h 的安装器。
    /// Avalonia ToggleButton 只有 IsCheckedChanged（EventHandler&lt;RoutedEventArgs&gt;），
    /// 这里按新旧值分派为 WPF 语义。
    /// </summary>
    public static class Events
    {
        public static void AddChecked(Control c, EventHandler<RoutedEventArgs> handler)
            => Subscribe(c, handler, wantChecked: true);

        public static void AddUnchecked(Control c, EventHandler<RoutedEventArgs> handler)
            => Subscribe(c, handler, wantChecked: false);

        private static void Subscribe(Control c, EventHandler<RoutedEventArgs> handler, bool wantChecked)
        {
            if (c == null || handler == null) return;
            bool lastChecked = (c as ToggleButton)?.IsChecked == true;
            c.GetObservable(ToggleButton.IsCheckedProperty).Subscribe(new ActionObserver<bool?>(v =>
            {
                bool nowChecked = v == true;
                if (nowChecked != lastChecked && nowChecked == wantChecked)
                    handler(c, new RoutedEventArgs { Source = c });
                lastChecked = nowChecked;
            }));
        }

        private sealed class ActionObserver<T> : IObserver<T>
        {
            private readonly Action<T> _onNext;
            public ActionObserver(Action<T> onNext) { _onNext = onNext; }
            public void OnCompleted() { }
            public void OnError(Exception error) { }
            public void OnNext(T value) => _onNext(value);
        }
    }

    // ===== DnD：WPF IDataObject 读取包装（Avalonia 12 IDataTransfer 适配）=====

    /// <summary>
    /// WPF e.Data（IDataObject）的只读 shim：包一层 DragEventArgs.DataTransfer。
    /// 支持 Text / FileDrop / 自定义字符串格式。
    /// </summary>
    public sealed class WpfDataObject
    {
        private readonly IDataTransfer _transfer;
        internal WpfDataObject(IDataTransfer transfer) { _transfer = transfer; }

        public bool GetDataPresent(string format)
            => _transfer?.Items?.Any(i => i.Formats.Any(f => Matches(f, format))) == true;

        public object GetData(string format)
        {
            var item = _transfer?.Items?.FirstOrDefault(i => i.Formats.Any(f => Matches(f, format)));
            if (item == null) return null;
            var fmt = item.Formats.First(f => Matches(f, format));
            return item.TryGetRaw(fmt);
        }

        /// <summary>WPF GetFileDropList：FileDrop 格式 → 文件路径集合。</summary>
        public System.Collections.IEnumerable GetFileDropList()
        {
            var raw = GetData(FileDropFormat);
            if (raw is IEnumerable<Avalonia.Platform.Storage.IStorageItem> items)
                return items.Select(f => f?.Path?.LocalPath).Where(p => p != null).ToList();
            if (raw is IEnumerable<string> paths && raw is not string)
                return paths.ToList();
            return Array.Empty<string>();
        }

        public IEnumerable<string> GetFileNames()
            => GetFileDropList().Cast<string>().ToList();

        private const string FileDropFormat = "FileDrop";

        private static bool Matches(DataFormat f, string wpfFormat)
        {
            if (f == null) return false;
            string n = null;
            try { n = f.ToSystemName(); } catch { }
            n ??= f.Identifier;
            n ??= "";
            return n == wpfFormat
                || (wpfFormat == FileDropFormat && (n == "File" || n == "FileDrop" || n == "application/x-vnd.ms-filedrop"))
                || (wpfFormat is "Text" or "UnicodeText" or "System.String" && n == "Text");
        }
    }

    public static class DragDropCompat
    {
        /// <summary>WPF e.Data → shim 包装。</summary>
        public static WpfDataObject WpfData(this DragEventArgs e) => new WpfDataObject(e?.DataTransfer);

        /// <summary>WPF e.GetPosition(relativeTo)（DragEventArgs 自带，Avalonia 也有，勿用）。</summary>
        public static void SetDragEffects(this DragEventArgs e, DragDropEffects effects) => e.DragEffects = effects;
    }
}
