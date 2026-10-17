// OpenFill - Metadata: wersja 0.2, data 2026-10-05 15:15
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using OpenFill.Core.Hosting;

namespace OpenFill.App;

/// <summary>
/// Panel <-> core channel through WebView2: PostWebMessageAsJson to the panel, WebMessageReceived from the panel.
/// The panel (the same HTML as in the CLI) detects chrome.webview itself and uses postMessage instead of a WebSocket.
/// </summary>
public sealed class WebViewPanelTransport : IPanelTransport
{
    private readonly WebView2 _view;
    private volatile bool _ready;

    public event Action<string>? Received;
    public bool HasClients => _ready;

    public WebViewPanelTransport(WebView2 view)
    {
        _view = view;
        _view.CoreWebView2.WebMessageReceived += OnMessage;
        _view.CoreWebView2.NavigationStarting += (_, _) => _ready = false;
    }

    private void OnMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string json;
        try { json = e.WebMessageAsJson; } catch { return; }
        if (json.Contains("\"hello\"")) _ready = true;
        var handler = Received;
        if (handler is null) return;
        // Handled off the UI thread: AppHost may call CDP, which returns to the UI thread by itself.
        Task.Run(() => { try { handler(json); } catch { } });
    }

    public void Send(string json)
    {
        void Post()
        {
            try { _view.CoreWebView2?.PostWebMessageAsJson(json); } catch { /* panel is reloading */ }
        }
        try
        {
            if (_view.IsDisposed) return;
            if (_view.InvokeRequired) _view.BeginInvoke(new Action(Post));
            else Post();
        }
        catch { }
    }
}
