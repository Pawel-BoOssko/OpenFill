// OpenFill - Metadata: wersja 0.2, data 2026-10-05 15:15
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Collections.Concurrent;

namespace OpenFill.Core.Cdp;

/// <summary>
/// CDP connection over a raw WebSocket to /devtools/browser (used by the CLI with Chromium).
/// Supports the "flat" session mode (Target.setAutoAttach flatten=true): one socket, many sessions.
/// </summary>
public sealed class WebSocketCdpConnection : ICdpConnection
{
    private readonly ClientWebSocket _ws = new();
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonNode?>> _pending = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private Task? _recvLoop;
    private int _id;

    public bool IsConnected => _ws.State == WebSocketState.Open;
    /// <summary>Last error of the receive loop (diagnostics for a broken connection).</summary>
    public Exception? LastError { get; private set; }
    public event Action<CdpEvent>? Event;

    public async Task ConnectAsync(string wsUrl, CancellationToken ct = default)
    {
        await _ws.ConnectAsync(new Uri(wsUrl), ct);
        _recvLoop = Task.Run(ReceiveLoop);
    }

    public async Task<JsonNode?> SendAsync(string method, JsonObject? @params = null, string? sessionId = null, CancellationToken ct = default)
    {
        var id = Interlocked.Increment(ref _id);
        var msg = new JsonObject { ["id"] = id, ["method"] = method };
        if (@params is not null) msg["params"] = @params;
        if (sessionId is not null) msg["sessionId"] = sessionId;

        var tcs = new TaskCompletionSource<JsonNode?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;

        var bytes = Encoding.UTF8.GetBytes(msg.ToJsonString());
        // ClientWebSocket does not allow parallel sends, and cancelling a send in progress
        // puts the socket into the Aborted state (breaks the whole connection). So: a send queue
        // and no cancellation token on SendAsync itself - we cancel only the wait for the response.
        await _sendLock.WaitAsync(ct);
        try { await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None); }
        catch { _pending.TryRemove(id, out _); throw; }
        finally { _sendLock.Release(); }

        using var reg = ct.Register(() => { if (_pending.TryRemove(id, out var t)) t.TrySetCanceled(ct); });
        return await tcs.Task;
    }

    private async Task ReceiveLoop()
    {
        var buffer = new ArraySegment<byte>(new byte[64 * 1024]);
        var sb = new StringBuilder();
        try
        {
            while (!_cts.IsCancellationRequested && _ws.State == WebSocketState.Open)
            {
                sb.Clear();
                WebSocketReceiveResult res;
                do
                {
                    res = await _ws.ReceiveAsync(buffer, _cts.Token);
                    if (res.MessageType == WebSocketMessageType.Close) return;
                    sb.Append(Encoding.UTF8.GetString(buffer.Array!, 0, res.Count));
                } while (!res.EndOfMessage);

                Dispatch(sb.ToString());
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { LastError = ex; if (Environment.GetEnvironmentVariable("OPENFILL_DEBUG_CDP") == "1") Console.Error.WriteLine("[cdp] receive loop: " + ex); }
        finally
        {
            foreach (var p in _pending.Values) p.TrySetCanceled();
            _pending.Clear();
        }
    }

    private void Dispatch(string json)
    {
        JsonNode? root;
        try { root = JsonNode.Parse(json); }
        catch { return; }
        if (root is not JsonObject obj) return;

        if (obj.TryGetPropertyValue("id", out var idNode) && idNode is not null)
        {
            var id = idNode.GetValue<int>();
            if (_pending.TryRemove(id, out var tcs))
            {
                if (obj.TryGetPropertyValue("error", out var err) && err is not null)
                    tcs.TrySetException(new CdpException(err!.ToJsonString()));
                else
                    tcs.TrySetResult(obj["result"]);
            }
            return;
        }

        if (obj.TryGetPropertyValue("method", out var m) && m is not null)
        {
            var sid = obj.TryGetPropertyValue("sessionId", out var s) ? s?.GetValue<string>() : null;
            var p = obj["params"] as JsonObject ?? new JsonObject();
            try { Event?.Invoke(new CdpEvent(m.GetValue<string>(), p, sid)); } catch { /* a subscriber must not stop the loop */ }
        }
    }

    public async ValueTask DisposeAsync()
    {
        try { _cts.Cancel(); } catch { }
        try
        {
            if (_ws.State == WebSocketState.Open)
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
        }
        catch { }
        _ws.Dispose();
        _cts.Dispose();
    }
}

public sealed class CdpException(string message) : Exception(message);
