// OpenFill - Metadata: wersja 0.3, data 2026-10-05 17:09
using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using OpenFill.Core.Cdp;
using OpenFill.Core.Logging;

namespace OpenFill.Core.Browser;

/// <summary>One recorded network flow (request + response), framed by requestId.</summary>
public sealed class NetworkEntry
{
    public string RequestId = "";
    public string Url = "";
    public string Method = "";
    public string ResourceType = "";
    public int Status;
    public double StartedMs;
    public double? FinishedMs;
    public string? MimeType;
    public bool Failed;
    public string? ErrorText;
    public bool BodyAvailable;
    public long EncodedLength;

    public string Host
    {
        get { try { return new Uri(Url).Host; } catch { return ""; } }
    }
}

/// <summary>
/// Tracks the page network traffic through CDP Network.*. Frames by requestId, remembers the last N entries,
/// lets the model fetch a response body on demand (Network.getResponseBody). Data is redacted before logging.
///
/// This generalizes the Open Browser approach: there the stream was assembled by message.id under ChatGPT,
/// here we frame any traffic by requestId and assume nothing about the format of a specific page.
/// </summary>
public sealed class NetworkMonitor(CdpSession session, EventLog log, int capacity = 400)
{
    private readonly ConcurrentDictionary<string, NetworkEntry> _byId = new();
    private readonly LinkedList<NetworkEntry> _recent = new();
    private readonly object _lock = new();
    private double _epochMs;

    private bool _subscribed;
    public bool Enabled { get; private set; }

    public async Task EnableAsync(CancellationToken ct = default)
    {
        if (!_subscribed) { _subscribed = true; session.Connection.Event += OnEvent; }
        await session.SendAsync("Network.enable", new JsonObject
        {
            ["maxTotalBufferSize"] = 20_000_000,
            ["maxResourceBufferSize"] = 10_000_000
        }, ct);
        Enabled = true;
    }

    private long _activity;
    /// <summary>Counter of every network event (grows monotonically) - used to detect quiet.</summary>
    public long Activity => Interlocked.Read(ref _activity);

    private void Track(NetworkEntry e)
    {
        lock (_lock)
        {
            _byId[e.RequestId] = e;
            _recent.AddLast(e);
            while (_recent.Count > capacity)
            {
                var first = _recent.First!.Value;
                _recent.RemoveFirst();
                _byId.TryRemove(first.RequestId, out _);
            }
        }
    }

    private void OnEvent(CdpEvent ev)
    {
        if (ev.SessionId is not null && session.SessionId is not null && ev.SessionId != session.SessionId) return;
        if (ev.Method.StartsWith("Network.", StringComparison.Ordinal)) Interlocked.Increment(ref _activity);
        try
        {
            switch (ev.Method)
            {
                case "Network.requestWillBeSent":
                {
                    var id = ev.Params["requestId"]?.GetValue<string>() ?? return_empty();
                    var req = ev.Params["request"] as JsonObject;
                    var e = _byId.TryGetValue(id, out var ex) ? ex : new NetworkEntry { RequestId = id };
                    e.Url = req?["url"]?.GetValue<string>() ?? e.Url;
                    e.Method = req?["method"]?.GetValue<string>() ?? e.Method;
                    e.ResourceType = ev.Params["type"]?.GetValue<string>() ?? e.ResourceType;
                    if (_epochMs == 0) _epochMs = ev.Params["timestamp"]?.GetValue<double>() * 1000 ?? 0;
                    e.StartedMs = (ev.Params["timestamp"]?.GetValue<double>() * 1000 ?? 0) - _epochMs;
                    Track(e);
                    log.WriteNetwork(new { kind = "request", e.RequestId, e.Url, e.Method, e.ResourceType });
                    break;
                }
                case "Network.responseReceived":
                {
                    var id = ev.Params["requestId"]?.GetValue<string>() ?? return_empty();
                    var resp = ev.Params["response"] as JsonObject;
                    if (_byId.TryGetValue(id, out var e))
                    {
                        e.Status = (int)(resp?["status"]?.GetValue<double>() ?? 0);
                        e.MimeType = resp?["mimeType"]?.GetValue<string>();
                        e.ResourceType = ev.Params["type"]?.GetValue<string>() ?? e.ResourceType;
                    }
                    break;
                }
                case "Network.loadingFinished":
                {
                    var id = ev.Params["requestId"]?.GetValue<string>() ?? return_empty();
                    if (_byId.TryGetValue(id, out var e))
                    {
                        e.FinishedMs = (ev.Params["timestamp"]?.GetValue<double>() * 1000 ?? 0) - _epochMs;
                        e.EncodedLength = (long)(ev.Params["encodedDataLength"]?.GetValue<double>() ?? 0);
                        e.BodyAvailable = true;
                        Announce(e);
                    }
                    break;
                }
                case "Network.loadingFailed":
                {
                    var id = ev.Params["requestId"]?.GetValue<string>() ?? return_empty();
                    if (_byId.TryGetValue(id, out var e))
                    {
                        e.Failed = true;
                        e.ErrorText = ev.Params["errorText"]?.GetValue<string>();
                        e.FinishedMs = (ev.Params["timestamp"]?.GetValue<double>() * 1000 ?? 0) - _epochMs;
                        Announce(e);
                    }
                    break;
                }
                case "Network.webSocketCreated":
                {
                    var url = ev.Params["url"]?.GetValue<string>() ?? "";
                    log.Write("network", "websocket", "WebSocket " + Redactor.Text(url), status: "info");
                    break;
                }
            }
        }
        catch
        {
            // A single event must not break the monitor.
        }

        static string return_empty() => "";
    }

