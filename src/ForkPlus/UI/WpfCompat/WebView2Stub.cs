// WPF → Avalonia 迁移兼容层：WebView2 的原生渲染实现（原为纯文本占位）。
//
// 真机 bug#10 根因：原 WPF 版 AI 弹窗内容（AI 辅助开发 / AI 解释 / AI 代码评审 / git mm 手册）
// 全部由 WebView2 渲染 HTML+CSS；迁移时本类是 TextBlock 占位（NavigateToString 去标签显示纯文本），
// 导致"AI 相关弹窗样式全都有问题"——无排版、无代码块底色、无列表缩进、无表格。
//
// 现改为：WebView2 : ScrollViewer，NavigateToString 把 HTML body 解析成 Avalonia 控件树
// （见 MarkdownHtmlRenderer，视觉对齐 WPF 版 md-ai-output.css），内部滚动/背景/明暗主题齐备。
// 调用面保持不变：NavigateToString / EnsureCoreWebView2Async / CoreWebView2(事件) /
// ExecuteScriptAsync / DefaultBackgroundColor / Dispose。
//
// 兼容性桥接：
//  - NavigationCompleted 在内容布局完成后触发（延迟一帧），让 AiDevelopmentWindow 的
//    "导航完成后量高度调 WebView.Height"逻辑照常工作；
//  - ExecuteScriptAsync("document.documentElement.scrollHeight") 返回实测内容高度；
//  - ExecuteScriptAsync("window.scrollTo(0, …)") 映射为滚动到底部；
//  - ApplicationThemeChanged 时用新明暗配色重渲染当前内容。

using System;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;
using ForkPlus.Settings;
using ForkPlus.UI;
using ForkPlus.UI.WpfCompat;

namespace Microsoft.Web.WebView2.Core
{
    /// <summary>CoreWebView2Profile.PreferredColorScheme 枚举（兼容层）。</summary>
    public enum CoreWebView2PreferredColorScheme
    {
        Auto = 0,
        Light = 1,
        Dark = 2,
    }

    /// <summary>CoreWebView2Profile 兼容层（仅存配色偏好）。</summary>
    public sealed class CoreWebView2Profile
    {
        public CoreWebView2PreferredColorScheme PreferredColorScheme { get; set; } = CoreWebView2PreferredColorScheme.Auto;
    }

    /// <summary>CoreWebView2Environment 兼容层（WebView2EnvironmentHelper 返回值形状）。</summary>
    public sealed class CoreWebView2Environment
    {
        /// <summary>原 WinRT API CoreWebView2Environment.CreateAsync（兼容层返回空环境）。</summary>
        public static Task<CoreWebView2Environment> CreateAsync(string browserExecutableFolder = null, string userDataFolder = null)
            => Task.FromResult(new CoreWebView2Environment());
    }

    public class CoreWebView2ContextMenuRequestedEventArgs : EventArgs
    {
        public bool Handled { get; set; }
    }

    public class CoreWebView2NavigationCompletedEventArgs : EventArgs
    {
        public bool IsSuccess { get; set; }
    }

    public class CoreWebView2WebMessageReceivedEventArgs : EventArgs
    {
        public string Message { get; }
        public CoreWebView2WebMessageReceivedEventArgs(string message) { Message = message; }
        public string TryGetWebMessageAsString() => Message;
    }

    /// <summary>CoreWebView2 兼容层：事件由 NavigateToString / 按钮回调驱动。</summary>
    public sealed class CoreWebView2
    {
        internal CoreWebView2() { }

        public CoreWebView2Profile Profile { get; } = new();

        public event EventHandler<CoreWebView2ContextMenuRequestedEventArgs> ContextMenuRequested;
        public event EventHandler<CoreWebView2NavigationCompletedEventArgs> NavigationCompleted;
        public event EventHandler<CoreWebView2WebMessageReceivedEventArgs> WebMessageReceived;

        public Task<string> ExecuteScriptAsync(string script) => Task.FromResult("null");

        internal void RaiseNavigationCompleted(bool success)
            => NavigationCompleted?.Invoke(this, new CoreWebView2NavigationCompletedEventArgs { IsSuccess = success });

        internal void RaiseWebMessageReceived(string message)
            => WebMessageReceived?.Invoke(this, new CoreWebView2WebMessageReceivedEventArgs(message));
    }
}

namespace Microsoft.Web.WebView2.Wpf
{
    using Microsoft.Web.WebView2.Core;

    /// <summary>
    /// WebView2 原生渲染兼容控件：HTML → Avalonia 控件树（MarkdownHtmlRenderer），
    /// 自带内部滚动。明暗主题跟随 NotificationCenter.ApplicationThemeChanged。
    /// </summary>
    public class WebView2 : ScrollViewer
    {
        private CoreWebView2 _core;
        private string _lastHtml;
        private bool _themeHooked;

