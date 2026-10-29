// OpenFill - Metadata: wersja 0.10, data 2026-10-29 23:14
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
    /// <summary>Frames of other origins (sign-in widgets, payment forms): read and operated through CDP.</summary>
    public FrameLayer Frames { get; }
    private string Scripts => _extractorJs + "\n;\n" + _actionsJs;
    public ConsoleMonitor Console { get; }

    public BrowserController(CdpSession session, EventLog log)
    {
        _session = session;
        _log = log;
        Network = new NetworkMonitor(session, log);
        Frames = new FrameLayer(session);
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
        if (ev.Method.StartsWith("Target.", StringComparison.Ordinal)) { Frames.OnEvent(ev); return; }
        if (ev.Method == "Page.fileChooserOpened")
        {
            _dialogs.Enqueue("file chooser: the page tried to open a file dialog; it was blocked (no window opened). To give the page a file, use upload_file with the id of its file field from get_page and the full path of the file.");
            _log.Write("browser", "dialog", "The page tried to open a file dialog - blocked", status: "warn");
            return;
        }
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
    private string? _initKey;
    private readonly SemaphoreSlim _syncGate = new(1, 1);
    private string? TargetKey => (_session.Connection as ITabAwareConnection)?.ActiveTargetKey;

    /// <summary>
    /// The page the connection talks to can change under the task (a pop-up window opens or closes). Before each operation the controller
    /// notices it, sets the new page up (InitAsync) and leaves a note for the model.
    /// </summary>
    private async Task SyncTargetAsync(CancellationToken ct)
    {
        if (_session.Connection is not ITabAwareConnection) return;
        var now = TargetKey;
        if (now is null || _initKey is null || now == _initKey) return;
        await _syncGate.WaitAsync(ct);
        try
        {
            now = TargetKey;
            if (now is null || now == _initKey) return;
            var wasPopup = _initKey.Contains(":popup:", StringComparison.Ordinal);
            var isPopup = now.Contains(":popup:", StringComparison.Ordinal);
            await InitAsync(ct);
            if (isPopup) _dialogs.Enqueue("window: a pop-up window opened (for example a sign-in window). get_page, act and the other tools now work in the pop-up; the page that opened it waits for the result. When the pop-up closes you are back on the main page. To go back to the main page yourself use switch_window.");
            else if (wasPopup) _dialogs.Enqueue("window: you are back on the main page (the pop-up window closed or you switched).");
            _log.Write("browser", "window", isPopup ? "Working in the pop-up window" : "Working in the main page again");
        }
        finally { _syncGate.Release(); }
    }

    /// <summary>Switches between the page and its pop-up window ("popup" or "main").</summary>
    public string SwitchWindow(string target)
    {
        if (_session.Connection is not IWindowSwitch sw) return "There are no separate windows here.";
        var popup = string.Equals(target, "popup", StringComparison.OrdinalIgnoreCase);
        if (!sw.Select(popup)) return "There is no pop-up window open.";
        return popup ? "Now working in the pop-up window." : "Now working in the main page.";
    }

    /// <summary>
    /// Scripts are injected once per (connection, tab): a tab that serves task after task (each with its own controller)
    /// must not collect a new copy of the scripts every time.
    /// </summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ICdpConnection, HashSet<string>> Injected = new();

    /// <summary>Stops listening to the tab (the task is over). The tab and its page are untouched.</summary>
    public void Detach()
    {
        if (_subscribed) { _subscribed = false; _session.Connection.Event -= OnCdpEvent; }
        Network.Detach();
        Console.Detach();
    }

    /// <summary>
    /// Prepares the browser tab the connection points to now. Safe to call again after switching tabs: events are subscribed to once,
    /// the per-tab enables are repeated, and the scripts are injected once per tab.
    /// </summary>
    public async Task InitAsync(CancellationToken ct = default)
    {
        if (!_subscribed) { _subscribed = true; _session.Connection.Event += OnCdpEvent; }
        await _session.SendAsync("Page.enable", null, ct);
        await _session.SendAsync("DOM.enable", null, ct);
        await Frames.EnableAsync(ct);
        // A native file dialog would stay open on the person's screen and block the page; the model gets a note instead.
        try { await _session.SendAsync("Page.setInterceptFileChooserDialog", new JsonObject { ["enabled"] = true }, ct); } catch (OperationCanceledException) { throw; } catch { }
        await Network.EnableAsync(ct);
        await Console.EnableAsync(ct);
        // A tab that is in the foreground for the person must not make the page think it is hidden.
        try { await _session.SendAsync("Emulation.setFocusEmulationEnabled", new JsonObject { ["enabled"] = true }, ct); } catch (OperationCanceledException) { throw; } catch { }
        _initKey = TargetKey ?? "main";
        var key = ((_session.Connection as ITabAwareConnection)?.ActiveTargetKey ?? "main") + "|" + (_session.SessionId ?? "");
        var seen = Injected.GetOrCreateValue(_session.Connection);
        bool inject;
        lock (seen) inject = seen.Add(key);
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
        await SyncTargetAsync(ct);
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
        await SyncTargetAsync(ct);
        await EnsureScriptsAsync(ct);
        var arg = new JsonObject { ["includeHidden"] = includeHidden, ["maxFields"] = maxFields };
        var node = await _session.CallAsync("window.__openfill.extract", arg, ct: ct);
        if (node is JsonObject mainObj) await Frames.MergeAllAsync(mainObj, Scripts, arg, ct);
        return PageModel.From(node);
    }

    public async Task<JsonNode?> ActAsync(string id, string action, string? value, CancellationToken ct = default)
    {
        await SyncTargetAsync(ct);
        if (FrameLayer.TrySplit(id, out var frameKey, out var innerId))
        {
            var farg = new JsonObject { ["id"] = innerId, ["action"] = action };
            if (value is not null) farg["value"] = value;
            var fres = await Frames.CallAsync(frameKey, Scripts, "window.__openfill.act", farg, ct);
            if (fres is JsonObject fo && fo["id"] is not null) fo["id"] = id;
            _log.Write("browser", "act", $"{action} {id}", fres, status: fres?["ok"]?.GetValue<bool>() == true ? "ok" : "error");
            await Task.Delay(150, ct);
            return fres;
        }
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
        await SyncTargetAsync(ct);
        if (steps.OfType<JsonObject>().Any(s => FrameLayer.TrySplit(s["id"]?.GetValue<string>(), out _, out _)))
        {
            await EnsureScriptsAsync(ct);
            var results = new JsonArray();
            var allOk = true;
            foreach (var s in steps.OfType<JsonObject>())
            {
                var sid = s["id"]?.GetValue<string>();
                var step = (JsonObject)s.DeepClone();
                JsonNode? r;
                if (FrameLayer.TrySplit(sid, out var fk, out var fi))
                {
                    step["id"] = fi;
                    r = await Frames.CallAsync(fk, Scripts, "window.__openfill.act", step, ct);
                    if (r is JsonObject ro && ro["id"] is not null) ro["id"] = sid;
                }
                else r = await _session.CallAsync("window.__openfill.act", step, ct: ct);
                results.Add(r?.DeepClone());
                if (r?["ok"]?.GetValue<bool>() != true) allOk = false;
                await Task.Delay(40, ct);
            }
            var many = new JsonObject { ["ok"] = allOk, ["results"] = results };
            _log.Write("browser", "actMany", $"{steps.Count} steps (with frames)", many, status: allOk ? "ok" : "error");
            await WaitStableAsync(ct: ct);
            return many;
        }
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
        if (FrameLayer.TrySplit(id, out var fkey, out var innerId))
        {
            var ra = new JsonObject { ["id"] = innerId };
            var r1 = await Frames.CallAsync(fkey, Scripts, "window.__openfill.rect", ra, ct);
            if (r1?["ok"]?.GetValue<bool>() != true) return null;
            await Task.Delay(60, ct); // scroll
            var r2 = await Frames.CallAsync(fkey, Scripts, "window.__openfill.rect", new JsonObject { ["id"] = innerId }, ct);
            var off = await Frames.OffsetAsync(fkey, ct);
            if (r2?["x"] is null || off is null) return null;
            return (r2["x"]!.GetValue<double>() + off.Value.x, r2["y"]!.GetValue<double>() + off.Value.y);
        }
        await EnsureScriptsAsync(ct);
        var r = await _session.CallAsync("window.__openfill.rect", new JsonObject { ["id"] = id }, ct: ct);
        if (r?["ok"]?.GetValue<bool>() != true) return null;
        await Task.Delay(60, ct); // scroll
        r = await _session.CallAsync("window.__openfill.rect", new JsonObject { ["id"] = id }, ct: ct);
        return (r!["x"]!.GetValue<double>(), r["y"]!.GetValue<double>());
    }

    public async Task<JsonNode> RealClickAsync(string id, CancellationToken ct = default)
    {
        await SyncTargetAsync(ct);
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
        await SyncTargetAsync(ct);
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
        JsonNode? val;
        if (FrameLayer.TrySplit(id, out var vk, out var vi))
            val = await Frames.CallAsync(vk, Scripts, "function(a){const e=window.__openfill.resolve(a.id);return e?(e.value!==undefined?e.value:e.textContent):null;}", new JsonObject { ["id"] = vi }, ct);
        else val = await _session.EvaluateAsync($"(function(){{const e=window.__openfill.resolve({JsonValue.Create(id)!.ToJsonString()});return e?(e.value!==undefined?e.value:e.textContent):null;}})()", awaitPromise: false, ct: ct);
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
        await SyncTargetAsync(ct);
        if (!Keys.TryGetValue(key, out var k)) return new JsonObject { ["ok"] = false, ["error"] = "UNKNOWN_KEY", ["key"] = key, ["known"] = string.Join(",", Keys.Keys) };
        var down = new JsonObject { ["type"] = k.text is null ? "rawKeyDown" : "keyDown", ["key"] = key == "Space" ? " " : key, ["code"] = k.code, ["windowsVirtualKeyCode"] = k.vk };
        if (k.text is not null) down["text"] = k.text;
        await _session.SendAsync("Input.dispatchKeyEvent", down, ct);
        await _session.SendAsync("Input.dispatchKeyEvent", new JsonObject { ["type"] = "keyUp", ["key"] = key == "Space" ? " " : key, ["code"] = k.code, ["windowsVirtualKeyCode"] = k.vk }, ct);
        await Task.Delay(80, ct);
        return new JsonObject { ["ok"] = true, ["key"] = key };
    }

    private const string FocusPasswordJs = "(() => { const vis = e => { const r = e.getBoundingClientRect(), s = getComputedStyle(e); return r.width > 0 && r.height > 0 && s.visibility !== 'hidden' && s.display !== 'none'; }; const e = [...document.querySelectorAll('input[type=password]')].find(vis); if (!e) return null; e.scrollIntoView({ block: 'center' }); e.focus(); const r = e.getBoundingClientRect(); return { x: r.left + r.width / 2, y: r.top + r.height / 2 }; })()";
    private const string PasswordStateJs = "(() => { const vis = e => { const r = e.getBoundingClientRect(), s = getComputedStyle(e); return r.width > 0 && r.height > 0 && s.visibility !== 'hidden' && s.display !== 'none'; }; const e = [...document.querySelectorAll('input[type=password]')].find(vis); if (!e) return 'none'; let a = false; try { a = e.matches(':-webkit-autofill') || e.matches(':autofill'); } catch (x) { } return ((e.value && e.value.length > 0) || a) ? 'filled' : 'empty'; })()";

    /// <summary>
    /// Fills the password field of a sign-in page from the browser's saved passwords, the way a person does: click in the field,
    /// ArrowDown in the suggestion list, Enter. The list ignores keys sent through CDP, so the window is brought to the front
    /// and real keys are pressed. The password itself never passes through here.
    /// </summary>
    public async Task<JsonNode> UseSavedPasswordAsync(CancellationToken ct = default)
    {
        await SyncTargetAsync(ct);
        var state = (await _session.EvaluateAsync(PasswordStateJs, ct: ct))?.ToString();
        if (state == "none") return new JsonObject { ["ok"] = false, ["error"] = "NO_PASSWORD_FIELD", ["hint"] = "There is no visible password field on this page (it may be in a pop-up window: switch_window)." };
        if (state == "filled") return new JsonObject { ["ok"] = true, ["filled"] = true, ["method"] = "already" };

        var native = _session.Connection as INativeInput;
        var focused = native is not null && await native.FocusWindowAsync(ct);
        await Task.Delay(400, ct);
        string? method = null;
        for (var attempt = 0; attempt < 2 && method is null; attempt++)
        {
            var at = await _session.EvaluateAsync(FocusPasswordJs, ct: ct);
            if (at is not JsonObject p || p["x"] is null) break;
            var x = p["x"]!.GetValue<double>(); var y = p["y"]!.GetValue<double>();
            await _session.SendAsync("Input.dispatchMouseEvent", new JsonObject { ["type"] = "mouseMoved", ["x"] = x, ["y"] = y }, ct);
            await _session.SendAsync("Input.dispatchMouseEvent", new JsonObject { ["type"] = "mousePressed", ["x"] = x, ["y"] = y, ["button"] = "left", ["clickCount"] = 1 }, ct);
            await _session.SendAsync("Input.dispatchMouseEvent", new JsonObject { ["type"] = "mouseReleased", ["x"] = x, ["y"] = y, ["button"] = "left", ["clickCount"] = 1 }, ct);
            await Task.Delay(900, ct); // the suggestion list takes a moment

            if (focused && native is not null && await native.SendKeysAsync(new[] { "ArrowDown", "Enter" }, ct))
            {
                await Task.Delay(500, ct);
                if ((await _session.EvaluateAsync(PasswordStateJs, ct: ct))?.ToString() == "filled") { method = "keyboard"; break; }
            }
            await PressKeyAsync("ArrowDown", ct);
            await PressKeyAsync("Enter", ct);
            await Task.Delay(500, ct);
            if ((await _session.EvaluateAsync(PasswordStateJs, ct: ct))?.ToString() == "filled") method = "cdp";
        }
        var filled = method is not null;
        _log.Write("browser", "password", filled ? "Saved password filled in by the browser (" + method + ")" : "The browser did not fill in a saved password (window in front: " + focused + ")", status: filled ? "ok" : "warn");
        var res = new JsonObject { ["ok"] = filled, ["filled"] = filled, ["windowInFront"] = focused };
        if (method is not null) res["method"] = method;
        if (!filled) res["hint"] = "No saved password was offered for this page. Ask the calling model or the person for the password.";
        return res;
    }

    /// <summary>Fills a visible login field when present and the password field with an authorized, locally imported credential.
    /// No password appears in tool results, page snapshots or OpenFill logs.</summary>
    public async Task<JsonNode> FillImportedCredentialAsync(string login, string password, CancellationToken ct = default)
    {
        await SyncTargetAsync(ct);
        var serializedLogin = JsonValue.Create(login)!.ToJsonString();
        var serializedPassword = JsonValue.Create(password)!.ToJsonString();
        var js = "(function(){"
            + "const visible=x=>x&&x.getClientRects().length>0&&!x.disabled&&!x.readOnly;"
            + "const all=[...document.querySelectorAll('input')].filter(visible);"
            + "const p=all.find(x=>(x.type||'').toLowerCase()==='password');"
            + "if(!p)return {ok:false,error:'NO_VISIBLE_PASSWORD_FIELD',loginFieldFound:false,loginFilled:false,passwordFilled:false};"
            + "const setter=Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set;"
            + "const fire=x=>{x.dispatchEvent(new Event('input',{bubbles:true}));x.dispatchEvent(new Event('change',{bubbles:true}));};"
            + "const source=p.form?[...p.form.querySelectorAll('input')].filter(visible):all;"
            + "const candidates=source.filter(x=>x!==p&&['','text','email','tel','number'].includes((x.type||'').toLowerCase()));"
            + "const score=x=>{const s=((x.name||'')+' '+(x.id||'')+' '+(x.autocomplete||'')+' '+(x.placeholder||'')).toLowerCase();"
            + "let n=0;if((x.autocomplete||'').toLowerCase()==='username')n+=100;if((x.type||'').toLowerCase()==='email')n+=60;"
            + "if(/user|login|email|mail|konto|account/.test(s))n+=50;if(/search|code|otp|phone|tel/.test(s))n-=80;if(!x.value)n+=10;return n;};"
            + "candidates.sort((a,b)=>score(b)-score(a));const u=candidates.length&&score(candidates[0])>=0?candidates[0]:null;"
            + "let loginFilled=false;if(u&&" + serializedLogin + ".length){setter.call(u," + serializedLogin + ");fire(u);loginFilled=u.value===" + serializedLogin + ";}"
            + "setter.call(p," + serializedPassword + ");fire(p);const passwordFilled=p.value.length>0;"
            + "return {ok:passwordFilled,passwordFilled:passwordFilled,loginFieldFound:!!u,loginFilled:loginFilled};"
            + "})()";
        var result = await _session.EvaluateAsync(js, ct: ct);
        var filled = result?["ok"]?.GetValue<bool>() == true;
        var loginFieldFound = result?["loginFieldFound"]?.GetValue<bool>() == true;
        var loginFilled = result?["loginFilled"]?.GetValue<bool>() == true;
        _log.Write("browser", "imported_credential",
            filled
                ? "Imported credential filled; login field " + (loginFieldFound ? (loginFilled ? "filled" : "not filled") : "not present")
                : "No eligible password input",
            status: filled ? "ok" : "warn");
        return result ?? new JsonObject { ["ok"] = false };
    }

    /// <summary>Backward-compatible password-only helper.</summary>
    public Task<JsonNode> FillImportedPasswordAsync(string password, CancellationToken ct = default)
        => FillImportedCredentialAsync("", password, ct);

    /// <summary>Points an &lt;input type=file&gt; field at file(s) from disk (CV, profile photo).</summary>
    public async Task<JsonNode> UploadFileAsync(string id, IReadOnlyList<string> files, CancellationToken ct = default)
    {
        await SyncTargetAsync(ct);
        foreach (var f in files)
            if (!File.Exists(f)) return new JsonObject { ["ok"] = false, ["error"] = "FILE_NOT_FOUND", ["file"] = f };
        if (FrameLayer.TrySplit(id, out var uk, out var ui))
        {
            var (oid, osid) = await Frames.ResolveObjectAsync(uk, ui, ct);
            if (oid is null) return new JsonObject { ["ok"] = false, ["error"] = "NOT_FOUND", ["id"] = id };
            await _session.Connection.SendAsync("DOM.setFileInputFiles", new JsonObject
            {
                ["objectId"] = oid,
                ["files"] = new JsonArray(files.Select(f => (JsonNode)Path.GetFullPath(f)).ToArray())
            }, osid ?? _session.SessionId, ct);
            _log.Write("browser", "upload", $"file for {id}: {string.Join(", ", files.Select(Path.GetFileName))}");
            return new JsonObject { ["ok"] = true, ["id"] = id, ["files"] = files.Count };
        }
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
        await SyncTargetAsync(ct);
        var res = await _session.SendAsync("Page.captureScreenshot", new JsonObject { ["format"] = "jpeg", ["quality"] = quality }, ct);
        return res?["data"]?.GetValue<string>();
    }

    public async Task<JsonNode?> RunJsAsync(string code, CancellationToken ct = default)
    {
        await SyncTargetAsync(ct);
        var wrapped = $"(async()=>{{ try {{ return {{ ok:true, value: await ({code}) }}; }} catch(e) {{ return {{ ok:false, error: String(e&&e.message||e) }}; }} }})()";
        return await _session.EvaluateAsync(wrapped, awaitPromise: true, ct: ct);
    }

    // One real CDP text insertion into the focused editor, without a model round trip.
    public async Task InsertTextAsync(string text, CancellationToken ct = default)
    {
        await SyncTargetAsync(ct);
        await _session.SendAsync("Input.insertText",
            new JsonObject { ["text"] = text }, ct);
    }

    public async Task<string> CurrentUrlAsync(CancellationToken ct = default)
    {
        await SyncTargetAsync(ct);
        return (await _session.EvaluateAsync("location.href", awaitPromise: false, ct: ct))?.GetValue<string>() ?? "";
    }
}