    /// <summary>Whether an entry is "relevant" for a human in the panel (data, not static resources).</summary>
    public static bool IsInteresting(NetworkEntry e) =>
        e.ResourceType is "XHR" or "Fetch" or "Document" or "EventSource" || (e.Method is not ("GET" or "" or "OPTIONS"));

    /// <summary>Short panel entry for relevant calls (the full traffic goes to a separate _network file).</summary>
    private void Announce(NetworkEntry e)
    {
        log.WriteNetwork(new { kind = "done", e.RequestId, e.Status, e.Failed, e.ErrorText, e.MimeType, e.EncodedLength, e.FinishedMs });
        if (!AnnounceToPanel || !IsInteresting(e)) return;
        var ms = e.FinishedMs is { } f ? $" {Math.Max(0, f - e.StartedMs):F0}ms" : "";
        var head = e.Failed ? "FAIL" : e.Status.ToString();
        log.Write("network", "response", $"{head} {e.Method} {Redactor.Text(Shorten(e.Url))}{ms}",
            new { e.RequestId, type = e.ResourceType, e.MimeType },
            status: e.Failed || e.Status >= 400 ? "error" : "info");
    }

    private static string Shorten(string url) => url.Length <= 160 ? url : url[..157] + "...";

    /// <summary>Whether to send a digest of relevant calls to the panel/event log.</summary>
    public bool AnnounceToPanel { get; set; } = true;

    public IReadOnlyList<NetworkEntry> Snapshot(string? filter = null, int limit = 40)
    {
        lock (_lock)
        {
            IEnumerable<NetworkEntry> q = _recent;
            // By default static noise is skipped; the model can ask for everything with the filter "*".
            if (filter == "*") { }
            else if (!string.IsNullOrWhiteSpace(filter))
                q = q.Where(e => e.Url.Contains(filter, StringComparison.OrdinalIgnoreCase)
                              || e.ResourceType.Contains(filter, StringComparison.OrdinalIgnoreCase));
            else
                q = q.Where(e => e.ResourceType is "XHR" or "Fetch" or "WebSocket" or "EventSource" or "Document");

            return q.Reverse().Take(limit).ToList();
        }
    }

    public async Task<string?> GetBodyAsync(string requestId, CancellationToken ct = default)
    {
        try
        {
            var res = await session.SendAsync("Network.getResponseBody", new JsonObject { ["requestId"] = requestId }, ct);
            if (res is JsonObject obj)
            {
                var body = obj["body"]?.GetValue<string>();
                var b64 = obj["base64Encoded"]?.GetValue<bool>() ?? false;
                if (body is null) return null;
                if (b64)
                {
                    try { return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(body)); }
                    catch { return "[binary body]"; }
                }
                return body;
            }
        }
        catch (CdpException) { }
        return null;
    }

    public NetworkEntry? Find(string requestId) => _byId.TryGetValue(requestId, out var e) ? e : null;
}
