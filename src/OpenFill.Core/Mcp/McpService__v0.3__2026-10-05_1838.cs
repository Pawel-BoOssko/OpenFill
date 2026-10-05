// OpenFill - Metadata: wersja 0.3, data 2026-10-05 18:38
using System.Security.Cryptography;
using System.Text;
using OpenFill.Core.Config;
using OpenFill.Core.Logging;

namespace OpenFill.Core.Mcp;

/// <summary>
/// Starts everything MCP needs next to the app: secret address, local server, public tunnel and a self-test.
/// The address contains a random secret, because anyone who knows it can drive the browser.
/// The full address is written to runtime\mcp-url.txt; the log never contains the secret.
/// </summary>
public sealed class McpService : IAsyncDisposable
{
    private readonly McpServer _server;
    private readonly CloudflareTunnel? _tunnel;

    public string LocalUrl { get; }
    public string? PublicUrl { get; }
    public McpTaskManager Tasks { get; }

    private McpService(McpServer server, McpTaskManager tasks, CloudflareTunnel? tunnel, string localUrl, string? publicUrl)
    {
        _server = server; Tasks = tasks; _tunnel = tunnel; LocalUrl = localUrl; PublicUrl = publicUrl;
    }

    public static async Task<McpService> StartAsync(IRunHost host, AppPaths paths, AppConfig config, EventLog log,
        Action<string>? announce = null, CancellationToken ct = default)
    {
        var secret = LoadOrCreateSecret(Path.Combine(paths.Runtime, "mcp-secret.txt"));
        var tasks = new McpTaskManager(host, paths.Tasks, TimeSpan.FromMinutes(Math.Max(1, config.CallerAnswerTimeoutMinutes)));
        var server = new McpServer(tasks, secret, config.McpPort, msg => log.Write("mcp", "request", msg, status: "info"), paths.Screenshots);
        server.Start();
        var path = "/mcp/" + secret;
        var localUrl = $"http://127.0.0.1:{server.Port}{path}";
        log.Write("mcp", "start", $"MCP server listening on 127.0.0.1:{server.Port}");

        CloudflareTunnel? tunnel = null;
        string? publicUrl = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(config.McpPublicBaseUrl))
                publicUrl = config.McpPublicBaseUrl.TrimEnd('/') + path;
            else if (config.McpTunnel)
            {
                tunnel = await CloudflareTunnel.StartAsync(server.Port, Path.Combine(paths.Runtime, "bin"),
                    m => log.Write("mcp", "tunnel", m, status: "info"), ct);
                publicUrl = tunnel.Url + path;
            }
        }
        catch (Exception ex)
        {
            log.Write("mcp", "tunnel", "Tunnel failed: " + ex.Message, status: "error");
        }

        var best = publicUrl ?? localUrl;
        server.LinkBase = best;
        try { File.WriteAllText(Path.Combine(paths.Runtime, "mcp-url.txt"), best + Environment.NewLine); } catch { }

        if (publicUrl is not null)
        {
            var ok = await SelfTestAsync(publicUrl, ct);
            log.Write("mcp", "selftest", ok ? "Public address answers MCP requests." : "Public address did not answer the self-test yet.",
                status: ok ? "ok" : "warn");
        }
        log.Write("mcp", "url", "MCP address ready (full address with the secret is in runtime\\mcp-url.txt)", status: "ok");
        announce?.Invoke(best);
        return new McpService(server, tasks, tunnel, localUrl, publicUrl);
    }

    private static string LoadOrCreateSecret(string file)
    {
        try
        {
            if (File.Exists(file))
            {
                var s = File.ReadAllText(file).Trim();
                if (s.Length >= 20) return s;
            }
        }
        catch { }
        var bytes = RandomNumberGenerator.GetBytes(24);
        var secret = Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, secret);
        return secret;
    }

    private static async Task<bool> SelfTestAsync(string url, CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        const string body = """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""";
        for (var i = 0; i < 15 && !ct.IsCancellationRequested; i++)
        {
            try
            {
                using var resp = await http.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json"), ct);
                if (resp.IsSuccessStatusCode && (await resp.Content.ReadAsStringAsync(ct)).Contains("openfill_start_task")) return true;
            }
            catch { }
            try { await Task.Delay(3000, ct); } catch { break; }
        }
        return false;
    }

    public async ValueTask DisposeAsync()
    {
        await _server.DisposeAsync();
        if (_tunnel is not null) await _tunnel.DisposeAsync();
    }
}
