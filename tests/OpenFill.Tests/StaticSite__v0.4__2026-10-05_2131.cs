// OpenFill - Metadata: wersja 0.4, data 2026-10-05 21:31
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OpenFill.Tests;

/// <summary>
/// Serves the testsite directory over HTTP (CDP Network does not always capture file://) and fakes a simple API:
/// GET /api/companies?q= (suggestions), POST /api/profile (profile save).
/// </summary>
public sealed class StaticSite : IDisposable
{
    private readonly HttpListener _l = new();
    private readonly string _dir;
    public int Port { get; }
    public string BaseUrl => $"http://127.0.0.1:{Port}/";

    /// <summary>Last POST /api/profile body (for assertions in tests).</summary>
    public JsonObject? LastProfile { get; private set; }

    private static readonly string[] Companies =
        { "Acme Consulting", "Acme Tech", "Acme S.A.", "Lumen Partners", "Accenture", "Allegro" };

    public StaticSite(string dir)
    {
        _dir = dir;
        var tl = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        tl.Start(); Port = ((IPEndPoint)tl.LocalEndpoint).Port; tl.Stop();
        _l.Prefixes.Add($"http://127.0.0.1:{Port}/");
        _l.Start();
        _ = Task.Run(Loop);
    }

    private async Task Loop()
    {
        while (_l.IsListening)
        {
            HttpListenerContext ctx;
            try { ctx = await _l.GetContextAsync(); } catch { break; }
            try { await Handle(ctx); } catch { try { ctx.Response.StatusCode = 500; ctx.Response.Close(); } catch { } }
        }
    }

    private async Task Handle(HttpListenerContext ctx)
    {
        var path = ctx.Request.Url!.AbsolutePath;
        if (path == "/api/companies")
        {
            var q = ctx.Request.QueryString["q"] ?? "";
            await Ok(ctx, JsonSerializer.Serialize(Companies.Where(c => c.StartsWith(q, StringComparison.OrdinalIgnoreCase)).ToArray()));
            return;
        }
        if (path == "/api/profile" && ctx.Request.HttpMethod == "POST")
        {
            using var r = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
            LastProfile = JsonNode.Parse(await r.ReadToEndAsync()) as JsonObject;
            await Ok(ctx, """{"ok":true}""");
            return;
        }

        var rel = Uri.UnescapeDataString(path.TrimStart('/'));
        if (string.IsNullOrEmpty(rel)) rel = "index.html";
        var file = Path.Combine(_dir, rel);
        if (File.Exists(file))
        {
            var bytes = await File.ReadAllBytesAsync(file);
            ctx.Response.ContentType = file.EndsWith(".html") ? "text/html; charset=utf-8" : "application/octet-stream";
            ctx.Response.ContentLength64 = bytes.Length;
            await ctx.Response.OutputStream.WriteAsync(bytes);
        }
        else ctx.Response.StatusCode = 404;
        ctx.Response.Close();
    }

    private static async Task Ok(HttpListenerContext ctx, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        ctx.Response.ContentType = "application/json; charset=utf-8";
        ctx.Response.ContentLength64 = bytes.Length;
        await ctx.Response.OutputStream.WriteAsync(bytes);
        ctx.Response.Close();
    }

    public void Dispose() { try { _l.Stop(); } catch { } }
}
