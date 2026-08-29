// WPF → Avalonia 迁移兼容层 第四部分：第二批高频 API shim
// （TryFindResource / Owner / ContainerFromElement / DoDragDrop / Cursors / Imaging stub）
// 全部带 TODO 迁移标记，收尾阶段逐个替换为原生实现。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace ForkPlus.UI.WpfCompat
{
    // （TryFindResource / SetResourceReference 见 WpfCompat.Controls.cs 的 ResourceCompat）

    // ===== WPF Window.Owner（Avalonia 只能经 ShowDialog(owner) 设置）=====

    public static class WindowOwnerCompat
    {
        private static readonly ConditionalWeakTable<Window, StrongBox<Window>> Owners = new();

        /// <summary>
        /// WPF w.Owner = owner。Avalonia 的 Owner 无公开 setter；
        /// 这里记表供 WindowDialogCompat.ShowDialog 优先使用（不设置也能找活动窗口兜底）。
        /// </summary>
        public static void SetOwnerCompat(this Window self, Window owner)
        {
            Owners.Remove(self);
            Owners.Add(self, new StrongBox<Window>(owner));
        }

        internal static Window TryGetOwner(Window self)
            => Owners.TryGetValue(self, out var box) ? box.Value : null;
    }

    // ===== WPF ItemsControl.ContainerFromElement =====

    public static class ItemsControlCompat
    {
        /// <summary>
        /// WPF ItemsControl.ContainerFromElement(element)：从内部元素找到条目容器。
        /// 实现：沿可视树向上找 ItemContainerGenerator 生成的容器。
        /// </summary>
        public static Control ContainerFromElement(this ItemsControl itemsControl, Visual element)
        {
            if (itemsControl == null || element == null) return null;
            var containers = itemsControl.GetRealizedContainers()?.ToList()
                ?? new List<Control>();
            for (var v = element; v != null; v = v.GetVisualParent())
                if (containers.Contains(v))
                    return v;
            return null;
        }
    }

    // ===== WPF DragDrop.DoDragDrop（阻塞式）→ Avalonia 12 DoDragDropAsync =====
    // 注意：与 WpfCompat.Controls.cs 的 DragDropCompat（读取侧）区分，本类只负责"发起拖放"。

    public static class DragDropLauncher
    {
        private static readonly ConditionalWeakTable<InputElement, StrongBox<PointerPressedEventArgs>> LastPress = new();

        /// <summary>
        /// WPF DragDrop.DoDragDrop(source, data, effects)（阻塞直到放下）。
        /// Avalonia 12 的 DoDragDropAsync 必须拿 PointerPressedEventArgs；
        /// 这里对 source 挂 Tunnel 记录最近一次按下，首次手势可能不触发（TODO 迁移：改为事件内 await）。
        /// </summary>
        public static DragDropEffects DoDragDrop(InputElement source, object data, DragDropEffects allowedEffects)
        {
            if (source == null) return DragDropEffects.None;
            if (!LastPress.TryGetValue(source, out var box))
            {
                box = new StrongBox<PointerPressedEventArgs>();
                LastPress.Add(source, box);
                source.AddHandler(InputElement.PointerPressedEvent, (_, e) => box.Value = e,
                    global::Avalonia.Interactivity.RoutingStrategies.Tunnel);
                return DragDropEffects.None; // 本次手势没有按下记录，跳过（下次起生效）
            }
            if (box.Value == null) return DragDropEffects.None;

            var transfer = ToTransfer(data);
            if (transfer == null) return DragDropEffects.None;

            _ = global::Avalonia.Input.DragDrop.DoDragDropAsync(box.Value, transfer, allowedEffects);
            return DragDropEffects.None; // 异步结果不回传（WPF 语义近似），TODO 迁移
        }

        private static IDataTransfer ToTransfer(object data)
        {
            switch (data)
            {
                case IDataTransfer t:
                    return t;
                case string[] files:
                    var df = new DataObject();
                    df.Set(new DataFormat("FileDrop"), files.ToList());
                    return df;
                case string text:
                    var dt = new DataObject();
                    dt.Set(new DataFormat("Text"), text);
                    return dt;
                default:
                    var dobj = new DataObject();
                    dobj.Set(new DataFormat("ForkPlusItem"), data);
                    return dobj;
            }
        }
    }

    // ===== WPF System.Windows.Input.Cursors =====

    /// <summary>WPF Cursors 静态类（Avalonia 用 new Cursor(StandardCursorType.X)）。</summary>
    public static class Cursors
    {
        public static Cursor Hand => new Cursor(StandardCursorType.Hand);
        public static Cursor Arrow => new Cursor(StandardCursorType.Arrow);
        public static Cursor Wait => new Cursor(StandardCursorType.Wait);
        public static Cursor IBeam => new Cursor(StandardCursorType.Ibeam);
        public static Cursor Cross => new Cursor(StandardCursorType.Cross);
        public static Cursor SizeAll => new Cursor(StandardCursorType.SizeAll);
        public static Cursor SizeNESW => new Cursor(StandardCursorType.SizeNesw);
        public static Cursor SizeNS => new Cursor(StandardCursorType.SizeNs);
        public static Cursor SizeNWSE => new Cursor(StandardCursorType.SizeNwse);
        public static Cursor SizeWE => new Cursor(StandardCursorType.SizeWe);
        public static Cursor No => new Cursor(StandardCursorType.No);
        public static Cursor None => new Cursor(StandardCursorType.None);
    }
}

// ===== WPF System.Windows.Media.Imaging 形状 stub（图标提取等 Win32 互操作用）=====

namespace System.Windows.Media.Imaging
{
    using ForkPlus.UI.WpfCompat;

    /// <summary>WPF Int32Rect。</summary>
    public struct Int32Rect
    {
        public int X, Y, Width, Height;
        public static Int32Rect Empty => default;
    }

    /// <summary>WPF BitmapSizeOptions（仅形状兼容）。</summary>
    public sealed class BitmapSizeOptions
    {
        public static BitmapSizeOptions FromEmptyOptions() => new BitmapSizeOptions();
    }

    public enum BitmapCreateOptions { None, DelayCreation, IgnoreColorProfile, PreservePixelFormat }
    public enum BitmapCacheOption { Default, OnDemand, OnLoad, None }

    /// <summary>
    /// WPF System.Windows.Media.Imaging 静态类。
    /// CreateBitmapSourceFromHIcon 是 GDI→WPF 位图桥；Avalonia 侧需要走 WriteableBitmap，
    /// 迁移期返回 null（调用方已有 null 兜底），TODO 迁移：补 Win32 图标位图转换。
    /// </summary>
    public static class Imaging
    {
        public static global::Avalonia.Media.IImage CreateBitmapSourceFromHIcon(IntPtr hIcon, Int32Rect sourceRect, BitmapSizeOptions sizeOptions)
        {
            return null; // TODO 迁移：GDI HICON → Avalonia Bitmap
        }

        public static global::Avalonia.Media.IImage CreateBitmapSourceFromHBitmap(IntPtr bitmap, Int32Rect sourceRect, BitmapSizeOptions sizeOptions)
        {
            return null; // TODO 迁移：GDI HBITMAP → Avalonia Bitmap
        }
    }
}
