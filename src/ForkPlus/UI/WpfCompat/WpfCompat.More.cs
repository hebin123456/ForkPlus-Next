// WPF → Avalonia 迁移兼容层 第三部分：动画/输入/绑定/树/杂项 shim
// 全部通过 GlobalUsings.cs 全局可见。带 TODO 迁移标记，收尾阶段逐个替换。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace ForkPlus.UI.WpfCompat
{
    // ===== VisualTreeHelper 补充成员 =====

    /// <summary>WPF DpiScale 结构。</summary>
    public struct DpiScale
    {
        public double DpiScaleX { get; }
        public double DpiScaleY { get; }
        public double PixelsPerDip => DpiScaleY * 96.0;
        public DpiScale(double scaleX, double scaleY) { DpiScaleX = scaleX; DpiScaleY = scaleY; }
    }

    // ===== SystemParameters 补充常量已合并到 WpfCompat.Controls.cs 的 SystemParameters 类 =====

    // ===== 绑定 =====

    /// <summary>WPF System.Windows.Data.Binding 的静态辅助（DoNothing）。</summary>
    public static class WpfBinding
    {
        /// <summary>WPF Binding.DoNothing：绑定引擎哨兵值。</summary>
        public static readonly object DoNothing = new object();
    }

    /// <summary>WPF System.Windows.Data.BindingOperations。</summary>
    public static class BindingCompat
    {
        public static void SetBinding(AvaloniaObject target, AvaloniaProperty property, Avalonia.Data.BindingBase binding)
            => target.Bind(property, binding);

        public static void ClearBinding(AvaloniaObject target, AvaloniaProperty property)
            => target.SetValue(property, AvaloniaProperty.UnsetValue);
    }

    // ===== WPF ApplicationCommands / ComponentCommands =====

    /// <summary>
    /// WPF System.Windows.Input.ApplicationCommands shim。
    /// Paste/Copy/Cut/SelectAll 对 TextBox/TextEditor 目标做了最小可用实现，其余 no-op。
    /// </summary>
    public static class ApplicationCommands
    {
        private abstract class CmdBase : System.Windows.Input.ICommand
        {
            public event EventHandler CanExecuteChanged { add { } remove { } }
            public bool CanExecute(object parameter) => true;
            public void Execute(object parameter) => ExecuteCore(parameter);
            protected abstract void ExecuteCore(object parameter);

        }

        private sealed class PasteCommand : CmdBase
        {
            protected override void ExecuteCore(object parameter)
            {
                if (parameter is TextBox tb)
                {
                    string text = Clipboard.GetText();
                    if (!string.IsNullOrEmpty(text))
                    {
                        int caret = tb.CaretIndex;
                        tb.Text = tb.Text?.Insert(Math.Min(caret, tb.Text?.Length ?? 0), text) ?? text;
                        tb.CaretIndex = caret + text.Length;
                    }
                }
                // AvaloniaEdit TextEditor 不在此路径上，忽略
            }
        }

        private sealed class CopyCommand : CmdBase
        {
            protected override void ExecuteCore(object parameter)
            {
                if (parameter is TextBox tb && !string.IsNullOrEmpty(tb.SelectedText)) Clipboard.SetText(tb.SelectedText);
            }
        }

        private sealed class CutCommand : CmdBase
        {
            protected override void ExecuteCore(object parameter)
            {
                if (parameter is TextBox tb && !string.IsNullOrEmpty(tb.SelectedText))
                {
                    Clipboard.SetText(tb.SelectedText);
                    int start = tb.SelectionStart, len = tb.SelectionEnd - tb.SelectionStart;
                    tb.Text = tb.Text.Remove(start, len);
                    tb.CaretIndex = start;
                }
            }
        }

        private sealed class SelectAllCommand : CmdBase
        {
            protected override void ExecuteCore(object parameter)
            {
                if (parameter is TextBox tb) tb.SelectAll();
            }
        }

        private sealed class NoopCommand : CmdBase
        {
            protected override void ExecuteCore(object parameter) { }
        }

        private static readonly System.Windows.Input.ICommand Noop = new NoopCommand();
        public static System.Windows.Input.ICommand Paste => _paste ??= new PasteCommand();
        public static System.Windows.Input.ICommand Copy => _copy ??= new CopyCommand();
        public static System.Windows.Input.ICommand Cut => _cut ??= new CutCommand();
        public static System.Windows.Input.ICommand SelectAll => _selectAll ??= new SelectAllCommand();
        public static System.Windows.Input.ICommand Undo => Noop;
        public static System.Windows.Input.ICommand Redo => Noop;
        public static System.Windows.Input.ICommand Delete => Noop;
        public static System.Windows.Input.ICommand Find => Noop;
        public static System.Windows.Input.ICommand Replace => Noop;
        public static System.Windows.Input.ICommand Save => Noop;
        public static System.Windows.Input.ICommand SaveAs => Noop;
        public static System.Windows.Input.ICommand Open => Noop;
        public static System.Windows.Input.ICommand Print => Noop;
        public static System.Windows.Input.ICommand PrintPreview => Noop;
        public static System.Windows.Input.ICommand New => Noop;
        public static System.Windows.Input.ICommand Close => Noop;
        public static System.Windows.Input.ICommand Properties => Noop;
        public static System.Windows.Input.ICommand Help => Noop;
        private static System.Windows.Input.ICommand _paste, _copy, _cut, _selectAll;
    }

    // ===== 逻辑树 =====

    public static class LogicalTreeHelper
    {
        public static StyledElement GetParent(StyledElement element) => element?.Parent as StyledElement;

        public static StyledElement FindLogicalNode(StyledElement root, string name)
            => FindByName(root, name);

        private static StyledElement FindByName(StyledElement node, string name)
        {
            if (node == null) return null;
            if (node.Name == name) return node;
            if (node is Visual nv)
                foreach (var child in nv.GetVisualDescendants().OfType<StyledElement>())
                    if (child.Name == name) return child;
            return null;
        }
    }

    // ===== 渲染选项 =====

    public enum ClearTypeHint
    {
        Auto = 0,
        Enabled = 1,
    }

    /// <summary>
    /// WPF System.Windows.Media.RenderOptions（Avalonia 文本渲染自动处理）no-op。
    /// 改名 RenderOptionsShim 避免与 Avalonia.Media.RenderOptions 二义性。
    /// </summary>
    public static class RenderOptionsShim
    {
        public static void SetClearTypeHint(Visual visual, ClearTypeHint hint) { }
        public static ClearTypeHint GetClearTypeHint(Visual visual) => ClearTypeHint.Auto;
        public static void SetBitmapScalingMode(Visual visual, global::Avalonia.Media.Imaging.BitmapInterpolationMode mode) { }
        /// <summary>WPF RenderOptions.ProcessRenderMode（Avalonia 无对应全局开关）。</summary>
        public static object ProcessRenderMode { get; set; }
    }

    /// <summary>WPF System.Windows.Interop.RenderMode。</summary>
    public enum RenderMode
    {
        Default = 0,
        SoftwareOnly = 1,
    }

    /// <summary>WPF System.Windows.PresentationTraceSources no-op。</summary>
    public static class PresentationTraceSources
    {
        public static void SetTraceLevel(object obj, object level) { }
    }

    // ===== 键盘导航 =====

    public enum FocusNavigationDirection
    {
        Next = 0,
        Previous = 1,
        First = 2,
        Last = 3,
        Left = 4,
        Right = 5,
        Up = 6,
        Down = 7,
    }

    public class TraversalRequest
    {
        public FocusNavigationDirection FocusNavigationDirection { get; }
        public TraversalRequest(FocusNavigationDirection direction) { FocusNavigationDirection = direction; }
    }

    /// <summary>
    /// WPF UIElement 捕获/焦点/坐标 API 的扩展 shim（this. 调用侧）。
    /// TODO 迁移：Avalonia 12 无全局指针捕获查询（捕获经由 PointerEventArgs.Pointer.Capture），
    /// Capture/Release 当前为 no-op 记账，后续在具体手势控件里改为事件内捕获。
    /// </summary>
    public static class InputCompat
    {
        private static readonly System.Collections.Generic.HashSet<InputElement> _captured = new();

        public static void CaptureMouse(this InputElement element)
        {
            if (element != null) _captured.Add(element);
        }

        public static void ReleaseMouseCapture(this InputElement element)
        {
            if (element != null) _captured.Remove(element);
        }

        public static bool IsMouseCaptured(this InputElement element)
            => element != null && _captured.Contains(element);

        public static Point PointFromScreen(this Visual visual, PixelPoint point)
            => TopLevel.GetTopLevel(visual)?.PointToClient(point) ?? default;

        public static Point PointToScreen(this Visual visual, Point point)
            => TopLevel.GetTopLevel(visual)?.PointToScreen(point).ToPoint() ?? default;

        public static bool MoveFocus(this InputElement element, TraversalRequest request)
        {
            var dir = request?.FocusNavigationDirection switch
            {
                FocusNavigationDirection.Previous => NavigationDirection.Previous,
                FocusNavigationDirection.First => NavigationDirection.First,
                FocusNavigationDirection.Last => NavigationDirection.Last,
                FocusNavigationDirection.Up => NavigationDirection.Up,
                FocusNavigationDirection.Down => NavigationDirection.Down,
                FocusNavigationDirection.Left => NavigationDirection.Left,
                FocusNavigationDirection.Right => NavigationDirection.Right,
                _ => NavigationDirection.Next,
            };
            var tl = TopLevel.GetTopLevel(element);
            return tl?.FocusManager?.TryMoveFocus(dir, new FindNextElementOptions()) ?? false;
        }

        public static bool GetKeyboardFocus(this InputElement element)
        {
            element?.Focus(NavigationMethod.Unspecified);
            return true;
        }
    }

    // ===== ContextMenuClosing =====

    public class ContextMenuClosingEventArgs : EventArgs
    {
        public bool Cancel { get; set; }
    }

    // ===== Win32 互操作 =====

    /// <summary>WPF System.Windows.Interop.WindowInteropHelper。</summary>
    public sealed class WindowInteropHelper
    {
        private readonly Window _window;
        public WindowInteropHelper(Window window) { _window = window; }
        public IntPtr Handle => _window?.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        public IntPtr Owner { get; set; }
    }

    /// <summary>WPF System.Windows.Shell.WindowChrome（Avalonia 自绘 chrome，属性仅存值）。</summary>
    public class WindowChrome
    {
        public Thickness GlassFrameThickness { get; set; } = new Thickness(0);
        public Thickness ResizeBorderThickness { get; set; } = new Thickness(4);
        public Thickness CaptionHeight { get; set; } = new Thickness(0, 32, 0, 0);
        public double CornerRadius { get; set; }
        public bool UseAeroCaptionButtons { get; set; }
    }

    // ===== WPF 视觉杂项 stub =====

    /// <summary>WPF System.Windows.Media.DrawingVisual：轻量自绘视觉。Avalonia 侧由 Control.Render 承担。</summary>
    public class DrawingVisual : global::Avalonia.Controls.Control
    {
        public new void Render(DrawingContext context) { }
    }

    /// <summary>WPF FormatConvertedBitmap stub。</summary>
    public class FormatConvertedBitmap
    {
        public global::Avalonia.Media.IImage Source { get; set; }
    }

    /// <summary>WPF IScrollInfo stub。</summary>
    public interface IScrollInfo
    {
        bool CanVerticallyScroll { get; set; }
        bool CanHorizontallyScroll { get; set; }
    }

    /// <summary>WPF FrameworkContentElement stub。</summary>
    public class FrameworkContentElement : AvaloniaObject { }

    /// <summary>WPF Visual3D stub。</summary>
    public class Visual3D : AvaloniaObject { }

    /// <summary>WPF ScrollContentPresenter stub（Avalonia 同名类在 Primitives，复杂场景手工迁移）。</summary>
    public class ScrollContentPresenterStub { }

    // ===== WPF 动画体系 stub =====
    // TODO 迁移：Avalonia 动画模型完全不同（Animation + Transitions，声明式）。
    // 这里只保留类型形状让代码编译；视觉动效待后续按控件逐个移植。

    public enum FillBehavior { HoldEnd, Stop }
    public enum HandoffBehavior { SnapshotAndReplace, Compose }

    /// <summary>WPF Duration。</summary>
    public struct Duration
    {
        public TimeSpan? Time { get; }
        public Duration(TimeSpan t) { Time = t; }
        public static Duration Automatic => new Duration();
        public static Duration Forever => new Duration(TimeSpan.MaxValue);
        public static implicit operator Duration(TimeSpan t) => new Duration(t);
    }

    /// <summary>WPF System.Windows.Media.Animation.DoubleAnimation stub。</summary>
    public class DoubleAnimation
    {
        public double? From { get; set; }
        public double? To { get; set; }
        public double? By { get; set; }
        public Duration Duration { get; set; }
        public FillBehavior FillBehavior { get; set; } = FillBehavior.HoldEnd;
        public IEasingFunctionBase EasingFunction { get; set; }
        public object BeginTime { get; set; }
    }

    /// <summary>WPF ThicknessAnimation stub。</summary>
    public class ThicknessAnimation
    {
        public Thickness? From { get; set; }
        public Thickness? To { get; set; }
        public Duration Duration { get; set; }
        public FillBehavior FillBehavior { get; set; } = FillBehavior.HoldEnd;
    }

    /// <summary>缓动函数公共标记（形状兼容）。</summary>
    public interface IEasingFunctionBase { }

    public class QuadraticEase : IEasingFunctionBase
    {
        public EasingMode EasingMode { get; set; } = EasingMode.EaseOut;
    }

    public class CubicEase : IEasingFunctionBase
    {
        public EasingMode EasingMode { get; set; } = EasingMode.EaseOut;
    }

    public class PowerEase : IEasingFunctionBase
    {
        public EasingMode EasingMode { get; set; } = EasingMode.EaseOut;
        public double Power { get; set; } = 2;
    }

    /// <summary>WPF FrameworkElement：Avalonia 12 无此类型，语义由 Control 承担。</summary>
    public class FrameworkElement : global::Avalonia.Controls.Control
    {
        /// <summary>WPF ActualWidth/ActualHeight（Avalonia Bounds）。</summary>
        public double ActualWidth => Bounds.Width;
        public double ActualHeight => Bounds.Height;
        public Size RenderSize => Bounds.Size;
    }

    /// <summary>WPF Storyboard stub：Begin/SetTarget 等全 no-op。</summary>
    public class Storyboard
    {
        public System.Collections.IList Children { get; } = new System.Collections.ArrayList();
        public void Begin() { }
        public void Begin(Visual containingObject) { }
        public void Stop() { }
        public static void SetTarget(object timeline, AvaloniaObject target) { }
        public static void SetTargetName(object timeline, string name) { }
        public static void SetTargetProperty(object timeline, object propertyPath) { }
    }

    /// <summary>
    /// WPF element.BeginAnimation(property, animation) 的等价物。
    /// 对 double 属性用 DispatcherTimer 做线性补间；Thickness/其他类型 no-op。
    /// 同一 (target, property) 的新动画会顶掉旧动画（对应 WPF HandoffBehavior.SnapshotAndReplace 近似）。
    /// TODO 迁移：正式实现请改用 Avalonia Animations/Transitions。
    /// </summary>
    public static class WpfAnimation
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<(AvaloniaObject, AvaloniaProperty), DispatcherTimer>
            _running = new();

        public static void BeginAnimation(AvaloniaObject target, AvaloniaProperty property, object animation)
        {
            if (target == null || property == null) return;

            if (animation is DoubleAnimation da && property.PropertyType == typeof(double))
            {
                if (!da.To.HasValue)
                {
                    // WPF 传 null 动画 = 还原基值；迁移期近似为清除运行中的动画、保持当前值。
                    if (_running.TryRemove((target, property), out var old)) old.Stop();
                    return;
                }
                double from = da.From ?? (target.GetValue(property) is double d ? d : 0.0);
                double to = da.To.Value;
                TimeSpan dur = da.Duration.Time ?? TimeSpan.Zero;
                if (dur <= TimeSpan.Zero) dur = TimeSpan.FromMilliseconds(200);

                if (_running.TryRemove((target, property), out var prev)) prev.Stop();

                var start = Environment.TickCount64;
                var timer = new DispatcherTimer(DispatcherPriority.Default) { Interval = TimeSpan.FromMilliseconds(15) };
                timer.Tick += (_, _) =>
                {
                    double t = Math.Min(1.0, (Environment.TickCount64 - start) / dur.TotalMilliseconds);
                    target.SetValue(property, from + (to - from) * t);
                    if (t >= 1.0)
                    {
                        timer.Stop();
                        _running.TryRemove(new KeyValuePair<(AvaloniaObject, AvaloniaProperty), DispatcherTimer>((target, property), timer));
                    }
                };
                _running[(target, property)] = timer;
                timer.Start();
            }
            else if (animation is ThicknessAnimation ta && property.PropertyType == typeof(Thickness))
            {
                if (ta.To.HasValue)
                    target.SetValue(property, ta.To.Value); // TODO 迁移：Thickness 补间，当前直接跳到终值
            }
        }
    }

    // ===== WPF ListView GridView stub（MultiselectionTreeView 的 GridView 模式数据形状）=====

    public class GridView
    {
        public System.Collections.IList Columns { get; } = new System.Collections.ArrayList();
    }

    public class GridViewColumn
    {
        public object Header { get; set; }
        public double Width { get; set; } = double.NaN;
        public object CellTemplate { get; set; }
        public object DisplayMemberBinding { get; set; }
    }

    public class GridViewColumnHeader : global::Avalonia.Controls.Control
    {
        public object Content { get; set; }
    }
}

// WPF 命名空间占位（Storyboard.SetTarget 等签名引用用）
namespace System.Windows.Media.Animation
{
    using ForkPlus.UI.WpfCompat;

    /// <summary>WPF Timeline 基类 stub（DoubleAnimation 等的基类形状）。</summary>
    public class TimelineShim { }

    public static class Timeline
    {
        // WPF Timeline.SetDesiredFrameRate 等 no-op
        public static void SetDesiredFrameRate(TimelineShim t, int? fps) { }
    }
}
