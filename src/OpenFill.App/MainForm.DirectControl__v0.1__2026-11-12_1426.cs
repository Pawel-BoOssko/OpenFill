// 2026-11-12 14:26 CET | GPT-6 | OpenFill | deterministic browser control and ChatGPT visit capture.
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Web.WebView2.Core;
using OpenFill.Core.Hosting;

namespace OpenFill.App;

public sealed partial class MainForm
{
    private DirectBrowserApi? _directBrowserApi;
    private ChatGptVisitStore? _chatGptVisits;
    private readonly ConcurrentDictionary<string, (string Title, DateTimeOffset When)> _lastChatActivity = new();
    private readonly SemaphoreSlim _directCommandGate = new(1, 1);

    private static readonly string ChatGptSidebarTitleScript = """
        (() => {
          const match = location.pathname.match(/(?:^|\/)c\/([a-zA-Z0-9_-]+)/);
          if (!match) return null;
          const suffix = '/c/' + match[1];
          const links = document.querySelectorAll('a[href]');
          for (const a of links) {
            let path;
            try { path = new URL(a.getAttribute('href'), location.href).pathname; } catch { continue; }
            if (path !== suffix && !path.endsWith(suffix)) continue;
            const title = (a.querySelector('[data-testid="history-item-title"]')?.textContent
              || a.querySelector('[title]')?.getAttribute('title')
              || a.innerText || a.textContent || '').trim();
            if (title) return title.slice(0, 500);
          }
          return null;
        })()
        """;

    private async Task RefreshChatGptTitleAsync(CoreWebView2 core, string expectedUrl)
    {
        if (!ChatGptVisitStore.TryParse(expectedUrl, out _, out _)) return;
        try
        {
            await Task.Delay(1100);
            if (!string.Equals(core.Source, expectedUrl, StringComparison.Ordinal)) return;
            var scriptResult = await core.ExecuteScriptAsync(ChatGptSidebarTitleScript);
            var node = JsonNode.Parse(scriptResult);
            if (node is JsonValue value && value.TryGetValue<string>(out var title) &&
                !string.IsNullOrWhiteSpace(title))
                CaptureChatGptVisit(core, title);
        }
        catch { /* page may have navigated away while the title was being read */ }
    }

    private void CaptureChatGptVisit(CoreWebView2 core, string? titleFromDom = null)
    {
        var store = _chatGptVisits;
        if (store is null) return;
        string url, title;
        try { url = core.Source; title = string.IsNullOrWhiteSpace(titleFromDom) ? core.DocumentTitle : titleFromDom; }
        catch { return; }
        if (!ChatGptVisitStore.TryParse(url, out var chatId, out _)) return;
        var now = DateTimeOffset.UtcNow;
        if (_lastChatActivity.TryGetValue(chatId, out var previous) &&
            previous.Title == title && (now - previous.When).TotalSeconds < 30)
            return;
        _lastChatActivity[chatId] = (title, now);
        _ = Task.Run(() =>
        {
            try { store.Record(url, title, now); }
            catch (Exception ex)
            {
                _host?.Session.Log.Write("browser", "chatgpt_visit_error",
                    "Could not save ChatGPT visit: " + ex.Message, status: "warn");
            }
        });
    }

