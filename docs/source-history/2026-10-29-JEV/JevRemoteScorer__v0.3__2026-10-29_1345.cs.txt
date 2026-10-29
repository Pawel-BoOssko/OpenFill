// OpenFill - Metadata: wersja 0.3, data 2026-10-29 13:45
// Entire inbound task and entire outbound MCP text are assessed on SATANAMD.
// No API key is stored here; endpoint is private Tailscale.
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OpenFill.Core.Mcp;

public interface IJevMessageInspector
{
    Task<double> InspectAsync(string message, CancellationToken ct);
}

public sealed class JevRemoteScorer : IJevScorer, IJevMessageInspector
{
    private static readonly HttpClient Client = new(new SocketsHttpHandler
    {
        ConnectTimeout = TimeSpan.FromSeconds(5)
    }) { Timeout = TimeSpan.FromSeconds(45) };
    private readonly Uri _base;

    public JevRemoteScorer(string scoreUrl)
    {
        if (!Uri.TryCreate(scoreUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttp || uri.Host != "100.121.221.23" ||
            uri.Port != 43977 || uri.AbsolutePath != "/score" ||
            uri.Query.Length > 0 || uri.Fragment.Length > 0)
            throw new ArgumentException("Invalid private JEV endpoint", nameof(scoreUrl));
        _base = new Uri("http://100.121.221.23:43977/");
    }

    public async Task<IReadOnlyDictionary<string,double>> ScoreAsync(string taskText, CancellationToken ct)
    {
        double p = await SendAsync("score", new { command = taskText }, "client_meddling", ct).ConfigureAwait(false);
        return new Dictionary<string,double> { ["client_meddling"] = p };
    }

    public Task<double> InspectAsync(string message, CancellationToken ct) =>
        SendAsync("inspect", new { message }, "ai_directed_instructions", ct);

    private async Task<double> SendAsync(string path, object payload, string field, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, new Uri(_base, path))
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        using var resp = await Client.SendAsync(req, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode) throw new HttpRequestException(
            "JEV service returned HTTP " + (int)resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var obj = JsonDocument.Parse(body);
        if (!obj.RootElement.TryGetProperty(field, out var node) ||
            node.ValueKind != JsonValueKind.Number || !node.TryGetDouble(out var p) ||
            !double.IsFinite(p) || p < 0 || p > 1)
            throw new InvalidOperationException("JEV returned invalid " + field + " score");
        return p;
    }
}
