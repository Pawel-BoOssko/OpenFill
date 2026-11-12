// 2026-11-12 14:25 CET | GPT-6 | OpenFill | model-free, loopback-only browser scripting API.
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace OpenFill.App;

/// <summary>Deterministic localhost commands. No AgentRunner, model client or MCP task is invoked.</summary>
internal sealed class DirectBrowserApi : IAsyncDisposable
{
    public const int DefaultPort = 17873;
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly Func<JsonObject, Task<JsonObject>> _execute;
    private readonly Task _acceptTask;
    private readonly string _token;
    public int Port { get; }
    public string TokenFile { get; }
    public string Address => $"http://127.0.0.1:{Port}/";

    public DirectBrowserApi(string runtimeDirectory, Func<JsonObject, Task<JsonObject>> execute, int port = DefaultPort)
    {
        _execute = execute;
        Port = port;
        Directory.CreateDirectory(runtimeDirectory);
        TokenFile = Path.Combine(runtimeDirectory, "direct-browser-api.token");
        if (!File.Exists(TokenFile))
            File.WriteAllText(TokenFile, Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), Encoding.ASCII);
        _token = File.ReadAllText(TokenFile, Encoding.ASCII).Trim();
        _listener.Prefixes.Add(Address);
        _listener.Start();
        _acceptTask = Task.Run(AcceptAsync);
    }

    private async Task AcceptAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            HttpListenerContext context;
            try { context = await _listener.GetContextAsync().WaitAsync(_stop.Token); }
            catch (OperationCanceledException) { return; }
            catch (HttpListenerException) { return; }
            catch (ObjectDisposedException) { return; }
            _ = Task.Run(() => HandleAsync(context));
        }
    }

    private async Task HandleAsync(HttpListenerContext ctx)
    {
        try
        {
            var req = ctx.Request;
            if (req.HttpMethod == "GET" && req.Url?.AbsolutePath == "/health")
            {
                await ReplyAsync(ctx, 200, new JsonObject { ["ok"] = true, ["service"] = "OpenFill direct-browser-api" });
                return;
            }

            var supplied = req.Headers["X-OpenFill-Token"] ?? "";
            var expected = Encoding.UTF8.GetBytes(_token);
            var actual = Encoding.UTF8.GetBytes(supplied);
            if (expected.Length != actual.Length || !CryptographicOperations.FixedTimeEquals(expected, actual))
            {
                await ReplyAsync(ctx, 401, new JsonObject { ["ok"] = false, ["error"] = "UNAUTHORIZED" });
                return;
            }
            if (req.HttpMethod != "POST" || req.Url?.AbsolutePath != "/command")
            {
                await ReplyAsync(ctx, 404, new JsonObject { ["ok"] = false, ["error"] = "UNKNOWN_ROUTE" });
                return;
            }
            if (req.ContentLength64 > 1024 * 1024)
            {
                await ReplyAsync(ctx, 413, new JsonObject { ["ok"] = false, ["error"] = "BODY_TOO_LARGE" });
                return;
            }
            using var reader = new StreamReader(req.InputStream, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();
            if (body.Length > 1024 * 1024)
            {
                await ReplyAsync(ctx, 413, new JsonObject { ["ok"] = false, ["error"] = "BODY_TOO_LARGE" });
                return;
            }
            if (JsonNode.Parse(body) is not JsonObject cmd)
            {
                await ReplyAsync(ctx, 400, new JsonObject { ["ok"] = false, ["error"] = "INVALID_JSON_OBJECT" });
                return;
            }
            var result = await _execute(cmd).WaitAsync(TimeSpan.FromSeconds(60), _stop.Token);
            await ReplyAsync(ctx, 200, result);
        }
        catch (TimeoutException)
        {
            await SafeReplyAsync(ctx, 504, "COMMAND_TIMEOUT");
        }
        catch (OperationCanceledException)
        {
            await SafeReplyAsync(ctx, 503, "SERVICE_STOPPING");
        }
        catch (Exception ex)
        {
            await SafeReplyAsync(ctx, 500, "DIRECT_COMMAND_FAILED: " + ex.Message);
        }
    }

    private static async Task SafeReplyAsync(HttpListenerContext ctx, int status, string error)
    {
        try { await ReplyAsync(ctx, status, new JsonObject { ["ok"] = false, ["error"] = error }); }
        catch { /* client disconnected */ }
    }

    private static async Task ReplyAsync(HttpListenerContext ctx, int status, JsonObject payload)
    {
        var bytes = Encoding.UTF8.GetBytes(payload.ToJsonString());
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        ctx.Response.ContentLength64 = bytes.Length;
        await ctx.Response.OutputStream.WriteAsync(bytes);
        ctx.Response.Close();
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        _listener.Close();
        try { await _acceptTask.WaitAsync(TimeSpan.FromSeconds(2)); } catch { }
        _stop.Dispose();
    }
}

