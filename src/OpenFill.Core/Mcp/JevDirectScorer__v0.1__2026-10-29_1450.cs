// OpenFill - Metadata: wersja 0.1, data 2026-10-29 14:50
// TypeSafe JEV direct API: no SATANAMD and no intermediary service.
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace OpenFill.Core.Mcp;

public sealed class JevDirectScorer : IJevScorer, IJevMessageInspector
{
    public const string Model = "typesafe/jev-1.13";
    public const string Endpoint = "https://openrouter.ai/api/alpha/decisions";
    private static readonly HttpClient Http = new(new SocketsHttpHandler
    {
        ConnectTimeout = TimeSpan.FromSeconds(5)
    }) { Timeout = TimeSpan.FromSeconds(35) };
    private readonly Func<string?> _localKey;
    private readonly JsonNode _inbound;
    private readonly JsonNode _outbound;

    public JevDirectScorer(Func<string?> localKey)
    {
        _localKey = localKey ?? throw new ArgumentNullException(nameof(localKey));
        _inbound = ReadPrompt("OpenFill.Assets.jev_inbound.json", "client_meddling");
        _outbound = ReadPrompt("OpenFill.Assets.jev_outbound.json", "ai_directed_instructions");
    }

    private static JsonNode ReadPrompt(string resource, string question)
    {
        using var input = typeof(JevDirectScorer).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException("Missing embedded JEV prompt: " + question);
        var data = JsonNode.Parse(input) ?? throw new InvalidOperationException("Invalid JEV prompt");
        if (data["model"]?.GetValue<string>() != Model ||
            data["questions"]?[question]?["type"]?.GetValue<string>() != "choice")
            throw new InvalidOperationException("Unexpected embedded JEV prompt schema");
        return data["questions"]!.DeepClone();
    }

    public async Task<IReadOnlyDictionary<string, double>> ScoreAsync(string taskText, CancellationToken ct)
    {
        double p = await DecideAsync("polecenie", taskText, _inbound, "client_meddling", ct);
        return new Dictionary<string,double> { ["client_meddling"] = p };
    }

    public Task<double> InspectAsync(string message, CancellationToken ct) =>
        DecideAsync("wiadomosc", message, _outbound, "ai_directed_instructions", ct);

    private async Task<double> DecideAsync(
        string stateField, string text, JsonNode prompt, string question, CancellationToken ct)
    {
        string? key = _localKey();
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("Local OpenFill JEV API key is not configured");

        var body = new JsonObject
        {
            ["model"] = Model,
            ["state"] = new JsonObject { [stateField] = text },
            ["questions"] = prompt.DeepClone()
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key.Trim());
        request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException("JEV API HTTP " + (int)response.StatusCode);
        using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        if (!doc.RootElement.TryGetProperty("answers", out var answers) ||
            !answers.TryGetProperty(question, out var answer))
            throw new InvalidOperationException("Missing JEV decision answer");

        double p;
        if (answer.TryGetProperty("probabilities", out var probabilities) &&
            probabilities.TryGetProperty("yes", out var yes) && yes.TryGetDouble(out p))
        {
            // exact yes probability
        }
        else if (answer.TryGetProperty("choice", out var chosen) &&
                 answer.TryGetProperty("confidence", out var confidence) &&
                 confidence.TryGetDouble(out var certainty))
        {
            p = chosen.GetString() == "yes" ? certainty :
                chosen.GetString() == "no" ? 1.0 - certainty : double.NaN;
        }
        else throw new InvalidOperationException("Missing JEV yes probability");
        if (!double.IsFinite(p) || p < 0 || p > 1)
            throw new InvalidOperationException("Invalid JEV probability");
        return p;
    }
}