    private async Task InstallChatGptActivityHookAsync(CoreWebView2 core)
    {
        core.WebMessageReceived += (sender, e) =>
        {
            if (!ChatGptVisitStore.TryParse(e.Source, out _, out _)) return;
            try
            {
                if (JsonNode.Parse(e.WebMessageAsJson) is JsonObject msg &&
                    msg["kind"]?.GetValue<string>() == "openfill.chatgpt.activity")
                    CaptureChatGptVisit(core, DirectString(msg, "title"));
            }
            catch { }
        };
        // The message is sent at most once a minute per document while the user interacts.
        // No network request and no AI inference. Also observes ChatGPT SPA route changes.
        await core.AddScriptToExecuteOnDocumentCreatedAsync("""
            (() => {
              const host = location.hostname.toLowerCase();
              if (host !== 'chatgpt.com' && host !== 'chat.openai.com') return;
              let last = 0;
              const ping = () => {
                if (Date.now() - last < 60000) return;
                last = Date.now();
                try {
                  const id = location.pathname.match(/\/c\/([a-zA-Z0-9_-]+)/)?.[1];
                  let title = '';
                  if (id) {
                    const suffix = '/c/' + id;
                    for (const a of document.querySelectorAll('a[href]')) {
                      try {
                        if (new URL(a.getAttribute('href'), location.href).pathname.endsWith(suffix)) {
                          title = (a.innerText || a.textContent || '').trim().slice(0,500);
                          if (title) break;
                        }
                      } catch (_) {}
                    }
                  }
                  chrome.webview.postMessage({kind:'openfill.chatgpt.activity', title});
                } catch (_) {}
              };
              document.addEventListener('pointerdown', ping, {capture:true, passive:true});
              document.addEventListener('keydown', ping, {capture:true, passive:true});
              window.addEventListener('focus', ping, {passive:true});
              ping();
            })();
            """);
    }

    private void StartDirectBrowserServices()
    {
        try
        {
            _chatGptVisits = new ChatGptVisitStore(Path.Combine(_paths.Memory, "chatgpt"));
            _host?.Session.Log.Write("browser", "chatgpt_registry",
                "Automatic ChatGPT visits DB: " + _chatGptVisits.DatabasePath);
        }
        catch (Exception ex)
        {
            _host?.Session.Log.Write("browser", "chatgpt_registry_error",
                "Could not start ChatGPT registry: " + ex.Message, status: "error");
        }
        try
        {
            _directBrowserApi = new DirectBrowserApi(_paths.Runtime, DispatchDirectBrowserCommandAsync);
            _host?.Session.Log.Write("browser", "direct_api",
                "Deterministic browser API listening at " + _directBrowserApi.Address +
                "; token file " + _directBrowserApi.TokenFile);
        }
        catch (Exception ex)
        {
            _host?.Session.Log.Write("browser", "direct_api_error",
                "Could not start deterministic browser API: " + ex.Message, status: "error");
        }
    }

    private async Task<JsonObject> DispatchDirectBrowserCommandAsync(JsonObject command)
    {
        await _directCommandGate.WaitAsync();
        try
        {
            if (!IsHandleCreated || IsDisposed || _closing)
                return FailDirect("BROWSER_UNAVAILABLE");
            var ready = new TaskCompletionSource<JsonObject>(TaskCreationOptions.RunContinuationsAsynchronously);
            BeginInvoke(new Action(async () =>
            {
                try { ready.TrySetResult(await ExecuteDirectBrowserCommandOnUiAsync(command)); }
                catch (Exception ex) { ready.TrySetResult(FailDirect(ex.GetType().Name + ": " + ex.Message)); }
            }));
            return await ready.Task;
        }
        finally { _directCommandGate.Release(); }
    }

    private static JsonObject FailDirect(string error) => new() { ["ok"] = false, ["error"] = error };
    private static JsonObject OkDirect(string action) => new() { ["ok"] = true, ["action"] = action };
    private static string? DirectString(JsonObject json, string key) =>
        json[key] is JsonValue value && value.TryGetValue<string>(out var s) ? s : null;

