// OpenFill - Metadata: wersja 0.1, data 2026-10-12 21:26
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using OpenFill.Core.Cdp;

namespace OpenFill.Core.Browser;

/// <summary>
/// Pages whose form sits in a frame of another origin (the sign-in widgets of Apple, Microsoft, Google, payment forms).
/// The page script cannot read such a frame from outside, so each of these frames is entered through CDP: an isolated world
/// in the frame (a frame in the same process: through the tab's own session; a frame in its own process: through the session
/// that Target.setAutoAttach delivers) runs the same extractor and actions. Elements of a frame get ids like "f1:of7".
/// </summary>
public sealed class FrameLayer
{
    public sealed class Frame
    {
        public string Key = "", FrameId = "", Url = "", Host = "";
        public string? SessionId;        // session that talks to the frame (null: the tab's own session)
        public string? OwnerSessionId;   // session that knows the <iframe> element of this frame (null: the tab's own session)
        public string? ParentFrameId;
        public long ContextId;
    }

    private const int MaxFrames = 8;
    private readonly CdpSession _session;
    private readonly ConcurrentDictionary<string, string> _targets = new();   // targetId -> sessionId (frames in their own process)
    private readonly ConcurrentDictionary<string, string> _keys = new();      // frameId -> "f1"
    private readonly ConcurrentDictionary<string, Frame> _frames = new();     // key -> frame (the last discovery)
    private int _seq;

    public FrameLayer(CdpSession session) { _session = session; }

    private static readonly Regex IdRx = new(@"^(f\d+):(.+)$", RegexOptions.Compiled);

    /// <summary>"f1:of7" -> ("f1", "of7").</summary>
    public static bool TrySplit(string? id, out string key, out string inner)
    {
        key = inner = "";
        if (string.IsNullOrEmpty(id)) return false;
        var m = IdRx.Match(id);
        if (!m.Success) return false;
        key = m.Groups[1].Value; inner = m.Groups[2].Value;
        return true;
    }

    private Task<JsonNode?> SendTo(string? sessionId, string method, JsonObject? p, CancellationToken ct)
        => _session.Connection.SendAsync(method, p, sessionId ?? _session.SessionId, ct);

    // ------------------------------------------------------------ frames in their own process

    /// <summary>Call when the tab changes (and at the start): forgets the old sessions and asks the browser to report frames in their own process.</summary>
    public async Task EnableAsync(CancellationToken ct)
    {
        _targets.Clear(); _frames.Clear();
        try { await _session.SendAsync("Target.setAutoAttach", new JsonObject { ["autoAttach"] = true, ["waitForDebuggerOnStart"] = false, ["flatten"] = true }, ct); }
        catch (OperationCanceledException) { throw; }
        catch { /* a browser without it: only frames of the same process are available */ }
    }

    public void OnEvent(CdpEvent ev)
    {
        if (ev.SessionId is not null && _session.SessionId is not null && ev.SessionId != _session.SessionId) return;
        if (ev.Method == "Target.attachedToTarget")
        {
            var info = ev.Params["targetInfo"] as JsonObject;
            if (info?["type"]?.GetValue<string>() != "iframe") return;
            var sid = ev.Params["sessionId"]?.GetValue<string>();
            var tid = info["targetId"]?.GetValue<string>();
            if (sid is not null && tid is not null) _targets[tid] = sid;
        }
        else if (ev.Method == "Target.detachedFromTarget")
        {
            var sid = ev.Params["sessionId"]?.GetValue<string>();
            foreach (var kv in _targets.ToArray()) if (kv.Value == sid) _targets.TryRemove(kv.Key, out _);
        }
    }

    // ------------------------------------------------------------ discovery

    private static string? OriginOf(JsonNode? node)
    {
        var fr = node?["frame"];
        var o = fr?["securityOrigin"]?.GetValue<string>();
        if (!string.IsNullOrEmpty(o) && o != "://") return o;
        if (Uri.TryCreate(fr?["url"]?.GetValue<string>(), UriKind.Absolute, out var u) && (u.Scheme == "http" || u.Scheme == "https"))
            return u.GetLeftPart(UriPartial.Authority);
        return null;
    }

