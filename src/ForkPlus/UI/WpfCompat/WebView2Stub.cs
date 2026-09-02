// WPF → Avalonia 迁移兼容层：WebView2 占位实现
// 原工程用 Microsoft.Web.WebView2（Windows-only WPF 控件）渲染 AI 对话的 markdown→HTML。
// Avalonia 侧暂无内置 WebView；迁移期用纯文本占位控件保住调用面，HTML 以去标签文本显示。
// Migration note：接入跨平台 WebView（如 Avalonia WebView / CefNet / Native WebView handler）后删除本文件。

using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace Microsoft.Web.WebView2.Core
{
    /// <summary>CoreWebView2Profile.PreferredColorScheme 枚举（stub）。</summary>
    public enum CoreWebView2PreferredColorScheme
    {
        Auto = 0,
        Light = 1,
        Dark = 2,
    }

    /// <summary>CoreWebView2Profile stub。</summary>
    public sealed class CoreWebView2Profile
    {
        public CoreWebView2PreferredColorScheme PreferredColorScheme { get; set; } = CoreWebView2PreferredColorScheme.Auto;
    }

    /// <summary>CoreWebView2Environment stub（WebView2EnvironmentHelper 返回值形状）。</summary>
    public sealed class CoreWebView2Environment
    {
        /// <summary>原 WinRT API CoreWebView2Environment.CreateAsync（stub 返回空环境）。</summary>
        public static Task<CoreWebView2Environment> CreateAsync(string browserExecutableFolder = null, string userDataFolder = null)
            => Task.FromResult(new CoreWebView2Environment());
    }

    public class CoreWebView2ContextMenuRequestedEventArgs : EventArgs
    {
        public bool Handled { get; set; }
    }

    public class CoreWebView2NavigationCompletedEventArgs : EventArgs
    {
        public bool IsSuccess { get; set; } = true;
    }

    public class CoreWebView2WebMessageReceivedEventArgs : EventArgs
    {
        public string Message { get; }
        public CoreWebView2WebMessageReceivedEventArgs(string message) { Message = message; }
        public string TryGetWebMessageAsString() => Message;
    }

    /// <summary>CoreWebView2 stub：脚本执行 no-op，事件由 NavigateToString 驱动。</summary>
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
    /// WebView2 占位控件：NavigateToString 以去标签纯文本显示 HTML，
    /// 其余（脚本、右键菜单、web message）为 no-op / 不触发。
    /// </summary>
    public class WebView2 : global::Avalonia.Controls.TextBlock
    {
        private CoreWebView2 _core;

        public CoreWebView2 CoreWebView2 => _core ??= new CoreWebView2();

        /// <summary>WPF WebView2.DefaultBackgroundColor（stub 仅存值，透明背景由 TextBlock 默认呈现）。</summary>
        public System.Drawing.Color DefaultBackgroundColor { get; set; } = System.Drawing.Color.Transparent;

        /// <summary>WPF WebView2.Dispose()（TextBlock 非 IDisposable，stub 空实现）。</summary>
        public void Dispose() { }

        public Task EnsureCoreWebView2Async(object userData = null) => Task.CompletedTask;

        public void NavigateToString(string html)
        {
            Text = StripHtml(html);
            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap;
            CoreWebView2.RaiseNavigationCompleted(success: true);
        }

        public Task<string> ExecuteScriptAsync(string script) => CoreWebView2.ExecuteScriptAsync(script);

        private static string StripHtml(string html)
        {
            if (string.IsNullOrEmpty(html)) return string.Empty;
            var text = Regex.Replace(html, "<(script|style)[\\s\\S]*?</\\1>", string.Empty, RegexOptions.IgnoreCase);
            text = Regex.Replace(text, "<br\\s*/?>", "\n", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, "</(p|div|li|h[1-6]|tr)>", "\n", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, "<[^>]+>", string.Empty);
            return System.Net.WebUtility.HtmlDecode(text).Trim();
        }
    }
}
