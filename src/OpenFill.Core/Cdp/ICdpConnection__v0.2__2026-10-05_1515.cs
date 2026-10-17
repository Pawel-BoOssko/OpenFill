// OpenFill - Metadata: wersja 0.2, data 2026-10-05 15:15
using System.Text.Json.Nodes;

namespace OpenFill.Core.Cdp;

/// <summary>
/// Channel to the Chrome DevTools Protocol. One abstraction for two worlds:
/// a raw WebSocket (Chromium in the CLI) and WebView2 (the Windows app).
/// </summary>
public interface ICdpConnection : IAsyncDisposable
{
    /// <summary>Calls a CDP method. sessionId is for methods tied to a specific tab/target.</summary>
    Task<JsonNode?> SendAsync(string method, JsonObject? @params = null, string? sessionId = null, CancellationToken ct = default);

    /// <summary>CDP events (method, params, sessionId).</summary>
    event Action<CdpEvent>? Event;

    bool IsConnected { get; }
}

public sealed record CdpEvent(string Method, JsonObject Params, string? SessionId);
