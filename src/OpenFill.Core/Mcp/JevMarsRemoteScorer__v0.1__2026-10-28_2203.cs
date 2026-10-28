// OpenFill - Metadata: wersja 0.1, data 2026-10-28 22:03
// JEV scorer: sends complete task text to the private SATANAMD JEV endpoint.
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OpenFill.Core.Mcp;

public sealed class JevMarsRemoteScorer : IJevScorer
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(45) };
    private readonly Uri _url;

    public JevMarsRemoteScorer(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttp || uri.Host != "100.121.221.23" ||
            uri.Port != 43977 || uri.AbsolutePath != "/score")
            throw new ArgumentException("Invalid configured private JEV service address", nameof(url));
        _url = uri;
    }

    public async Task<IReadOnlyDictionary<string, double>> ScoreAsync(string taskText, CancellationToken cancellationToken)
    {
        // Do not trim or split: the user requires the entire original command.
        var payload = JsonSerializer.Serialize(new { command = taskText });
        using var request = new HttpRequestMessage(HttpMethod.Post, _url)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        using var response = await Client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Private JEV service unavailable: HTTP " + (int)response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("mars_people", out var scoreElement) ||
            scoreElement.ValueKind != JsonValueKind.Number || !scoreElement.TryGetDouble(out var score) ||
            !double.IsFinite(score) || score < 0 || score > 1)
            throw new InvalidOperationException("Private JEV service returned invalid score");
        return new Dictionary<string, double> { ["mars_people"] = score };
    }
}