    /// <summary>Frames of another origin than their parent (the same-origin ones the page script reads by itself).</summary>
    public async Task<List<Frame>> DiscoverAsync(CancellationToken ct)
    {
        var found = new List<Frame>();
        try
        {
            var tree = await SendTo(null, "Page.getFrameTree", null, ct);
            if (tree?["frameTree"] is JsonObject root)
                await WalkAsync(root, null, OriginOf(root), found, ct);
            foreach (var (_, sid) in _targets.ToArray())
            {
                if (found.Count >= MaxFrames) break;
                JsonNode? t;
                try { t = await SendTo(sid, "Page.getFrameTree", null, ct); }
                catch (OperationCanceledException) { throw; }
                catch { continue; }
                if (t?["frameTree"] is not JsonObject r || r["frame"] is not JsonObject rf) continue;
                await AddAsync(found, rf, sid, null, rf["parentId"]?.GetValue<string>(), ct);
                await WalkAsync(r, sid, OriginOf(r), found, ct);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { }
        _frames.Clear();
        foreach (var f in found) _frames[f.Key] = f;
        return found;
    }

    private async Task WalkAsync(JsonObject node, string? sessionId, string? parentOrigin, List<Frame> found, CancellationToken ct)
    {
        var parentId = node["frame"]?["id"]?.GetValue<string>();
        foreach (var child in (node["childFrames"] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
        {
            if (found.Count >= MaxFrames) return;
            var origin = OriginOf(child);
            if (child["frame"] is JsonObject fr && (origin is null || origin != parentOrigin))
                await AddAsync(found, fr, sessionId, sessionId, parentId, ct);
            await WalkAsync(child, sessionId, origin, found, ct);
        }
    }

    private async Task AddAsync(List<Frame> found, JsonObject fr, string? sessionId, string? ownerSessionId, string? parentId, CancellationToken ct)
    {
        var frameId = fr["id"]?.GetValue<string>();
        var url = fr["url"]?.GetValue<string>() ?? "";
        if (frameId is null || found.Any(f => f.FrameId == frameId)) return;
        if (!(url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))) return;
        try
        {
            var w = await SendTo(sessionId, "Page.createIsolatedWorld", new JsonObject { ["frameId"] = frameId, ["worldName"] = "openfill" }, ct);
            var ctx = w?["executionContextId"]?.GetValue<long>();
            if (ctx is null) return;
            Uri.TryCreate(url, UriKind.Absolute, out var u);
            found.Add(new Frame
            {
                Key = _keys.GetOrAdd(frameId, _ => "f" + Interlocked.Increment(ref _seq)),
                FrameId = frameId, Url = url, Host = u?.Host ?? url,
                SessionId = sessionId, OwnerSessionId = ownerSessionId, ParentFrameId = parentId, ContextId = ctx.Value
            });
        }
        catch (OperationCanceledException) { throw; }
        catch { /* a frame of another process that this session does not own: it comes through its own session */ }
    }

    // ------------------------------------------------------------ running code in a frame

    public async Task<JsonNode?> EvalAsync(Frame f, string expression, bool awaitPromise, CancellationToken ct)
    {
        var res = await SendTo(f.SessionId, "Runtime.evaluate", new JsonObject
        {
            ["expression"] = expression, ["contextId"] = f.ContextId, ["returnByValue"] = true,
            ["awaitPromise"] = awaitPromise, ["userGesture"] = true, ["timeout"] = 30000
        }, ct);
        if (res?["exceptionDetails"] is JsonObject ex)
            throw new CdpException(ex["exception"]?["description"]?.GetValue<string>() ?? ex["text"]?.GetValue<string>() ?? "JS exception");
        return res?["result"]?["value"];
    }

    private async Task<Frame?> GetAsync(string key, bool refresh, CancellationToken ct)
    {
        if (!refresh && _frames.TryGetValue(key, out var f)) return f;
        await DiscoverAsync(ct);
        return _frames.TryGetValue(key, out var g) ? g : null;
    }

    /// <summary>Runs a function of the page scripts (window.__openfill.xxx) in the frame. A frame that changed is found again once.</summary>
    public async Task<JsonNode?> CallAsync(string key, string scripts, string function, JsonObject arg, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            var f = await GetAsync(key, attempt > 0, ct);
            if (f is null) return new JsonObject { ["ok"] = false, ["error"] = "FRAME_GONE", ["frame"] = key };
            try
            {
                var has = await EvalAsync(f, "!!(window.__openfill && window.__openfill.extract && window.__openfill.act)", false, ct);
                if (has?.GetValue<bool>() != true) await EvalAsync(f, scripts, false, ct);
                var expr = $"(function(){{ const arg = {arg.ToJsonString()}; return (async () => {{ return await ({function})(arg); }})(); }})()";
                return await EvalAsync(f, expr, true, ct);
            }
            catch (CdpException) when (attempt == 0) { /* the frame was reloaded: discover again */ }
        }
    }

    /// <summary>Reads the extraction of every frame and adds it to the page model of the tab.</summary>
    public async Task MergeAllAsync(JsonObject main, string scripts, JsonObject arg, CancellationToken ct)
    {
        var frames = await DiscoverAsync(ct);
        foreach (var f in frames)
        {
            try
            {
                var part = await CallAsync(f.Key, scripts, "window.__openfill.extract", arg, ct) as JsonObject;
                if (part is not null && part["ok"]?.GetValue<bool>() != false) Merge(main, part, f);
            }
            catch (OperationCanceledException) { throw; }
            catch { /* one frame that cannot be read must not spoil the page */ }
        }
    }

    private static void PrefixIds(JsonNode? n, string prefix)
    {
        if (n is JsonObject o)
        {
            foreach (var name in new[] { "id", "form" })
                if (o[name] is JsonValue v && v.TryGetValue<string>(out var s) && s.Length > 0) o[name] = prefix + s;
            foreach (var kv in o.ToArray()) if (kv.Value is JsonObject || kv.Value is JsonArray) PrefixIds(kv.Value, prefix);
        }
        else if (n is JsonArray a) foreach (var x in a) PrefixIds(x, prefix);
    }

    private static void Merge(JsonObject main, JsonObject part, Frame f)
    {
        PrefixIds(part, f.Key + ":");
        var added = 0;
        foreach (var name in new[] { "forms", "looseFields", "buttons", "links", "dialogs", "messages" })
        {
            if (part[name] is not JsonArray src || src.Count == 0) continue;
            if (main[name] is not JsonArray dst) { dst = new JsonArray(); main[name] = dst; }
            foreach (var item in src.ToList())
            {
                src.Remove(item);
                if (name == "forms" && item is JsonObject fo)
                    fo["name"] = $"(frame {f.Key}: {f.Host}) " + (fo["name"]?.GetValue<string>() ?? "");
                dst.Add(item);
                added++;
            }
        }
        if (added == 0) return;
        if (main["headings"] is not JsonArray hs) { hs = new JsonArray(); main["headings"] = hs; }
        hs.Add($"Frame {f.Key} = {f.Url} (its elements have ids {f.Key}:...)");
    }

    // ------------------------------------------------------------ position of a frame (for real mouse clicks)

    /// <summary>Top-left corner of the frame in the viewport of the tab (the frame is scrolled into view first).</summary>
    public async Task<(double x, double y)?> OffsetAsync(string key, CancellationToken ct)
    {
        if (!_frames.TryGetValue(key, out var f)) { await DiscoverAsync(ct); if (!_frames.TryGetValue(key, out f)) return null; }
        double x = 0, y = 0;
        var cur = f;
        for (var depth = 0; cur is not null && depth < 6; depth++)
        {
            var owner = await SendTo(cur.OwnerSessionId, "DOM.getFrameOwner", new JsonObject { ["frameId"] = cur.FrameId }, ct);
            var backendId = owner?["backendNodeId"]?.GetValue<int>();
            if (backendId is null) return null;
            try { await SendTo(cur.OwnerSessionId, "DOM.scrollIntoViewIfNeeded", new JsonObject { ["backendNodeId"] = backendId.Value }, ct); } catch (OperationCanceledException) { throw; } catch { }
            var q = await SendTo(cur.OwnerSessionId, "DOM.getContentQuads", new JsonObject { ["backendNodeId"] = backendId.Value }, ct);
            if (q?["quads"] is not JsonArray quads || quads.Count == 0 || quads[0] is not JsonArray quad || quad.Count < 2) return null;
            x += quad[0]!.GetValue<double>(); y += quad[1]!.GetValue<double>();
            var pid = cur.ParentFrameId;
            cur = pid is null ? null : _frames.Values.FirstOrDefault(v => v.FrameId == pid);
        }
        return (x, y);
    }

    /// <summary>The element as a remote object in the frame (for setting files on an input).</summary>
    public async Task<(string? objectId, string? sessionId)> ResolveObjectAsync(string key, string innerId, CancellationToken ct)
    {
        var f = await GetAsync(key, false, ct) ?? await GetAsync(key, true, ct);
        if (f is null) return (null, null);
        var res = await SendTo(f.SessionId, "Runtime.evaluate", new JsonObject
        {
            ["expression"] = $"window.__openfill.resolve({JsonValue.Create(innerId)!.ToJsonString()})",
            ["contextId"] = f.ContextId, ["returnByValue"] = false
        }, ct);
        return (res?["result"]?["objectId"]?.GetValue<string>(), f.SessionId);
    }
}
