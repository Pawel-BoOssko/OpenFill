// OpenFill - Metadata: wersja 0.2, data 2026-10-05 15:15
using System.Text.Json.Nodes;

namespace OpenFill.Core.Cdp;

/// <summary>
/// Higher-level helper over ICdpConnection: keeps the current tab sessionId,
/// calls Runtime.evaluate / Runtime.callFunctionOn and returns the result as a JsonNode.
/// </summary>
// E03: per-async-flow telemetry, no global counters and no page content in logs.
// These are CDP requests made through CdpSession during read tools, not a
// claim about unique DOM snapshots or calls made directly through ICdpConnection.
public sealed class CdpReadCallScope : IDisposable
{
    private static readonly AsyncLocal<CdpReadCallScope?> Current = new();
    private readonly CdpReadCallScope? _previous;
    private int _calls;

    public int Calls => Volatile.Read(ref _calls);

    public CdpReadCallScope()
    {
        _previous = Current.Value;
        Current.Value = this;
    }

    internal static void Track(string method)
    {
        // Runtime.evaluate is the primary physical read path. DOM methods
        // below are explicit read operations. Mutating CDP methods are excluded.
        if (method is not ("Runtime.evaluate" or "Runtime.callFunctionOn"
            or "DOM.getDocument" or "DOM.querySelector" or "DOM.querySelectorAll"
            or "DOM.getOuterHTML" or "DOM.describeNode")) return;
        if (Current.Value is { } scope) Interlocked.Increment(ref scope._calls);
    }

    public void Dispose() => Current.Value = _previous;
}

public sealed class CdpSession(ICdpConnection conn)
{
    public ICdpConnection Connection { get; } = conn;
    public string? SessionId { get; set; }

    public Task<JsonNode?> SendAsync(string method, JsonObject? @params = null, CancellationToken ct = default)
    {
        // E03: count actual CDP read-like requests only inside an explicit read-tool scope.
        CdpReadCallScope.Track(method);
        return Connection.SendAsync(method, @params, SessionId, ct);
    }

    /// <summary>Runs a JS expression and returns the result as JSON (objects are deep-serialized).</summary>
    public async Task<JsonNode?> EvaluateAsync(string expression, bool awaitPromise = true, int timeoutMs = 30000, CancellationToken ct = default)
    {
        var res = await SendAsync("Runtime.evaluate", new JsonObject
        {
            ["expression"] = expression,
            ["returnByValue"] = true,
            ["awaitPromise"] = awaitPromise,
            ["timeout"] = timeoutMs,
            ["userGesture"] = true
        }, ct);

        if (res is JsonObject obj)
        {
            if (obj["exceptionDetails"] is JsonObject ex)
            {
                var text = ex["exception"]?["description"]?.GetValue<string>()
                           ?? ex["text"]?.GetValue<string>() ?? "JS exception";
                throw new CdpException(text);
            }
            return obj["result"]?["value"];
        }
        return null;
    }

    /// <summary>Injects a JS function (called with an object argument) and returns the result.</summary>
    public Task<JsonNode?> CallAsync(string functionBody, JsonObject arg, int timeoutMs = 30000, CancellationToken ct = default)
    {
        // The JS function takes one argument "arg" (an object). We serialize it safely.
        var argJson = arg.ToJsonString();
        var expr = $"(function(){{ const arg = {argJson}; return (async () => {{ return await ({functionBody})(arg); }})(); }})()";
        return EvaluateAsync(expr, awaitPromise: true, timeoutMs, ct);
    }
}
