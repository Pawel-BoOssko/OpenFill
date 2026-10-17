// OpenFill - Metadata: wersja 0.2, data 2026-10-05 15:15
using System.Text.Json.Nodes;

namespace OpenFill.Core.Cdp;

/// <summary>
/// Higher-level helper over ICdpConnection: keeps the current tab sessionId,
/// calls Runtime.evaluate / Runtime.callFunctionOn and returns the result as a JsonNode.
/// </summary>
public sealed class CdpSession(ICdpConnection conn)
{
    public ICdpConnection Connection { get; } = conn;
    public string? SessionId { get; set; }

    public Task<JsonNode?> SendAsync(string method, JsonObject? @params = null, CancellationToken ct = default)
        => Connection.SendAsync(method, @params, SessionId, ct);

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