    private async Task<JsonObject> ExecuteDirectBrowserCommandOnUiAsync(JsonObject cmd)
    {
        var action = DirectString(cmd, "action")?.Trim().ToLowerInvariant() ?? "";
        if (action == "status")
            return new JsonObject
            {
                ["ok"] = true, ["service"] = "OpenFill direct browser",
                ["port"] = _directBrowserApi?.Port ?? DirectBrowserApi.DefaultPort,
                ["active_tab_id"] = _active?.Id,
                ["tabs"] = _tabs.Count,
                ["visit_database"] = _chatGptVisits?.DatabasePath,
                ["model_calls"] = 0
            };
        if (action == "tabs")
        {
            var tabs = new JsonArray();
            foreach (var tabInfo in _tabs.Values)
            {
                var browser = tabInfo.View.CoreWebView2;
                tabs.Add(new JsonObject
                {
                    ["id"] = tabInfo.Id, ["url"] = browser?.Source ?? "",
                    ["title"] = browser?.DocumentTitle ?? "",
                    ["active"] = ReferenceEquals(_active, tabInfo)
                });
            }
            return new JsonObject { ["ok"] = true, ["tabs"] = tabs };
        }
        if (action == "new_tab")
        {
            if (_tabMgr is null || !await _tabMgr.TryOpenUserTabAsync())
                return FailDirect("TAB_LIMIT_REACHED");
            var url = DirectString(cmd, "url");
            if (!string.IsNullOrWhiteSpace(url))
            {
                if (!IsSafeDirectUrl(url)) return FailDirect("INVALID_URL");
                _active?.View.CoreWebView2?.Navigate(url);
            }
            return new JsonObject { ["ok"] = true, ["tab_id"] = _active?.Id };
        }

        var id = DirectString(cmd, "tab_id");
        var tab = string.IsNullOrEmpty(id) ? _active : _tabs.GetValueOrDefault(id);
        if (tab?.View.CoreWebView2 is not { } core)
            return FailDirect("TAB_NOT_FOUND");
        if (_tabMgr?.Open().Any(t => t.TabId == tab.Id &&
            (t.State == "running" || t.State == "waiting")) == true)
            return FailDirect("TAB_BUSY_WITH_AI_TASK");

        if (action == "select_tab")
        {
            UserSelectTab(tab.Id);
            CaptureChatGptVisit(core);
            return new JsonObject { ["ok"] = true, ["tab_id"] = tab.Id };
        }
        if (action == "current_url")
            return new JsonObject { ["ok"] = true, ["url"] = core.Source, ["title"] = core.DocumentTitle };
        if (action == "navigate")
        {
            var url = DirectString(cmd, "url");
            if (!IsSafeDirectUrl(url)) return FailDirect("INVALID_URL");
            ActivateTab(tab);
            var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            ulong? requestedNavigationId = null;
            EventHandler<CoreWebView2NavigationStartingEventArgs>? started = null;
            EventHandler<CoreWebView2NavigationCompletedEventArgs>? completed = null;
            started = (sender, e) =>
            {
                if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var seen) &&
                    Uri.TryCreate(url, UriKind.Absolute, out var target) &&
                    string.Equals(seen.Host, target.Host, StringComparison.OrdinalIgnoreCase))
                    requestedNavigationId = e.NavigationId;
            };
            completed = (sender, e) =>
            {
                if (requestedNavigationId.HasValue && e.NavigationId == requestedNavigationId.Value)
                    done.TrySetResult(e.IsSuccess);
            };
            core.NavigationStarting += started;
            core.NavigationCompleted += completed;
            try
            {
                core.Navigate(url!);
                var success = await done.Task.WaitAsync(TimeSpan.FromSeconds(30));
                CaptureChatGptVisit(core);
                return new JsonObject { ["ok"] = success, ["url"] = core.Source,
                    ["title"] = core.DocumentTitle, ["tab_id"] = tab.Id,
                    ["error"] = success ? null : "NAVIGATION_FAILED" };
            }
            finally { core.NavigationStarting -= started; core.NavigationCompleted -= completed; }
        }
        if (action == "screenshot")
        {
            ActivateTab(tab);
            var folder = DirectString(cmd, "folder") ?? _paths.Screenshots;
            Directory.CreateDirectory(folder);
            var userFile = DirectString(cmd, "filename");
            var local = DateTimeOffset.Now;
            var timezone = local.Offset.Hours == 2 ? "CEST" : "CET";
            var name = string.IsNullOrWhiteSpace(userFile) ?
                $"{local:yyyy-MM-dd_HH-mm-ss}_{timezone}_GPT-6_OpenFill_Screenshot_Strony_DoAutomatycznejDokumentacji_{Guid.NewGuid():N}.png" :
                Path.GetFileName(userFile);
            if (!name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                return FailDirect("SCREENSHOT_REQUIRES_PNG");
            var file = Path.GetFullPath(Path.Combine(folder, name));
            await using (var stream = new FileStream(file, FileMode.CreateNew, FileAccess.Write))
                await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, stream);
            return new JsonObject { ["ok"] = true, ["file"] = file,
                ["bytes"] = new FileInfo(file).Length, ["url"] = core.Source,
                ["tab_id"] = tab.Id };
        }
        if (action == "wait")
        {
            var ms = cmd["ms"]?.GetValue<int>() ?? 1000;
            if (ms < 0 || ms > 30000) return FailDirect("WAIT_RANGE_0_30000");
            await Task.Delay(ms);
            return OkDirect(action);
        }

        string? script = action switch
        {
            "js" => DirectString(cmd, "script"),
            "html" => "document.documentElement.outerHTML.slice(0,2000000)",
            "text" => "document.body ? document.body.innerText.slice(0,1000000) : ''",
            "click" => BuildClickScript(DirectString(cmd, "selector")),
            "type" => BuildTypeScript(DirectString(cmd, "selector"), DirectString(cmd, "value")),
            "scroll" => $"(() => {{window.scrollBy({(cmd["x"]?.GetValue<int>() ?? 0)}, {(cmd["y"]?.GetValue<int>() ?? 0)});return {{x:window.scrollX,y:window.scrollY}};}})()",
            _ => null
        };
        if (action == "batch" && cmd["commands"] is JsonArray batch)
        {
            if (batch.Count > 100) return FailDirect("BATCH_MAX_100");
            var results = new JsonArray();
            foreach (var item in batch)
            {
                if (item is not JsonObject sub) return FailDirect("BATCH_INVALID_COMMAND");
                var result = await ExecuteDirectBrowserCommandOnUiAsync(sub);
                results.Add(result);
                if (result["ok"]?.GetValue<bool>() == false)
                    return new JsonObject { ["ok"] = false, ["results"] = results,
                        ["error"] = "BATCH_ABORTED_ON_ERROR" };
            }
            return new JsonObject { ["ok"] = true, ["results"] = results };
        }
        if (script is null) return FailDirect("UNKNOWN_ACTION_OR_MISSING_SCRIPT");
        var output = await core.ExecuteScriptAsync(script);
        JsonNode? value;
        try { value = JsonNode.Parse(output); }
        catch { value = JsonValue.Create(output); }
        CaptureChatGptVisit(core);
        if (value is JsonObject resultObject && resultObject["ok"] is JsonValue checkedValue &&
            checkedValue.TryGetValue<bool>(out var innerOk) && !innerOk)
            return new JsonObject { ["ok"] = false, ["action"] = action, ["value"] = value,
                ["error"] = resultObject["error"]?.GetValue<string>() ?? "BROWSER_ACTION_FAILED",
                ["tab_id"] = tab.Id, ["url"] = core.Source };
        return new JsonObject { ["ok"] = true, ["action"] = action, ["value"] = value,
            ["tab_id"] = tab.Id, ["url"] = core.Source };
    }

    private static bool IsSafeDirectUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) &&
        (u.Scheme == "https" || u.Scheme == "http" || url == "about:blank");

    private static string? BuildClickScript(string? selector)
    {
        if (string.IsNullOrWhiteSpace(selector)) return null;
        return "(() => {const e=document.querySelector(" + JsonSerializer.Serialize(selector) +
            ");if(!e)return {ok:false,error:'NOT_FOUND'};e.click();return {ok:true};})()";
    }

    private static string? BuildTypeScript(string? selector, string? value)
    {
        if (string.IsNullOrWhiteSpace(selector)) return null;
        return "(() => {const e=document.querySelector(" + JsonSerializer.Serialize(selector) +
            ");if(!e)return {ok:false,error:'NOT_FOUND'};const v=" +
            JsonSerializer.Serialize(value ?? "") + ";e.focus();" +
            "if('value' in e){const p=e.tagName==='TEXTAREA'?HTMLTextAreaElement.prototype:HTMLInputElement.prototype;" +
            "const d=Object.getOwnPropertyDescriptor(p,'value');if(d?.set)d.set.call(e,v);else e.value=v;}" +
            "else if(e.isContentEditable)e.textContent=v;else return {ok:false,error:'NOT_EDITABLE'};" +
            "e.dispatchEvent(new Event('input',{bubbles:true}));" +
            "e.dispatchEvent(new Event('change',{bubbles:true}));return {ok:true};})()";
    }
}

