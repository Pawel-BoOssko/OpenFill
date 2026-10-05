// OpenFill - Metadata: wersja 0.4, data 2026-10-05 17:09
using System.Text.Json.Nodes;
using OpenFill.Core.Assets;
using OpenFill.Core.Cdp;
using OpenFill.Core.Logging;

namespace OpenFill.Core.Browser;

/// <summary>
/// Controls the page through CDP: navigation, structure extraction, actions, waiting for the page to settle.
/// Knows no specific page - the same code works on Xing and anywhere else.
/// </summary>
public sealed class BrowserController
{
    private readonly CdpSession _session;
    private readonly EventLog _log;
    private readonly string _extractorJs;
    private readonly string _actionsJs;

    public NetworkMonitor Network { get; }
    public ConsoleMonitor Console { get; }

    public BrowserController(CdpSession session, EventLog log)
    {
        _session = session;
        _log = log;
        Network = new NetworkMonitor(session, log);
        Console = new ConsoleMonitor(session);
        _extractorJs = AssetLoader.Load("extractor.js");
        _actionsJs = AssetLoader.Load("actions.js");
    }

    private readonly System.Collections.Concurrent.ConcurrentQueue<string> _dialogs = new();

    /// <summary>
    /// JS dialogs (alert/confirm/prompt/beforeunload) block the page - without handling, every CDP call hangs.
    /// We accept them automatically (usually the result of an action the model already decided on),
    /// record the text and show it to the model on the next get_page.
    /// </summary>
    private void OnCdpEvent(CdpEvent ev)
    {
        if (ev.Method != "Page.javascriptDialogOpening") return;
        if (ev.SessionId is not null && _session.SessionId is not null && ev.SessionId != _session.SessionId) return;
        var type = ev.Params["type"]?.GetValue<string>() ?? "alert";
        var message = ev.Params["message"]?.GetValue<string>() ?? "";
        var prompt = ev.Params["defaultPrompt"]?.GetValue<string>() ?? "";
        _dialogs.Enqueue($"{type}: \"{message}\" — zaakceptowano automatycznie");
        _log.Write("browser", "dialog", $"Page dialog ({type}): {message} — accepting", status: "warn");
        _ = Task.Run(async () =>
        {
            try { await _session.SendAsync("Page.handleJavaScriptDialog", new JsonObject { ["accept"] = true, ["promptText"] = prompt }); }
            catch { /* the dialog may already be gone */ }
        });
    }

    /// <summary>JS dialogs handled since the last read (and clears the list).</summary>
    public List<string> TakeDialogs()
    {
        var list = new List<string>();
        while (_dialogs.TryDequeue(out var d)) list.Add(d);
        return list;
    }

    private bool _subscribed;
    private readonly HashSet<string> _scriptTargets = new();

    /// <summary>
    /// Prepares the browser tab the connection points to now. Safe to call again after switching tabs: events are subscribed to once,
    /// the per-tab enables are repeated, and the scripts are injected once per tab.
    /// </summary>
    public async Task InitAsync(CancellationToken ct = default)
    {
        if (!_subscribed) { _subscribed = true; _session.Connection.Event += OnCdpEvent; }
        await _session.SendAsync("Page.enable", null, ct);
        await _session.SendAsync("DOM.enable", null, ct);
        await Network.EnableAsync(ct);
        await Console.EnableAsync(ct);
        var key = (_session.Connection as ITabAwareConnection)?.ActiveTargetKey ?? "main";
        bool inject;
        lock (_scriptTargets) inject = _scriptTargets.Add(key);
        if (!inject) return;
        // We inject the scripts into every new document, so the identifiers and functions are always available.
        var combined = _extractorJs + "\n;\n" + _actionsJs;
        await _session.SendAsync("Page.addScriptToEvaluateOnNewDocument", new JsonObject { ["source"] = combined }, ct);
    }

