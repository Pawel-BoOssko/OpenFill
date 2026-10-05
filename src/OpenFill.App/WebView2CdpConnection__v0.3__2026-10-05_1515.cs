// OpenFill - Metadata: wersja 0.3, data 2026-10-05 15:15
using System.Text.Json.Nodes;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using OpenFill.Core.Cdp;

namespace OpenFill.App;

/// <summary>
/// CDP channel to the page in a WebView2 control. The same abstraction as the WebSocket in the CLI, so the whole core
/// (extractor, actions, network, console, model loop) works unchanged.
///
/// WebView2 requires every call to come from the UI thread - the model loop runs in the background, so every
/// call is passed to the UI thread (BeginInvoke) and the result comes back through a TaskCompletionSource.
/// CDP events have to be subscribed to by name up front (list below).
/// </summary>
public sealed class WebView2CdpConnection : ICdpConnection
{
    /// <summary>Events used by the core (NetworkMonitor, ConsoleMonitor, navigation).</summary>
    public static readonly string[] SubscribedEvents =
    {
        "Network.requestWillBeSent", "Network.responseReceived", "Network.loadingFinished", "Network.loadingFailed",
        "Network.webSocketCreated", "Runtime.consoleAPICalled", "Runtime.exceptionThrown", "Log.entryAdded",
        "Page.loadEventFired", "Page.frameNavigated", "Page.javascriptDialogOpening"
    };

    private readonly Control _ui;
    private readonly CoreWebView2 _core;
    private readonly List<(CoreWebView2DevToolsProtocolEventReceiver receiver, EventHandler<CoreWebView2DevToolsProtocolEventReceivedEventArgs> handler)> _subs = new();
    private volatile bool _disposed;

    public event Action<CdpEvent>? Event;
    public bool IsConnected => !_disposed;

    public WebView2CdpConnection(Control ui, CoreWebView2 core)
    {
        _ui = ui;
        _core = core;
        foreach (var name in SubscribedEvents)
        {
            var receiver = core.GetDevToolsProtocolEventReceiver(name);
            var eventName = name;
            EventHandler<CoreWebView2DevToolsProtocolEventReceivedEventArgs> handler = (_, e) => Raise(eventName, e.ParameterObjectAsJson);
            receiver.DevToolsProtocolEventReceived += handler;
            _subs.Add((receiver, handler));
        }
    }

    private void Raise(string method, string json)
    {
        if (_disposed) return;
        JsonObject p;
        try { p = JsonNode.Parse(json) as JsonObject ?? new JsonObject(); }
        catch { return; }
        try { Event?.Invoke(new CdpEvent(method, p, null)); } catch { /* a subscriber must not stop the flow */ }
    }

    public async Task<JsonNode?> SendAsync(string method, JsonObject? @params = null, string? sessionId = null, CancellationToken ct = default)
    {
        if (_disposed) throw new CdpException("Connection to the browser is closed.");
        var tcs = new TaskCompletionSource<JsonNode?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var paramsJson = @params?.ToJsonString() ?? "{}";

        void Call()
        {
            // async void on the UI thread - all exceptions are caught and go to the tcs.
            _ = CallOnUi();
            async Task CallOnUi()
            {
                try
                {
                    var json = await _core.CallDevToolsProtocolMethodAsync(method, paramsJson);
                    tcs.TrySetResult(string.IsNullOrEmpty(json) ? null : JsonNode.Parse(json));
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(new CdpException($"{method}: {ex.Message}"));
                }
            }
        }

        if (_ui.InvokeRequired) _ui.BeginInvoke(new Action(Call));
        else Call();

        using var reg = ct.Register(() => tcs.TrySetCanceled(ct));
        return await tcs.Task;
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;
        void Unsubscribe()
        {
            foreach (var (receiver, handler) in _subs)
                try { receiver.DevToolsProtocolEventReceived -= handler; } catch { }
            _subs.Clear();
        }
        try
        {
            if (_ui.IsHandleCreated && _ui.InvokeRequired) _ui.BeginInvoke(new Action(Unsubscribe));
            else Unsubscribe();
        }
        catch { }
        return ValueTask.CompletedTask;
    }
}
