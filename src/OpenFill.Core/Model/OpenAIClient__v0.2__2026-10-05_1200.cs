// OpenFill - Metadata: wersja 0.2, data 2026-10-05 12:00
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OpenFill.Core.Model;

/// <summary>
/// Minimal OpenAI Responses API client. Works on raw JSON (JsonNode), without an SDK,
/// so it does not depend on external packages and is easy to swap for a mock in tests.
///
/// Format (confirmed in the docs, 2026-10): tools of type "function" with the fields
/// name/description/parameters/strict; tool result sent as an input item
/// {"type":"function_call_output","call_id":...,"output":...}; without previous_response_id
/// all output items of the previous response (including reasoning) go back into input.
/// </summary>
public interface IModelClient
{
    Task<JsonObject> CreateResponseAsync(JsonObject request, CancellationToken ct = default);
}

public sealed class OpenAIClient : IModelClient
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public OpenAIClient(string apiKey, string baseUrl = "https://api.openai.com/v1", HttpClient? http = null)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _http = http ?? new HttpClient();
        _http.Timeout = TimeSpan.FromMinutes(10);
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<JsonObject> CreateResponseAsync(JsonObject request, CancellationToken ct = default)
    {
        using var msg = new HttpRequestMessage(HttpMethod.Post, _baseUrl + "/responses")
        {
            Content = new StringContent(request.ToJsonString(), Encoding.UTF8, "application/json")
        };
        using var resp = await _http.SendAsync(msg, HttpCompletionOption.ResponseContentRead, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new ModelException((int)resp.StatusCode, body);
        return JsonNode.Parse(body) as JsonObject ?? new JsonObject();
    }
}

public sealed class ModelException(int status, string body) : Exception($"OpenAI HTTP {status}: {body}")
{
    public int Status { get; } = status;
    public string Body { get; } = body;
}
