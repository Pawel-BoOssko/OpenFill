// OF-ARCHFLOW: DEV-only owner for the CDP local-plan browser in an initialized WebView2.
// Source-only entry point, not registered in production AgentRunner or ToolRegistry.
#if DEBUG
using Microsoft.Web.WebView2.WinForms;
using OpenFill.Core.Agent;
using OpenFill.Core.Cdp;

namespace OpenFill.App;

public sealed class LocalPlanWebView2DevSession : IAsyncDisposable
{
    private readonly WebView2CdpConnection _connection;
    public LocalPlanCdpBrowser Browser { get; }

    private LocalPlanWebView2DevSession(WebView2CdpConnection connection)
    {
        _connection = connection;
        Browser = new LocalPlanCdpBrowser(new CdpSession(connection));
    }

    public static LocalPlanWebView2DevSession Attach(WebView2 control)
    {
        if (control.CoreWebView2 is null)
            throw new InvalidOperationException("archflow_dev_webview2_not_initialized");
        if (!control.IsHandleCreated)
            throw new InvalidOperationException("archflow_dev_webview2_handle_missing");
        return new LocalPlanWebView2DevSession(
            new WebView2CdpConnection(control, control.CoreWebView2));
    }

    public ValueTask DisposeAsync() => _connection.DisposeAsync();
}
#endif
