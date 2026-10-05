// OpenFill - Metadata: wersja 0.3, data 2026-10-05 12:00
namespace OpenFill.Core.Model;

/// <summary>
/// Model client that fetches the key at call time. This lets the key be entered in the panel
/// after the app has started, without a restart. The HTTP client is recreated only when the key changes.
/// </summary>
public sealed class KeyedModelClient(Func<string?> keyProvider, Func<string> baseUrlProvider) : IModelClient
{
    private readonly object _lock = new();
    private OpenAIClient? _client;
    private string? _clientKey;
    private string? _clientBase;

    public Task<System.Text.Json.Nodes.JsonObject> CreateResponseAsync(System.Text.Json.Nodes.JsonObject request, CancellationToken ct = default)
    {
        var key = keyProvider();
        if (string.IsNullOrWhiteSpace(key))
            throw new ModelException(0, "No OpenAI key. Enter it in the panel (Settings) or set the OPENAI_API_KEY environment variable.");
        var baseUrl = baseUrlProvider();
        OpenAIClient client;
        lock (_lock)
        {
            if (_client is null || _clientKey != key || _clientBase != baseUrl)
            {
                _client = new OpenAIClient(key, baseUrl);
                _clientKey = key;
                _clientBase = baseUrl;
            }
            client = _client;
        }
        return client.CreateResponseAsync(request, ct);
    }
}