        /// <summary>
        /// Migration note（2026-09-04，问题E"git mm 手册内容无法选中和滚动"）：
        /// Avalonia 隐式 ControlTheme 按 StyleKey 精确匹配资源 key（默认=实际类型
        /// WebView2），而资源链里只有 {x:Type ScrollViewer}——子类匹配不到就落到
        /// ContentControl 的默认模板（裸 ContentPresenter，无 ScrollContentPresenter
        /// 无滚动条），于是手册内容显示出来但 extent/viewport 恒 0、完全不能滚。
        /// 重写 StyleKey 为基类 ScrollViewer，与 TouchpadAwareScrollViewer 在
        /// Scrollviewer.axaml 里挂 ControlTheme BasedOn 的做法等价，让 ForkPlus 的
        /// ScrollViewer 主题（含 SCP 命中修复）对本控件生效。
        /// </summary>
        protected override Type StyleKeyOverride => typeof(ScrollViewer);

        public WebView2()
        {
            Background = Brushes.Transparent;
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            HookThemeChange();
        }

        private void HookThemeChange()
        {
            if (_themeHooked)
            {
                return;
            }
            _themeHooked = true;
            ForkPlus.NotificationCenter.Current.ApplicationThemeChanged += OnApplicationThemeChanged;
            DetachedFromVisualTree += delegate
            {
                ForkPlus.NotificationCenter.Current.ApplicationThemeChanged -= OnApplicationThemeChanged;
            };
        }

        private void OnApplicationThemeChanged(object sender, EventArgs<ThemeType> e)
        {
            if (_lastHtml != null)
            {
                // 用新明暗配色重渲染当前内容（对应 CSS prefers-color-scheme）
                RenderHtml(_lastHtml);
            }
        }

        /// <summary>WPF WebView2.WebView → 兼容层直接暴露自身。</summary>
        public WebView2 CoreWebView2Host => this;

        /// <summary>WPF WebView2.DefaultBackgroundColor（兼容层：透明背景由控件本身呈现）。</summary>
        public System.Drawing.Color DefaultBackgroundColor { get; set; } = System.Drawing.Color.Transparent;

        /// <summary>WPF WebView2.Dispose()（ScrollViewer 非 IDisposable，空实现）。</summary>
        public void Dispose() { }

        public Task EnsureCoreWebView2Async(object userData = null) => Task.CompletedTask;

        public CoreWebView2 CoreWebView2 => _core ??= new CoreWebView2();

        /// <summary>渲染 HTML 文档：提取 body 后解析为控件树。</summary>
        public void NavigateToString(string html)
        {
            if (html == null)
            {
                html = string.Empty;
            }
            _lastHtml = html;
            RenderHtml(html);
            // 兼容桥：内容布局完成后触发 NavigationCompleted（延迟到渲染帧之后），
            // AiDevelopmentWindow 依赖此事件 + ExecuteScriptAsync(scrollHeight) 做气泡自动高度。
            Dispatcher.UIThread.Post(delegate
            {
                Dispatcher.UIThread.Post(delegate
                {
                    CoreWebView2.RaiseNavigationCompleted(success: true);
                }, DispatcherPriority.Render);
            }, DispatcherPriority.Render);
        }

        private void RenderHtml(string html)
        {
            string body = ExtractBody(html);
            bool dark = ForkPlusSettings.Default.Theme.IsDarkBase();
            Content = MarkdownHtmlRenderer.Render(body, dark, delegate (string message)
            {
                CoreWebView2.RaiseWebMessageReceived(message);
            });
        }

        /// <summary>提取 &lt;body&gt; 内容；无 body 标签时按片段整体处理。</summary>
        private static string ExtractBody(string html)
        {
            int bodyStart = html.IndexOf("<body", StringComparison.OrdinalIgnoreCase);
            if (bodyStart >= 0)
            {
                int openEnd = html.IndexOf('>', bodyStart);
                if (openEnd >= 0)
                {
                    int bodyEnd = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
                    if (bodyEnd > openEnd)
                    {
                        return html.Substring(openEnd + 1, bodyEnd - openEnd - 1);
                    }
                    return html.Substring(openEnd + 1);
                }
            }
            return html;
        }

        /// <summary>
        /// 脚本桥：只翻译 WPF 调用方真实用到的两个脚本——
        /// "document.documentElement.scrollHeight"（量内容高度）与 "window.scrollTo(0,…)"（滚到底）。
        /// </summary>
        public Task<string> ExecuteScriptAsync(string script)
        {
            if (script != null)
            {
                if (script.Contains("scrollHeight"))
                {
                    return Task.FromResult(MeasureContentHeight().ToString("0", CultureInfo.InvariantCulture));
                }
                if (script.Contains("scrollTo"))
                {
                    ScrollToEnd();
                    return Task.FromResult("true");
                }
            }
            return Task.FromResult("null");
        }

        /// <summary>当前内容完整高度（对应 DOM scrollHeight，无视视口裁剪）。</summary>
        private double MeasureContentHeight()
        {
            if (Content is Control control)
            {
                double width = Bounds.Width;
                if (width <= 0)
                {
                    width = control.DesiredSize.Width;
                }
                control.Measure(new Size(Math.Max(width, 50.0), double.PositiveInfinity));
                return control.DesiredSize.Height;
            }
            return 0.0;
        }

        /// <summary>滚动到底部（对应 window.scrollTo(0, scrollHeight)）。</summary>
        public void ScrollToEnd()
        {
            double target = Math.Max(0.0, Extent.Height - Viewport.Height);
            Offset = new Vector(0, target);
        }
    }
}
