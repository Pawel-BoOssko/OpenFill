// OpenFill - Metadata: wersja 0.2, data 2026-10-05 12:00
using System.Text.Json.Nodes;
using OpenFill.Core.Cdp;
using OpenFill.Core.Model;

namespace OpenFill.Tests;

/// <summary>CDP channel without a browser: every call returns an empty result (for model-loop tests).</summary>
public sealed class NullCdpConnection : ICdpConnection
{
    public event Action<CdpEvent>? Event { add { } remove { } }
    public bool IsConnected => true;
    public Task<JsonNode?> SendAsync(string method, JsonObject? @params = null, string? sessionId = null, CancellationToken ct = default)
        => Task.FromResult<JsonNode?>(new JsonObject());
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>Model that returns pre-scripted responses (Responses API format) and records the requests.</summary>
public sealed class ScriptedModel(params JsonObject[] responses) : IModelClient
{
    public List<JsonObject> Requests { get; } = new();
    private int _i;

    public Task<JsonObject> CreateResponseAsync(JsonObject request, CancellationToken ct = default)
    {
        Requests.Add((JsonObject)request.DeepClone());
        var r = _i < responses.Length ? responses[_i] : responses[^1];
        _i++;
        return Task.FromResult((JsonObject)r.DeepClone());
    }
}

/// <summary>Model that always throws the given exception.</summary>
public sealed class ThrowingModel(Exception ex) : IModelClient
{
    public int Calls { get; private set; }
    public Task<JsonObject> CreateResponseAsync(JsonObject request, CancellationToken ct = default) { Calls++; throw ex; }
}
