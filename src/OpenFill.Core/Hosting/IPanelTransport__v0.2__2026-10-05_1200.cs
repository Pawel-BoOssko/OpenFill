// OpenFill - Metadata: wersja 0.2, data 2026-10-05 12:00
namespace OpenFill.Core.Hosting;

/// <summary>
/// Channel between the core and the panel (HTML). The same panel works in two hosts:
/// in the CLI over WebSocket (PanelServer), in the Windows app via WebView2 (postMessage).
/// Messages are single JSON objects with a "kind" field.
/// </summary>
public interface IPanelTransport
{
    /// <summary>Send a message to all connected panels. Must be safe to call from any thread.</summary>
    void Send(string json);

    /// <summary>Message from the panel (any thread).</summary>
    event Action<string>? Received;

    /// <summary>Whether any panel is currently connected (if not, questions go to the fallback channel).</summary>
    bool HasClients { get; }
}
