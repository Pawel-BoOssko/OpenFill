// OpenFill - Metadata: wersja 0.5, data 2026-10-14 22:35
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OpenFill.Tests;

/// <summary>
/// Serves the testsite directory over HTTP, fakes a small API and provides a local credential-login page.
/// No external account is needed for password-manager regression tests.
/// </summary>
public sealed class StaticSite : IDisposable
{
    private readonly HttpListener _l = new();
    private readonly string _dir;
    private readonly Dictionary<string, string> _loginAccounts = new(StringComparer.OrdinalIgnoreCase);
    public int Port { get; }
    public string BaseUrl => $"http://127.0.0.1:{Port}/";

    public JsonObject? LastProfile { get; private set; }
    public string? LastLoginUser { get; private set; }
    public bool LastLoginOk { get; private set; }

    private static readonly string[] Companies =
        { "Acme Consulting", "Acme Tech", "Acme S.A.", "Lumen Partners", "Accenture", "Allegro" };

    public StaticSite(string dir)
    {
        _dir = dir;
        var tl = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        tl.Start();
        Port = ((IPEndPoint)tl.LocalEndpoint).Port;
        tl.Stop();
        _l.Prefixes.Add($"http://127.0.0.1:{Port}/");
        _l.Start();
        _ = Task.Run(Loop);
    }

    public void AddLoginAccount(string login, string password) => _loginAccounts[login] = password;

    private async Task Loop()
    {
        while (_l.IsListening)
        {
            HttpListenerContext ctx;
            try { ctx = await _l.GetContextAsync(); }
            catch { break; }
            try { await Handle(ctx); }
            catch
            {
                try { ctx.Response.StatusCode = 500; ctx.Response.Close(); }
                catch { }
            }
        }
    }

    private async Task Handle(HttpListenerContext ctx)
    {
        var path = ctx.Request.Url!.AbsolutePath;

        if (path == "/of090-frame-parent")
        {
            var frame = ctx.Request.QueryString["frame"] ?? "";
            if (!Uri.TryCreate(frame, UriKind.Absolute, out var iframeUrl) ||
                iframeUrl.Host != "127.0.0.1" || iframeUrl.Scheme != "http")
            {
                ctx.Response.StatusCode = 400;
                ctx.Response.Close();
                return;
            }
            await Html(ctx, "<!doctype html><html><body><h1>Test hidden modal and iframe</h1>" +
                "<dialog open id='cookie-modal'>Cookie modal <button id='close-cookie' onclick='document.querySelector(\"dialog\").close()'>Close modal</button></dialog>" +
                "<iframe title='Login widget' style='width:650px;height:360px' src='" +
                WebUtility.HtmlEncode(frame) + "'></iframe></body></html>");
            return;
        }

        if (path == "/of090-frame-child")
        {
            await Html(ctx, "<!doctype html><html><body><form onsubmit='event.preventDefault();document.querySelector(\"#result\").textContent=\"LOGIN_SUBMITTED\"'>" +
                "<label>Testowy login <input name='username' autocomplete='username'></label>" +
                "<label>Testowe hasło <input type='password' name='password'></label>" +
                "<button id='submit-login'>Wyślij formularz</button></form><h2 id='result'></h2></body></html>");
            return;
        }

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

        if (path == "/credential-login" && ctx.Request.HttpMethod == "GET")
        {
            await Html(ctx, """
<!doctype html>
<html><head><meta charset="utf-8"><title>OpenFill credential test</title></head>
<body>
  <h1>Credential test login</h1>
  <form method="post" action="/credential-login">
    <label>Login or email <input name="username" id="username" autocomplete="username"></label>
    <label>Password <input type="password" name="password" id="password" autocomplete="current-password"></label>
    <button type="submit" id="login-button">Sign in</button>
  </form>
</body></html>
""");
            return;
        }

        if (path == "/credential-login" && ctx.Request.HttpMethod == "POST")
        {
            using var r = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding ?? Encoding.UTF8);
            var body = await r.ReadToEndAsync();
            var form = ParseForm(body);
            var login = form.TryGetValue("username", out var u) ? u : "";
            var password = form.TryGetValue("password", out var p) ? p : "";
            LastLoginUser = login;
            LastLoginOk = _loginAccounts.TryGetValue(login, out var expected) && expected == password;
            if (LastLoginOk)
                await Html(ctx, "<!doctype html><html><body><h1>LOGIN_OK: " + WebUtility.HtmlEncode(login) + "</h1></body></html>");
            else
            {
                ctx.Response.StatusCode = 401;
                await Html(ctx, "<!doctype html><html><body><h1>LOGIN_FAILED</h1></body></html>", preserveStatus: true);
            }
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

    private static Dictionary<string, string> ParseForm(string body)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in body.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var key = WebUtility.UrlDecode(parts[0].Replace("+", " "));
            var value = parts.Length > 1 ? WebUtility.UrlDecode(parts[1].Replace("+", " ")) : "";
            result[key] = value;
        }
        return result;
    }

    private static async Task Ok(HttpListenerContext ctx, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        ctx.Response.ContentType = "application/json; charset=utf-8";
        ctx.Response.ContentLength64 = bytes.Length;
        await ctx.Response.OutputStream.WriteAsync(bytes);
        ctx.Response.Close();
    }

    private static async Task Html(HttpListenerContext ctx, string html, bool preserveStatus = false)
    {
        var status = ctx.Response.StatusCode;
        var bytes = Encoding.UTF8.GetBytes(html);
        if (!preserveStatus) ctx.Response.StatusCode = 200;
        else ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "text/html; charset=utf-8";
        ctx.Response.ContentLength64 = bytes.Length;
        await ctx.Response.OutputStream.WriteAsync(bytes);
        ctx.Response.Close();
    }

    public void Dispose()
    {
        try { _l.Stop(); }
        catch { }
    }
}
