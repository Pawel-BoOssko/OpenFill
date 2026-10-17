// OpenFill - Metadata: wersja 0.2, data 2026-10-05 15:15
using System.Net;
using System.Net.WebSockets;
using System.Text;
using OpenFill.Core.Assets;
using OpenFill.Core.Hosting;

namespace OpenFill.Cli;

/// <summary>
/// Panel server for the CLI: serves panel.html at "/" and a two-way JSON channel over WebSocket at "/panel".
/// Implements IPanelTransport, so AppHost does not know whether it talks to WebView2 or to a browser.
/// </summary>
public sealed class PanelServer : IPanelTransport, IAsyncDisposable
{
    private readonly HttpListener _listener = new();
    private readonly List<Client> _clients = new();
    private readonly object _lock = new();
    private readonly string _html;
    private CancellationTokenSource? _cts;
    public int Port { get; }

    public event Action<string>? Received;

    public bool HasClients { get { lock (_lock) return _clients.Any(c => c.Ws.State == WebSocketState.Open); } }

    private sealed class Client(WebSocket ws)
    {
        public WebSocket Ws { get; } = ws;
        // ClientWebSocket/HttpListenerWebSocket do not allow parallel sends - we queue them.
        public SemaphoreSlim SendLock { get; } = new(1, 1);
    }

    public PanelServer(int port = 0)
    {
        Port = port == 0 ? GetFreePort() : port;
        _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
        _html = AssetLoader.Load("panel.html");
    }

    public string Url => $"http://127.0.0.1:{Port}/";

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _listener.Start();
        _ = Task.Run(() => AcceptLoop(_cts.Token));
    }

    public void Send(string json)
    {
        List<Client> clients;
        lock (_lock) clients = _clients.Where(c => c.Ws.State == WebSocketState.Open).ToList();
        var bytes = Encoding.UTF8.GetBytes(json);
        foreach (var c in clients) _ = SendTo(c, bytes);
    }

    private static async Task SendTo(Client c, byte[] bytes)
    {
        await c.SendLock.WaitAsync();
        try { await c.Ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None); }
        catch { /* a disconnected panel does not stop the work */ }
        finally { c.SendLock.Release(); }
    }

    private async Task AcceptLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try { ctx = await _listener.GetContextAsync(); }
            catch { break; }

            if (ctx.Request.Url?.AbsolutePath == "/panel" && ctx.Request.IsWebSocketRequest)
            {
                _ = Task.Run(() => HandleSocket(ctx, ct), ct);
            }
            else
            {
                try
                {
                    var bytes = Encoding.UTF8.GetBytes(_html);
                    ctx.Response.ContentType = "text/html; charset=utf-8";
                    ctx.Response.Headers["Cache-Control"] = "no-store";
                    ctx.Response.ContentLength64 = bytes.Length;
                    await ctx.Response.OutputStream.WriteAsync(bytes, ct);
                    ctx.Response.Close();
                }
                catch { }
            }
        }
    }

    private async Task HandleSocket(HttpListenerContext ctx, CancellationToken ct)
    {
        WebSocket ws;
        try { ws = (await ctx.AcceptWebSocketAsync(null)).WebSocket; }
        catch { return; }
        var client = new Client(ws);
        lock (_lock) _clients.Add(client);

        var buffer = new byte[64 * 1024];
        var sb = new StringBuilder();
        try
        {
            while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                sb.Clear();
                WebSocketReceiveResult res;
                do
                {
                    res = await ws.ReceiveAsync(buffer, ct);
                    if (res.MessageType == WebSocketMessageType.Close) return;
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, res.Count));
                } while (!res.EndOfMessage);
                try { Received?.Invoke(sb.ToString()); } catch { }
            }
        }
        catch { }
        finally
        {
            lock (_lock) _clients.Remove(client);
            try { ws.Dispose(); } catch { }
        }
    }

    private static int GetFreePort()
    {
        var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var p = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return p;
    }

    public async ValueTask DisposeAsync()
    {
        try { _cts?.Cancel(); } catch { }
        List<Client> clients;
        lock (_lock) clients = _clients.ToList();
        foreach (var c in clients)
            try { await c.Ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None); } catch { }
        try { _listener.Stop(); } catch { }
    }
}