    private async Task EnsureScriptsAsync(CancellationToken ct)
    {
        var has = await _session.EvaluateAsync("!!(window.__openfill && window.__openfill.extract && window.__openfill.act)", ct: ct);
        if (has?.GetValue<bool>() != true)
            await _session.EvaluateAsync(_extractorJs + "\n;\n" + _actionsJs, awaitPromise: false, ct: ct);
    }

    public async Task<string> NavigateAsync(string url, CancellationToken ct = default)
    {
        await _session.SendAsync("Page.navigate", new JsonObject { ["url"] = url }, ct);
        await WaitStableAsync(ct: ct);
        _log.Write("browser", "navigate", $"Opened {url}", new { url });
        return url;
    }

    /// <summary>Waits until network traffic is quiet for quietMs (or the time limit passes).</summary>
    public async Task WaitStableAsync(int quietMs = 600, int timeoutMs = 12000, CancellationToken ct = default)
    {
        var start = DateTime.UtcNow;
        long last = -1;
        var quietSince = DateTime.UtcNow;
        while ((DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
        {
            var now = Network.Activity;
            if (now != last) { last = now; quietSince = DateTime.UtcNow; }
            else if ((DateTime.UtcNow - quietSince).TotalMilliseconds >= quietMs) break;
            await Task.Delay(100, ct);
        }
        try
        {
            await _session.EvaluateAsync("document.readyState", awaitPromise: false, ct: ct);
        }
        catch { }
    }

    public async Task<PageModel> ExtractAsync(bool includeHidden = false, int maxFields = 150, CancellationToken ct = default)
    {
        await EnsureScriptsAsync(ct);
        var arg = new JsonObject { ["includeHidden"] = includeHidden, ["maxFields"] = maxFields };
        var node = await _session.CallAsync("window.__openfill.extract", arg, ct: ct);
        return PageModel.From(node);
    }

    public async Task<JsonNode?> ActAsync(string id, string action, string? value, CancellationToken ct = default)
    {
        await EnsureScriptsAsync(ct);
        var arg = new JsonObject { ["id"] = id, ["action"] = action };
        if (value is not null) arg["value"] = value;
        var res = await _session.CallAsync("window.__openfill.act", arg, ct: ct);
        _log.Write("browser", "act", $"{action} {id}", res, status: res?["ok"]?.GetValue<bool>() == true ? "ok" : "error");
        await Task.Delay(150, ct); // a moment for the page to react (autocomplete, validation)
        return res;
    }

    public async Task<JsonNode?> ActManyAsync(JsonArray steps, CancellationToken ct = default)
    {
        await EnsureScriptsAsync(ct);
        var arg = new JsonObject { ["steps"] = steps.DeepClone() };
        var res = await _session.CallAsync("window.__openfill.actMany", arg, ct: ct);
        _log.Write("browser", "actMany", $"{steps.Count} steps", res, status: res?["ok"]?.GetValue<bool>() == true ? "ok" : "error");
        await WaitStableAsync(ct: ct);
        return res;
    }

    // ------------------------------------------------------------ real input events (isTrusted)
    // Some pages ignore events generated from JS. CDP Input.* gives events indistinguishable from a human.

    private async Task<(double x, double y)?> CenterOfAsync(string id, CancellationToken ct)
    {
        await EnsureScriptsAsync(ct);
        var r = await _session.CallAsync("window.__openfill.rect", new JsonObject { ["id"] = id }, ct: ct);
        if (r?["ok"]?.GetValue<bool>() != true) return null;
        await Task.Delay(60, ct); // scroll
        r = await _session.CallAsync("window.__openfill.rect", new JsonObject { ["id"] = id }, ct: ct);
        return (r!["x"]!.GetValue<double>(), r["y"]!.GetValue<double>());
    }

    public async Task<JsonNode> RealClickAsync(string id, CancellationToken ct = default)
    {
        var c = await CenterOfAsync(id, ct);
        if (c is null) return new JsonObject { ["ok"] = false, ["error"] = "NOT_FOUND_OR_INVISIBLE", ["id"] = id };
        var (x, y) = c.Value;
        if (Environment.GetEnvironmentVariable("OPENFILL_DEBUG_CDP") == "1")
            System.Console.Error.WriteLine($"[realClick] {id} at {x},{y}: " + (await _session.EvaluateAsync($"(document.elementFromPoint({x.ToString(System.Globalization.CultureInfo.InvariantCulture)},{y.ToString(System.Globalization.CultureInfo.InvariantCulture)})||{{}}).outerHTML?.slice(0,100)+' vw='+innerWidth+'x'+innerHeight", awaitPromise: false, ct: ct)));
        await _session.SendAsync("Input.dispatchMouseEvent", new JsonObject { ["type"] = "mouseMoved", ["x"] = x, ["y"] = y }, ct);
        await _session.SendAsync("Input.dispatchMouseEvent", new JsonObject { ["type"] = "mousePressed", ["x"] = x, ["y"] = y, ["button"] = "left", ["clickCount"] = 1 }, ct);
        await _session.SendAsync("Input.dispatchMouseEvent", new JsonObject { ["type"] = "mouseReleased", ["x"] = x, ["y"] = y, ["button"] = "left", ["clickCount"] = 1 }, ct);
        _log.Write("browser", "realClick", $"real click {id}", new { id, x = Math.Round(x), y = Math.Round(y) });
        await Task.Delay(150, ct);
        return new JsonObject { ["ok"] = true, ["id"] = id };
    }

    public async Task<JsonNode> RealTypeAsync(string id, string text, bool clearFirst = true, CancellationToken ct = default)
    {
        var click = await RealClickAsync(id, ct);
        if (click["ok"]?.GetValue<bool>() != true) return click;
        if (clearFirst)
        {
            // Select all and delete (works in text fields and editors).
            var mod = OperatingSystem.IsMacOS() ? 4 : 2; // Meta or Ctrl
            await _session.SendAsync("Input.dispatchKeyEvent", new JsonObject { ["type"] = "keyDown", ["modifiers"] = mod, ["key"] = "a", ["code"] = "KeyA", ["windowsVirtualKeyCode"] = 65, ["commands"] = new JsonArray("selectAll") }, ct);
            await _session.SendAsync("Input.dispatchKeyEvent", new JsonObject { ["type"] = "keyUp", ["modifiers"] = mod, ["key"] = "a", ["code"] = "KeyA", ["windowsVirtualKeyCode"] = 65 }, ct);
            await PressKeyAsync("Backspace", ct);
        }
        await _session.SendAsync("Input.insertText", new JsonObject { ["text"] = text }, ct);
        _log.Write("browser", "realType", $"wpisano (prawdziwe klawisze) {id}", new { id, length = text.Length });
        await Task.Delay(150, ct);
        var val = await _session.EvaluateAsync($"(function(){{const e=window.__openfill.resolve({JsonValue.Create(id)!.ToJsonString()});return e?(e.value!==undefined?e.value:e.textContent):null;}})()", awaitPromise: false, ct: ct);
        return new JsonObject { ["ok"] = true, ["id"] = id, ["value"] = val?.DeepClone() };
    }

    private static readonly Dictionary<string, (string code, int vk, string? text)> Keys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Enter"] = ("Enter", 13, "\r"), ["Tab"] = ("Tab", 9, null), ["Escape"] = ("Escape", 27, null),
        ["Backspace"] = ("Backspace", 8, null), ["Delete"] = ("Delete", 46, null), ["Space"] = ("Space", 32, " "),
        ["ArrowDown"] = ("ArrowDown", 40, null), ["ArrowUp"] = ("ArrowUp", 38, null),
        ["ArrowLeft"] = ("ArrowLeft", 37, null), ["ArrowRight"] = ("ArrowRight", 39, null),
        ["Home"] = ("Home", 36, null), ["End"] = ("End", 35, null), ["PageDown"] = ("PageDown", 34, null), ["PageUp"] = ("PageUp", 33, null)
    };

    /// <summary>A real key press in the focused element (Enter, Tab, Escape, arrows).</summary>
    public async Task<JsonNode> PressKeyAsync(string key, CancellationToken ct = default)
    {
        if (!Keys.TryGetValue(key, out var k)) return new JsonObject { ["ok"] = false, ["error"] = "UNKNOWN_KEY", ["key"] = key, ["known"] = string.Join(",", Keys.Keys) };
        var down = new JsonObject { ["type"] = k.text is null ? "rawKeyDown" : "keyDown", ["key"] = key == "Space" ? " " : key, ["code"] = k.code, ["windowsVirtualKeyCode"] = k.vk };
        if (k.text is not null) down["text"] = k.text;
        await _session.SendAsync("Input.dispatchKeyEvent", down, ct);
        await _session.SendAsync("Input.dispatchKeyEvent", new JsonObject { ["type"] = "keyUp", ["key"] = key == "Space" ? " " : key, ["code"] = k.code, ["windowsVirtualKeyCode"] = k.vk }, ct);
        await Task.Delay(80, ct);
        return new JsonObject { ["ok"] = true, ["key"] = key };
    }

    /// <summary>Points an &lt;input type=file&gt; field at file(s) from disk (CV, profile photo).</summary>
    public async Task<JsonNode> UploadFileAsync(string id, IReadOnlyList<string> files, CancellationToken ct = default)
    {
        foreach (var f in files)
            if (!File.Exists(f)) return new JsonObject { ["ok"] = false, ["error"] = "FILE_NOT_FOUND", ["file"] = f };
        await EnsureScriptsAsync(ct);
        var res = await _session.SendAsync("Runtime.evaluate", new JsonObject
        {
            ["expression"] = $"window.__openfill.resolve({JsonValue.Create(id)!.ToJsonString()})",
            ["returnByValue"] = false
        }, ct);
        var objectId = res?["result"]?["objectId"]?.GetValue<string>();
        if (objectId is null) return new JsonObject { ["ok"] = false, ["error"] = "NOT_FOUND", ["id"] = id };
        await _session.SendAsync("DOM.setFileInputFiles", new JsonObject
        {
            ["objectId"] = objectId,
            ["files"] = new JsonArray(files.Select(f => (JsonNode)Path.GetFullPath(f)).ToArray())
        }, ct);
        _log.Write("browser", "upload", $"file for {id}: {string.Join(", ", files.Select(Path.GetFileName))}");
        return new JsonObject { ["ok"] = true, ["id"] = id, ["files"] = files.Count };
    }

    /// <summary>Screenshot of the visible part of the page (JPEG, base64) - for the model when the structure is not enough.</summary>
    public async Task<string?> ScreenshotJpegBase64Async(int quality = 60, CancellationToken ct = default)
    {
        var res = await _session.SendAsync("Page.captureScreenshot", new JsonObject { ["format"] = "jpeg", ["quality"] = quality }, ct);
        return res?["data"]?.GetValue<string>();
    }

    public async Task<JsonNode?> RunJsAsync(string code, CancellationToken ct = default)
    {
        var wrapped = $"(async()=>{{ try {{ return {{ ok:true, value: await ({code}) }}; }} catch(e) {{ return {{ ok:false, error: String(e&&e.message||e) }}; }} }})()";
        return await _session.EvaluateAsync(wrapped, awaitPromise: true, ct: ct);
    }

    public async Task<string> CurrentUrlAsync(CancellationToken ct = default)
        => (await _session.EvaluateAsync("location.href", awaitPromise: false, ct: ct))?.GetValue<string>() ?? "";
}
