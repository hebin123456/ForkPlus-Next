// WPF → Avalonia 迁移兼容层 第四部分：fixer pass3 依赖的 shim
// ScrollViewer.ScrollToVerticalOffset/ScrollToHorizontalOffset 等。

using System;
using Avalonia;
using Avalonia.Controls;

namespace ForkPlus.UI.WpfCompat
{
    /// <summary>WPF ScrollViewer.ScrollTo*Offset 扩展（Avalonia 经 Offset 属性设置）。</summary>
    public static class ScrollViewerCompat
    {
        public static void ScrollToVerticalOffsetCompat(this ScrollViewer sv, double offset)
        {
            if (sv == null) return;
            sv.Offset = sv.Offset.WithY(offset);
        }

        public static void ScrollToHorizontalOffsetCompat(this ScrollViewer sv, double offset)
        {
            if (sv == null) return;
            sv.Offset = sv.Offset.WithX(offset);
        }
    }
}
