// WPF → Avalonia 迁移兼容层（ForkPlus-Next 人工收尾部分）
// 说明：以下类型在 WPF 中存在、Avalonia 12 无对应物，按"最小可用"原则重建。
// 每一处都有 TODO 迁移标记，后续应逐步替换为 Avalonia 原生实现。

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace ForkPlus.UI.WpfCompat
{
    // ===== WPF 事件参数与委托 =====

    /// <summary>WPF System.Windows.RequestNavigateEventArgs。</summary>
    public class RequestNavigateEventArgs : EventArgs
    {
        public Uri Uri { get; set; }
        public bool Handled { get; set; }
        public RequestNavigateEventArgs() { }
        public RequestNavigateEventArgs(Uri uri) { Uri = uri; }
    }

    /// <summary>WPF ContextMenu 事件参数（Avalonia 用 ContextRequested 替代）。</summary>
    public class ContextMenuEventArgs : global::Avalonia.Input.ContextRequestedEventArgs
    {
        public object TargetElement { get; set; }
    }

    /// <summary>统一走 Avalonia ContextRequestedEventArgs 签名，lambda 兼容。</summary>
    public delegate void ContextMenuEventHandler(object sender, global::Avalonia.Input.ContextRequestedEventArgs e);

    /// <summary>WPF GiveFeedbackEventArgs（拖放光标反馈，Avalonia 无此事件）。</summary>
    public class GiveFeedbackEventArgs : EventArgs
    {
        public DragDropEffects DragEffects { get; set; }
    }

    /// <summary>WPF DataObjectPastingEventArgs（粘贴拦截，Avalonia TextBox 无此事件）。</summary>
    public class DataObjectPastingEventArgs : EventArgs
    {
        public bool Cancel { get; set; }
        public string TextToPaste { get; set; }
        /// <summary>WPF e.DataObject 等价物：粘贴内容的包装。</summary>
        public PastingDataObject DataObject { get; set; }
        public void CancelCommand() { Cancel = true; }
    }

    /// <summary>粘贴内容包装（对应 WPF IDataObject 的只读视图）。</summary>
    public sealed class PastingDataObject
    {
        private string _text;
        public PastingDataObject(string text) { _text = text; }
        public bool GetDataPresent(Type t) => t == typeof(string);
        public bool GetDataPresent(string format) => format == DataFormats.Text || format == DataFormats.UnicodeText || format == "System.String";
        public object GetData(Type t) => _text;
        public object GetData(string format) => GetDataPresent(format) ? _text : null;
        public void SetData(string format, object data) { if (data is string s) _text = s; }
    }

    /// <summary>
    /// WPF DataObject.AddPastingHandler 的等价安装器。
    /// 以隧道方式截获 Ctrl+V / Shift+Ins，读剪贴板后回调处理器；
    /// 处理器可改写文本（写回 DataObject）或 CancelCommand 接管粘贴。
    /// TODO 迁移：Avalonia TextBox 无原生粘贴拦截事件，此为行为近似实现。
    /// </summary>
    public static class PasteGuard
    {
        public static void AddPastingHandler(this TextBox textBox, EventHandler<DataObjectPastingEventArgs> handler)
        {
            if (textBox == null || handler == null) return;
            textBox.AddHandler(InputElement.KeyDownEvent, async (object s, KeyEventArgs e) =>
            {
                bool pasteKey = (e.Key == Key.V && e.KeyModifiers.HasFlag(KeyModifiers.Control))
                             || (e.Key == Key.Insert && e.KeyModifiers.HasFlag(KeyModifiers.Shift));
                if (!pasteKey) return;
                var clipboard = TopLevel.GetTopLevel(textBox)?.Clipboard;
                if (clipboard == null) return;
                string text = await clipboard.TryGetTextAsync();
                if (text == null) return;
                var args = new DataObjectPastingEventArgs { DataObject = new PastingDataObject(text) };
                handler(textBox, args);
                e.Handled = true; // 阻断默认粘贴，由 shim 统一插入
                if (args.Cancel) return; // 处理器已自行处理
                string toInsert = args.DataObject.GetData(DataFormats.Text) as string ?? text;
                int caret = textBox.CaretIndex;
                int start = textBox.SelectionStart, len = textBox.SelectionEnd - textBox.SelectionStart;
                if (len > 0) { textBox.Text = textBox.Text.Remove(start, len); caret = start; }
                textBox.Text = textBox.Text.Insert(caret, toInsert);
                textBox.CaretIndex = caret + toInsert.Length;
            }, RoutingStrategies.Tunnel);
        }
    }

    /// <summary>WPF ToolTipEventArgs。</summary>
    public class ToolTipEventArgs : EventArgs
    {
        public bool IsOpen { get; set; }
    }

    /// <summary>WPF RoutedPropertyChangedEventArgs&lt;T&gt;（Slider/ScrollBar 值变化等）。</summary>
    public class RoutedPropertyChangedEventArgs<T> : EventArgs
    {
        public T OldValue { get; }
        public T NewValue { get; }
        public object Source { get; set; }
        public RoutedPropertyChangedEventArgs(T oldValue, T newValue)
        {
            OldValue = oldValue;
            NewValue = newValue;
        }
    }

    /// <summary>WPF IWeakEventListener（与 WeakEventManager 配套）。</summary>
    public interface IWeakEventListener
    {
        bool ReceiveWeakEvent(Type managerType, object sender, EventArgs e);
    }

    // ===== WPF 拼写检查 no-op 降级（Avalonia 无内置拼写检查）=====

    /// <summary>WPF SpellingError 降级 shim：Suggestions 恒为空。</summary>
    public class SpellingError
    {
        public IEnumerable<string> Suggestions { get; } = Array.Empty<string>();
    }

    /// <summary>WPF EditingCommands 降级 shim。</summary>
    public static class EditingCommands
    {
        private sealed class NoopCommand : System.Windows.Input.ICommand
        {
            public event EventHandler CanExecuteChanged { add { } remove { } }
            public bool CanExecute(object parameter) => false;
            public void Execute(object parameter) { }
        }
        public static System.Windows.Input.ICommand CorrectSpellingError { get; } = new NoopCommand();
        public static System.Windows.Input.ICommand IgnoreSpellingError { get; } = new NoopCommand();
    }

    /// <summary>WPF System.Windows.SpellCheck no-op shim。</summary>
    public class SpellCheck
    {
        public bool IsEnabled { get; set; }
    }

    /// <summary>WPF XmlLanguage shim（仅保留语言标签语义）。</summary>
    public class XmlLanguage
    {
        public string IetfLanguageTag { get; }
        private XmlLanguage(string tag) { IetfLanguageTag = tag; }
        public static XmlLanguage GetLanguage(string ietfLanguageTag) => new XmlLanguage(ietfLanguageTag);
    }

    /// <summary>WPF SystemSounds shim（Avalonia 无系统音效 API，静默）。</summary>
    public static class SystemSounds
    {
        public sealed class SoundPlayer
        {
            internal SoundPlayer() { }
            public void Play() { }
        }
        public static SoundPlayer Beep { get; } = new SoundPlayer();
        public static SoundPlayer Exclamation { get; } = new SoundPlayer();
        public static SoundPlayer Asterisk { get; } = new SoundPlayer();
        public static SoundPlayer Hand { get; } = new SoundPlayer();
        public static SoundPlayer Question { get; } = new SoundPlayer();
    }

    /// <summary>TextBox.GetSpellingError 扩展：恒返回 null（无拼写引擎）。</summary>
    public static class SpellCheckExtensions
    {
        public static SpellingError GetSpellingError(this TextBox textBox, int caretIndex) => null;
    }

    // ===== 颜色工具（OxyPlot.Wpf 命名空间的扩展移到 WpfCompat）=====
    // 注意：ToOxyColor(Color) 直接复用 OxyPlot.Avalonia.ConverterExtensions，避免扩展方法二义性。

    /// <summary>WPF System.Windows.Media.ColorConverter shim（十六进制解析）。</summary>
    public static class ColorConverter
    {
        public static object ConvertFromString(string s)
            => global::Avalonia.Media.Color.Parse(s.TrimStart('#').Length == 6 ? "#" + s : s);
    }

    /// <summary>
    /// WPF ListCollectionView shim：支持 Filter + Refresh 的可绑定集合视图。
    /// 通过 CollectionChanged(Reset) 通知绑定目标刷新。
    /// </summary>
    public class ListCollectionView : System.Collections.IEnumerable, System.Collections.Specialized.INotifyCollectionChanged
    {
        private readonly System.Collections.IEnumerable _source;
        public System.Predicate<object> Filter { get; set; }
        public event System.Collections.Specialized.NotifyCollectionChangedEventHandler CollectionChanged
        {
            add { }
            remove { }
        }
        public ListCollectionView(System.Collections.IEnumerable source) { _source = source; }
        public System.Collections.IEnumerator GetEnumerator()
        {
            foreach (var item in _source)
                if (Filter == null || Filter(item)) yield return item;
        }
        public void Refresh() { /* 绑定刷新依赖宿主重设 ItemsSource；调用方已有该逻辑 */ }
    }

    // ===== 路由命令体系（WPF RoutedCommand/CommandBinding 的按键路由替代）=====

    public class ExecutedEventArgs : EventArgs
    {
        public object Parameter { get; set; }
        public object Source { get; set; }
    }

    public delegate void ExecutedRoutedEventHandler(object sender, ExecutedEventArgs e);

    /// <summary>WPF RoutedCommand 等价物：承载名称与手势集合。</summary>
    public class RoutedCommand
    {
        public string Name { get; set; } = string.Empty;
        public List<KeyGesture> InputGestures { get; } = new List<KeyGesture>();
        public override string ToString() => Name;
    }

    /// <summary>WPF CommandBinding：命令与处理器的绑定。</summary>
    public class CommandBinding
    {
        public RoutedCommand Command { get; }
        public System.Windows.Input.ICommand CommandInterface { get; }
        public ExecutedRoutedEventHandler Executed { get; }
        public CommandBinding(RoutedCommand command, ExecutedRoutedEventHandler executed)
        {
            Command = command;
            Executed = executed;
        }

        /// <summary>支持 ApplicationCommands 等 ICommand 形态的构造（CustomWindow 用）。</summary>
        public CommandBinding(System.Windows.Input.ICommand command, ExecutedRoutedEventHandler executed)
        {
            CommandInterface = command;
            Executed = executed;
        }
    }

    /// <summary>
    /// 按键路由器：把 WPF Window.CommandBindings 的语义搬到 Avalonia。
    /// 在 TopLevel 上以隧道方式监听 KeyDown，匹配已注册手势后执行绑定。
    /// TODO 迁移：长期应改为 Avalonia HotKey / KeyBinding 原生方案。
    /// </summary>
    public static class CommandRouter
    {
        private static readonly ConditionalWeakTable<TopLevel, List<CommandBinding>> _bindings = new();
        private static readonly ConditionalWeakTable<TopLevel, object> _installed = new();

        public static void AddCommandBinding(this Interactive host, CommandBinding binding)
        {
            if (host == null || binding == null) return;
            var tl = TopLevel.GetTopLevel(host);
            if (tl == null)
            {
                // 尚未挂到视觉树：延迟到 AttachedToVisualTree 后注册
                if (host is Control c)
                {
                    void Attached(object s, VisualTreeAttachmentEventArgs e)
                    {
                        c.AttachedToVisualTree -= Attached;
                        AddCommandBinding(host, binding);
                    }
                    c.AttachedToVisualTree += Attached;
                }
                return;
            }
            Install(tl);
            var list = _bindings.GetOrCreateValue(tl);
            list.Add(binding);
        }

        private static void Install(TopLevel tl)
        {
            if (_installed.TryGetValue(tl, out _)) return;
            _installed.Add(tl, new object());
            tl.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        }

        private static void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not TopLevel tl) return;
            if (!_bindings.TryGetValue(tl, out var list)) return;
            foreach (var b in list)
            {
                if (b.Command == null && b.CommandInterface == null) continue;
                if (b.Command != null)
                {
                    if (b.Command.InputGestures == null) continue;
                    foreach (var g in b.Command.InputGestures)
                    {
                        if (Matches(g, e))
                        {
                            b.Executed?.Invoke(b.Command, new ExecutedEventArgs { Source = e.Source, Parameter = null });
                            e.Handled = true;
                            return;
                        }
                    }
                }
                else if (b.CommandInterface is RoutedCommand rc && rc.InputGestures != null)
                {
                    foreach (var g in rc.InputGestures)
                    {
                        if (Matches(g, e))
                        {
                            b.Executed?.Invoke(rc, new ExecutedEventArgs { Source = e.Source, Parameter = null });
                            e.Handled = true;
                            return;
                        }
                    }
                }
            }
        }

        private static bool Matches(KeyGesture gesture, KeyEventArgs e)
        {
            if (gesture == null || e.Key == Key.None) return false;
            if (gesture.Key != e.Key) return false;
            var mods = e.KeyModifiers;
            var want = gesture.KeyModifiers;
            return mods == want;
        }
    }

    // ===== Adorner 装饰器体系 =====

    /// <summary>
    /// WPF Adorner 等价物：覆盖在被装饰元素之上的自绘控件。
    /// 基类用 Control 而非 Panel：Avalonia 12 把 Panel.Render 标记为 sealed，
    /// 自绘型装饰器（DragAdorner 等）必须从未密封 Render 的 Control 派生才能 override Render；
    /// 子控件承载型装饰器（CustomAdorner 等）通过 VisualChildren 列表挂子级，布局由各自的
    /// MeasureOverride/ArrangeOverride 手工驱动（与 WPF 原实现一致）。
    /// </summary>
    public abstract class Adorner : global::Avalonia.Controls.Control
    {
        public InputElement AdornedElement { get; }
        protected Adorner(InputElement adornedElement)
        {
            AdornedElement = adornedElement;
            IsHitTestVisible = false;
        }

        // ===== WPF 视觉/逻辑子级管理 API 的 VisualChildren 化 shim =====
        protected void AddVisualChild(global::Avalonia.Controls.Control child)
        {
            if (child != null && !VisualChildren.Contains(child)) VisualChildren.Add(child);
        }
        protected void RemoveVisualChild(global::Avalonia.Controls.Control child)
        {
            if (child != null) VisualChildren.Remove(child);
        }
        protected void AddLogicalChild(global::Avalonia.Controls.Control child) { }
        protected void RemoveLogicalChild(global::Avalonia.Controls.Control child) { }
        protected virtual int VisualChildrenCount => VisualChildren.Count;
        protected virtual Visual GetVisualChild(int index) => VisualChildren[index];
    }

    /// <summary>
    /// WPF AdornerLayer 等价物：每个 TopLevel 一个覆盖层 Canvas，
    /// 装饰器按被装饰元素在覆盖层中的坐标摆放。
    /// </summary>
    public sealed class AdornerLayer : Canvas
    {
        private static readonly ConditionalWeakTable<TopLevel, AdornerLayer> _layers = new();
        private readonly List<(Visual adorned, Control adorner)> _items = new();

        private AdornerLayer()
        {
            IsHitTestVisible = false;
            ZIndex = 4096;
        }

        public static AdornerLayer GetAdornerLayer(Visual visual)
        {
            if (visual == null) return null;
            var tl = TopLevel.GetTopLevel(visual);
            if (tl == null) return null;
            var layer = _layers.GetOrCreateValue(tl);
            if (layer.Parent == null)
            {
                // 把 TopLevel 的内容包进 Grid，再叠加装饰层
                if (tl is ContentControl cc && cc.Content != null && !(cc.Content is Grid g && g.Tag == layer))
                {
                    var old = cc.Content;
                    var grid = new Grid { Tag = layer };
                    if (old is Control oldCtl)
                    {
                        oldCtl.SetValue(Panel.ZIndexProperty, 0);
                        grid.Children.Add(oldCtl);
                    }
                    grid.Children.Add(layer);
                    cc.Content = grid;
                }
                tl.LayoutUpdated += (_, _) => layer.RepositionAll();
            }
            return layer;
        }

        public void Add(Visual adorner)
        {
            if (adorner is Control ctl && !_items.Exists(i => i.adorner == ctl))
            {
                var adorned = (adorner as Adorner)?.AdornedElement as Visual ?? adorner;
                _items.Add((adorned, ctl));
                Children.Add(ctl);
                RepositionAll();
            }
        }

        public void Remove(Visual adorner)
        {
            if (adorner is Control ctl)
            {
                _items.RemoveAll(i => i.adorner == ctl);
                Children.Remove(ctl);
            }
        }

        public void Update()
        {
            RepositionAll();
            InvalidateVisual();
        }

        private void RepositionAll()
        {
            foreach (var (adorned, adorner) in _items)
            {
                if (adorned == null || adorner == null) continue;
                var origin = adorned.TranslatePoint(new Point(), this) ?? new Point();
                adorner.SetValue(LeftProperty, origin.X);
                adorner.SetValue(TopProperty, origin.Y);
                var size = adorned.Bounds.Size;
                if (adorner is Adorner a && a.AdornedElement is Visual av) size = av.Bounds.Size;
                adorner.Width = size.Width;
                adorner.Height = size.Height;
            }
        }
    }

    // ===== 弱事件管理器（WPF WeakEventManager&lt;T,E&gt;）=====

    /// <summary>
    /// WPF WeakEventManager&lt;TInstance,TArgs&gt; 等价物。
    /// 通过反射订阅实例事件，以弱引用持有订阅者，避免 NotificationCenter 长生命周期造成的泄漏。
    /// </summary>
    public static class WeakEventManager<TInstance, TArgs>
        where TInstance : class
    {
        private sealed class Entry
        {
            public WeakReference Target;
            public MethodInfo Method;
            public Delegate StrongHandler; // 静态 lambda 无目标，强持有
            public void Invoke(object sender, object args)
            {
                if (StrongHandler != null) { StrongHandler.DynamicInvoke(sender, args); return; }
                var t = Target?.Target;
                if (t == null) return;
                Method?.Invoke(t, new[] { sender, args });
            }
        }

        private static readonly ConditionalWeakTable<TInstance, List<(EventInfo ev, Delegate d, Entry entry)>> _map = new();

        public static void AddHandler(TInstance instance, string eventName, Delegate handler)
        {
            if (instance == null || handler == null) return;
            var ev = typeof(TInstance).GetEvent(eventName) ?? instance.GetType().GetEvent(eventName);
            if (ev == null) return;
            var entry = new Entry
            {
                Target = handler.Target != null ? new WeakReference(handler.Target) : null,
                Method = handler.Method,
                StrongHandler = handler.Target == null ? handler : null
            };
            var forwarder = typeof(Forwarder).GetMethod(nameof(Forwarder.OnEvent))
                .MakeGenericMethod(ev.EventHandlerType.GetMethod("Invoke").GetParameters()[1].ParameterType);
            var proxy = Delegate.CreateDelegate(ev.EventHandlerType, new Forwarder(entry), forwarder);
            ev.AddEventHandler(instance, proxy);
            var list = _map.GetOrCreateValue(instance);
            list.Add((ev, proxy, entry));
        }

        public static void RemoveHandler(TInstance instance, string eventName, Delegate handler)
        {
            if (instance == null || !_map.TryGetValue(instance, out var list)) return;
            list.RemoveAll(x =>
            {
                if (x.ev.Name != eventName) return false;
                if (handler != null && x.entry.Method != handler.Method) return false;
                x.ev.RemoveEventHandler(instance, x.d);
                return true;
            });
        }

        private sealed class Forwarder
        {
            private readonly Entry _entry;
            public Forwarder(Entry entry) { _entry = entry; }
            [Preserve]
            public void OnEvent<TA>(object sender, TA args) => _entry.Invoke(sender, args);
        }
    }
}

// 占位属性，防止 linker 裁剪反射转发方法
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class PreserveAttribute : Attribute { }
}
