// OpenFill - Metadata: wersja 0.4, data 2026-10-12 19:24
using System.Text.Json.Nodes;
using OpenFill.Cli;
using OpenFill.Core;
using OpenFill.Core.Agent;
using OpenFill.Core.Cdp;
using OpenFill.Core.Config;
using OpenFill.Core.Hosting;
using OpenFill.Core.Logging;
using OpenFill.Core.Mcp;

namespace OpenFill.Tests;

/// <summary>A CDP connection bound to one tab of a shared browser connection: its own events, its own session.</summary>
internal sealed class SessionConnection : ICdpConnection
{
    private readonly ICdpConnection _inner;
    private readonly string _sessionId;
    public event Action<CdpEvent>? Event;
    public bool IsConnected => _inner.IsConnected;

    public SessionConnection(ICdpConnection inner, string sessionId)
    {
        _inner = inner;
        _sessionId = sessionId;
        _inner.Event += e => { if (e.SessionId == _sessionId) Event?.Invoke(new CdpEvent(e.Method, e.Params, null)); };
    }

    public Task<JsonNode?> SendAsync(string method, JsonObject? @params = null, string? sessionId = null, CancellationToken ct = default)
        => _inner.SendAsync(method, @params, _sessionId, ct);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>Browser tabs of a headless Chromium, each with its own connection (like the window's WebView2 tabs).</summary>
internal sealed class ChromiumTabs(WebSocketCdpConnection conn, string defaultUrl) : IBrowserTabs
{
    private readonly Dictionary<string, SessionConnection> _tabs = new();
    private int _n;

    public bool SupportsParallel => true;

    public string Adopt(CdpSession s)
    {
        var id = "ctab" + (++_n);
        lock (_tabs) _tabs[id] = new SessionConnection(conn, s.SessionId!);
        return id;
    }

    public async Task<string> OpenTabAsync(string? url, string label, CancellationToken ct)
    {
        var s = await CdpBootstrap.CreatePageAsync(conn, url ?? defaultUrl, 1000, 800, ct);
        await Task.Delay(600, ct);
        return Adopt(s);
    }

    public Task SelectTabAsync(string tabId, CancellationToken ct) => Task.CompletedTask;
    public Task NavigateAsync(string tabId, string url, CancellationToken ct) => _tabs[tabId].SendAsync("Page.navigate", new JsonObject { ["url"] = url }, ct: ct);
    public Task CloseTabAsync(string tabId) { lock (_tabs) _tabs.Remove(tabId); return Task.CompletedTask; }

    public async Task<string?> UrlAsync(string tabId)
    {
        SessionConnection? c;
        lock (_tabs) _tabs.TryGetValue(tabId, out c);
        if (c is null) return null;
        var r = await c.SendAsync("Runtime.evaluate", new JsonObject { ["expression"] = "location.href", ["returnByValue"] = true });
        return r?["result"]?["value"]?.GetValue<string>();
    }

    public bool Exists(string tabId) { lock (_tabs) return _tabs.ContainsKey(tabId); }
    public ICdpConnection? ConnectionOf(string tabId) { lock (_tabs) return _tabs.TryGetValue(tabId, out var c) ? c : null; }
    public void Strip(IReadOnlyList<TabInfo> tabs, string? activeTabId) { }
}

/// <summary>A page address that follows navigation; an address with "redirect" ends up on busy.com.</summary>
internal sealed class RedirectConnection(string start) : ICdpConnection
{
    public string Url { get; private set; } = start;
    public event Action<CdpEvent>? Event { add { } remove { } }
    public bool IsConnected => true;
    public Task<JsonNode?> SendAsync(string method, JsonObject? @params = null, string? sessionId = null, CancellationToken ct = default)
    {
        if (method == "Page.navigate")
        {
            var u = @params?["url"]?.GetValue<string>() ?? "";
            Url = u.Contains("redirect") ? "https://busy.com/landing" : u;
        }
        return Task.FromResult<JsonNode?>(method == "Runtime.evaluate"
            ? new JsonObject { ["result"] = new JsonObject { ["value"] = Url } }
            : new JsonObject());
    }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class TestGate(Func<string, string?> check) : IDomainGate
{
    public string? TryEnter(string domain) => check(domain);
}

public static class ParallelTests
{
    private static string S(JsonObject o, string key) => o[key]?.GetValue<string>() ?? "";

    public static async Task RunAsync(string testsite, Func<string, string> page)
    {
        await T.Section("DomainLocks: one task per site, all or nothing, refusal text", () =>
        {
            var locks = new DomainLocks();
            T.Check("free site is taken", locks.TryAcquire("T1", "mcp", "Fill the profile", new[] { "malt.de" }) is null);
            var h = locks.TryAcquire("T2", "panel", "Other job", new[] { "xing.com", "malt.de" });
            T.Eq("second task is refused, holder named", "T1", h?.TaskKey);
            T.Check("all or nothing: xing.com was not taken either", locks.HolderOf("xing.com") is null);
            T.Check("the same task may ask again", locks.TryAcquire("T1", "mcp", "Fill the profile", new[] { "malt.de", "xing.com" }) is null);
            T.Eq("task holds both", 2, locks.DomainsOf("T1").Count);
            T.Check("another site is free for another task", locks.TryAcquire("T3", "panel", "x", new[] { "booking.com" }) is null);

            var text = DomainLocks.RefusalText(h!);
            T.Contains("refusal: head", text, "REFUSED: the site malt.de is in use. Your task was NOT started.");
            T.Contains("refusal: who (another chat)", text, "another chat, task T1");
            T.NotContains("refusal: other task prompt stays private", text, "Fill the profile");
            T.Contains("refusal: privacy notice", text, "task details hidden from other callers");
            T.Contains("refusal: no way around", text, "There is no way around this.");
            T.Contains("refusal: try again later", text, "You can try again later.");
            T.Contains("refusal: duplicate note", text, "you may be duplicating the work of another chat, task T1");
            T.Check("refusal: no instructions to stop the other task", !text.Contains("stop it", StringComparison.OrdinalIgnoreCase));
            var panelHolder = locks.TryAcquire("T9", "panel", "z", new[] { "booking.com" });
            T.Contains("refusal: user typed in the window", DomainLocks.RefusalText(panelHolder!), "the user, typed in the OpenFill window (task T3)");
            T.Contains("refusal: mid-task variant", DomainLocks.RefusalText(h!, midTask: true), "is in use by another task, so this task may not go there.");

            locks.ReleaseAll("T1");
            T.Check("released after the task", locks.HolderOf("malt.de") is null && locks.DomainsOf("T1").Count == 0);
            T.Check("now the other task may go in", locks.TryAcquire("T2", "panel", "Other job", new[] { "malt.de" }) is null);
            return Task.CompletedTask;
        });

        await T.Section("CostGuard: 7-day window, archive, blocks stay until the user lifts them", () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "openfill_costwin_" + Guid.NewGuid().ToString("N")[..6]);
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, "costs.json");
            var now = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
            var g = new CostGuard(file, 7, () => now);
            g.Charge("example.com", 0.30);
            T.Check("charged", Math.Abs(g.Spent("example.com") - 0.30) < 1e-9);
            now = now.AddDays(6);
            g.Charge("example.com", 0.05);
            T.Check("both inside the window", Math.Abs(g.Spent("example.com") - 0.35) < 1e-9);
            now = now.AddDays(2);
            T.Check("the old cost dropped out of the window", Math.Abs(g.Spent("example.com") - 0.05) < 1e-9);
            T.Check("and was archived", Math.Abs(g.Archived("example.com") - 0.30) < 1e-9);
            T.Check("archive file written", File.Exists(Path.Combine(dir, "costs_archive.ndjson")));
            var reloaded = new CostGuard(file, 7, () => now);
            T.Check("window survives a restart", Math.Abs(reloaded.Spent("example.com") - 0.05) < 1e-9);

            g.Block("example.com");
            now = now.AddDays(40);
            T.Check("a block stays after the window has passed", g.IsBlocked("example.com"));
            T.Check("the user can lift it once the window is empty", g.Unblock("example.com", 1.0));
            T.Check("lifted", !g.IsBlocked("example.com"));

            var g2 = new CostGuard(file, 7, () => now);
            g2.Charge("busy.com", 1.2);
            g2.Block("busy.com");
            T.Check("the user cannot lift a block while the window is over the ceiling", !g2.Unblock("busy.com", 1.0));
            T.Eq("window days", 7, g2.WindowDays);
            return Task.CompletedTask;
        });

        await T.Section("TabManager and HistoryStore: busy tab, several current tasks", async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "openfill_par_tabs_" + Guid.NewGuid().ToString("N")[..6]);
            var ft = new FakeTabs();
            var tm = new TabManager(ft, Path.Combine(dir, "tabs.json"), 5);
            tm.RegisterFree(ft.NewBlank(), "Start");
            T.Check("no task: active tab is free", !tm.ActiveTabBusy());
            T.Check("fake tabs share one connection", !tm.SupportsParallel);
            var s = await tm.BeginAsync("k1", "first", null, default);
            T.Check("a task works in the open tab", tm.ActiveTabBusy());
            T.Check("no own connection without parallel support", tm.ConnectionOf(s.TabId) is null);
            await tm.EndAsync("k1");
            T.Check("after the task the tab is free again", !tm.ActiveTabBusy());

            // A tab opened by hand (to read a code in the e-mail): no task takes it over, none closes it, the person can.
            var handTab = ft.NewBlank();
            tm.RegisterUser(handTab, "New tab");
            var s2 = await tm.BeginAsync("k2", "second", null, default);
            T.Check("a new task does not take over the hand-made tab", s2.TabId != handTab);
            T.Check("the hand-made tab is still open", ft.Exists(handTab) && tm.Open().Any(t => t.TabId == handTab && t.State == "user"));
            for (var i = 0; i < 8; i++) { await tm.BeginAsync("kx" + i, "x" + i, null, default); await tm.EndAsync("kx" + i); }
            T.Check("the tab limit never closes it", ft.Exists(handTab));
            T.Check("the person can close it", await tm.UserCloseAsync(handTab) && !ft.Exists(handTab));
            T.Check("and it leaves no record", tm.Find("user:" + handTab) is null);

            var store = new HistoryStore(Path.Combine(dir, "tasks.ndjson"));
            var t0 = DateTime.UtcNow;
            store.Append(new HistoryRecord("A", "panel", "a", t0, null, "running", null, null));
            store.Append(new HistoryRecord("B", "mcp", "b", t0.AddSeconds(1), null, "running", null, null));
            store.Append(new HistoryRecord("C", "mcp", "c", t0.AddSeconds(2), null, "running", null, null));
            var list = store.Load(10, new[] { "B", "C" });
            T.Eq("both current tasks stay running", 2, list.Count(r => r.Status == "running"));
            T.Eq("the other one is interrupted", "interrupted", list.First(r => r.Id == "A").Status);
        });

        await T.Section("MCP: several tasks at once", async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "openfill_mcp_par_" + Guid.NewGuid().ToString("N")[..6]);
            var shots = Path.Combine(dir, "shots");
            var host = new FakeRunHost { Max = 3, ShotsDir = shots };
            var m = new McpTaskManager(host, dir, TimeSpan.FromSeconds(30));

            var a = await m.StartAsync("SLOW compare flight offers to Lisbon on skyscanner", 0, default, requester: "Test", chat: "Chat");
            var b = await m.StartAsync("SLOW update the LinkedIn headline to Senior Engineer", 0, default, requester: "Test", chat: "Chat");
            var c = await m.StartAsync("SLOW collect invoices from the Stripe dashboard", 0, default, requester: "Test", chat: "Chat");
            T.Eq("first runs", "running", S(a, "status"));
            T.Eq("second runs at the same time", "running", S(b, "status"));
            T.Eq("third runs at the same time", "running", S(c, "status"));
            T.Check("three different ids", new[] { S(a, "task_id"), S(b, "task_id"), S(c, "task_id") }.Distinct().Count() == 3);
            T.Contains("next step says other tasks may run", S(a, "next_step"), "Other tasks may run at the same time");

            var d = await m.StartAsync("A fourth completely different job about Booking hotels", 0, default, requester: "Test", chat: "Chat");
            T.Eq("fourth is refused: all slots taken", "busy", S(d, "status"));
            T.Eq("busy lists the three open tasks", 3, d["open_tasks"]?.AsArray().Count);
            T.Contains("busy says how many", S(d, "message"), "3 tasks, the most it may run at once (3)");

            var amb = await m.StatusAsync(null, 0, default);
            T.Eq("no id with several open tasks is an error", "error", S(amb, "status"));
            T.Contains("the error names the tasks", S(amb, "message"), S(a, "task_id"));
            T.Eq("status by id", "running", S(await m.StatusAsync(S(b, "task_id"), 0, default), "status"));

            var same = await m.StartAsync("SLOW update the LinkedIn headline to Senior Engineer", 0, default, requester: "Test", chat: "Chat");
            T.Eq("the same job returns the open task", S(b, "task_id"), S(same, "task_id"));

            var shot = await m.ScreenshotAsync(S(c, "task_id"), default);
            T.Eq("screenshot ok", "ok", S(shot, "status"));
            T.Eq("screenshot of the task asked for", S(c, "task_id"), host.LastShotTask);

            var cancelled = m.Cancel(S(b, "task_id"));
            T.Eq("cancel one task", "cancelled", S(cancelled, "status"));
            T.Check("the host was told which one", host.Stopped.Contains(S(b, "task_id")));
            T.Eq("the others keep running", "running", S(await m.StatusAsync(S(a, "task_id"), 0, default), "status"));
            T.Eq("the third keeps running too", "running", S(await m.StatusAsync(S(c, "task_id"), 0, default), "status"));

            var e = await m.StartAsync("A fresh job after the cancel: export the Notion pages", 0, default, requester: "Test", chat: "Chat");
            T.Eq("the freed slot can be used", "running", S(e, "status"));

            // A site that is in use: the host refuses, the caller gets the text, nothing is saved for the refused task.
            m.Cancel(S(e, "task_id"));
            var before = Directory.GetFiles(dir, "OF-*.json").Length;
            host.Refuse = t => t.Contains("alpha-one.com") ? new RunRefusal("domain_busy", DomainLocks.RefusalText(new LockHolder("OF-X", "mcp", "Sign up", "alpha-one.com", DateTime.UtcNow.AddMinutes(-3)))) : null;
            var r = await m.StartAsync("Open alpha-one.com and read the pricing", 0, default, requester: "Test", chat: "Chat");
            T.Eq("busy site: its own status", "domain_busy", S(r, "status"));
            T.Contains("busy site: the refusal text arrives whole", S(r, "message"), "Your task was NOT started.");
            T.Eq("busy site: no task record kept", before, Directory.GetFiles(dir, "OF-*.json").Length);
            T.Check("busy site: no task id handed out", r["task_id"] is null);

            m.Cancel(S(a, "task_id")); m.Cancel(S(c, "task_id"));
            await Task.Delay(300);

            var info = m.Info();
            T.Eq("info: parallel limit", 3, info["max_parallel_tasks"]?.GetValue<int>());
            T.Check("info: list of active tasks", info["active_tasks"] is JsonArray);

            var tools = McpServer.ToolList().ToJsonString();
            T.Check("tool text no longer says one task at a time", !tools.Contains("runs one task at a time"));
            T.Contains("tool text explains the site rule", tools, "ONE task at a time");
            T.Contains("screenshot takes a task id", McpServer.ToolList().First(t => t!["name"]!.GetValue<string>() == "openfill_screenshot")!["inputSchema"]!["properties"]!.ToJsonString(), "task_id");
        });

        await T.Section("Domain gate inside a task: navigate and redirects", async () =>
        {
            static JsonObject Call(string tool, string args) => JsonNode.Parse(
                "{\"id\":\"r\",\"usage\":{\"input_tokens\":1000,\"output_tokens\":0},\"output\":[{\"type\":\"function_call\",\"id\":\"f\",\"call_id\":\"c\",\"name\":\"" + tool +
                "\",\"arguments\":" + JsonValue.Create(args)!.ToJsonString() + "}]}")!.AsObject();
            var root = Path.Combine(Path.GetTempPath(), "openfill_gate_" + Guid.NewGuid().ToString("N")[..6]);

            (AgentRunner Runner, ScriptedModel Model, List<string> Asked) Build(ScriptedModel model, string sub)
            {
                var paths = new AppPaths("test", root + sub);
                paths.EnsureCreated();
                var log = new OpenFill.Core.Logging.EventLog(paths.Logs, paths.Runs);
                var conn = new RedirectConnection("https://start.com/");
                var browser = new OpenFill.Core.Browser.BrowserController(new CdpSession(conn), log);
                var config = new AppConfig { Model = "gpt-6.1-sol", MaxSteps = 20 };
                var asked = new List<string>();
                var gate = new TestGate(d => { asked.Add(d); return d == "busy.com" ? "REFUSED: the site busy.com is in use by another task, so this task may not go there." : null; });
                var registry = new ToolRegistry(browser, log, config, new OutputLimiter(paths.Overflow, 1000), new SiteNotesStore(paths.Notes, log),
                    new HeadlessInteraction(), new GapRecorder(paths.GapsFile, log), null, null, gate);
                return (new AgentRunner(model, registry, config, log, null, null, gate), model, asked);
            }

            var a = Build(new ScriptedModel(Call("navigate", "{\"url\":\"https://busy.com/x\"}"), Call("finish", "{\"status\":\"partial\",\"summary\":\"could not go there\"}")), "_a");
            var ra = await a.Runner.RunAsync("go to busy.com");
            T.Contains("navigate to a busy site is refused in the tool answer", a.Model.Requests[1]["input"]!.ToJsonString(), "REFUSED: the site busy.com is in use");
            T.Contains("the model is told not to open it", a.Model.Requests[1]["input"]!.ToJsonString(), "Do not open this site");
            T.Eq("the task ends as the model decided", "partial", ra.Status);

            var b = Build(new ScriptedModel(Call("navigate", "{\"url\":\"https://ok.com/redirect\"}"), Call("finish", "{\"status\":\"success\",\"summary\":\"done\"}")), "_b");
            var rb = await b.Runner.RunAsync("go to ok.com");
            T.Eq("a redirect onto a busy site stops the task", "partial", rb.Status);
            T.Contains("summary says why", rb.Summary, "another task is working on");
            T.Eq("the model was not asked again", 1, b.Model.Requests.Count);
            T.Check("both sites were checked", b.Asked.Contains("ok.com") && b.Asked.Contains("busy.com"));

            var c = Build(new ScriptedModel(Call("navigate", "{\"url\":\"https://ok.com/page\"}"), Call("finish", "{\"status\":\"success\",\"summary\":\"done\"}")), "_c");
            var rc = await c.Runner.RunAsync("go to ok.com");
            T.Eq("a free site is fine", "success", rc.Status);
        });

        await T.Section("Parallel tasks in the app: own tabs, one site at a time, limit, panel", async () =>
        {
            using var site = new StaticSite(testsite);
            using var chromium = new ChromiumLauncher();
            try
            {
                var wsUrl = await chromium.LaunchAsync(Path.Combine(Path.GetTempPath(), "openfill_test_profile_par_" + Guid.NewGuid().ToString("N")[..6]), headless: true);
                var conn = new WebSocketCdpConnection();
                await conn.ConnectAsync(wsUrl);
                var xing = site.BaseUrl + page("xing");
                var first = await CdpBootstrap.AttachPageAsync(conn, xing);

                var paths = new AppPaths("test", Path.Combine(Path.GetTempPath(), "openfill_test_root_par_" + Guid.NewGuid().ToString("N")[..6]));
                var config = new AppConfig { MaxSteps = 10, ConfirmIrreversible = true, MaxParallel = 3 };
                await using var panel = new PanelServer();
                panel.Start();
                await using var host = new AppHost(paths, config, new SecretStore(paths.SecretFile), first, panel, new HeadlessInteraction(),
                    () => new MockModelClient(askConfirm: true));
                var tabs = new ChromiumTabs(conn, xing);
                host.Tabs = new TabManager(tabs, Path.Combine(paths.Root, "tabs.json"), 5);
                host.Tabs.RegisterFree(tabs.Adopt(first), "Start");
                await host.InitAsync();
                T.Eq("three tasks may run at once", 3, host.MaxParallel);

                var ui = await CdpBootstrap.CreatePageAsync(conn, panel.Url, 520, 1400);
                await ui.SendAsync("Page.enable");
                async Task<string?> UiEval(string js) { try { return (await ui.EvaluateAsync(js))?.ToString(); } catch { return null; } }
                async Task<bool> Until(string js, int ms = 15000)
                {
                    var end = DateTime.UtcNow.AddMilliseconds(ms);
                    while (DateTime.UtcNow < end) { if (await UiEval(js) == "true") return true; await Task.Delay(150); }
                    return false;
                }
                T.Check("panel connected", await Until("document.getElementById('stateText').textContent === 'ready'"));
                T.Check("nothing finished yet: no 'last finished' line", await UiEval("document.getElementById('lastFin').hidden") == "true");

                var r1 = host.TryStart("Start from this page: https://alpha-one.com/\n\nFirst job on alpha-one.com: fill the first field", null);
                T.Check("first task starts", r1.Run is not null);
                var r2 = host.TryStart("Start from this page: https://alpha-one.com/shop\n\nAnother job on alpha-one.com, same site", null);
                T.Check("same site: refused", r2.Run is null);
                T.Eq("refusal kind", "domain_busy", r2.Refusal?.Kind);
                T.Contains("refusal text: head", r2.Refusal?.Message ?? "", "REFUSED: the site alpha-one.com is in use. Your task was NOT started.");
                T.Contains("refusal text: who", r2.Refusal?.Message ?? "", "the user, typed in the OpenFill window");
                T.NotContains("refusal text does not reveal another task", r2.Refusal?.Message ?? "", "First job on alpha-one.com");

                var r3 = host.TryStart("Start from this page: https://beta-two.com/\n\nSecond job on beta-two.com: fill the first field", null);
                var r4 = host.TryStart("Third job: type the word alpha-one.com into the first field of the page", null);
                T.Check("other sites run at the same time", r3.Run is not null && r4.Run is not null);
                T.Check("a site only named in the text (something to type) locks nothing", host.Locks.Held().Count == 2);
                var r5 = host.TryStart("Fourth job on delta-four.com", null);
                T.Check("the fourth is refused: limit", r5.Run is null && r5.Refusal?.Kind == "limit");
                T.Contains("limit text", r5.Refusal?.Message ?? "", "3 tasks are running, which is the maximum (3)");

                T.Check("three consent cards, one per task", await Until("document.querySelectorAll('.card').length === 3"));
                T.Check("panel lists the three tasks", await Until("document.querySelectorAll('#runs .runrow').length === 3"));
                T.Check("a started task's tab is in front: the header Stop button belongs to that task", await Until("/^Stop #\\d+$/.test(document.getElementById('stop').textContent)"));
                await UiEval("document.getElementById('showAll').click(); true");
                T.Check("stop button says stop all when all tasks are shown", await Until("document.getElementById('stop').textContent === 'Stop all'"));
                T.Check("task field locked: all slots taken", await UiEval("document.getElementById('task').readOnly && document.getElementById('run').disabled") == "true");
                T.Check("banner says several tasks", await Until("document.getElementById('bTitle').textContent.startsWith('3 tasks')"));
                T.Check("cards say which task they belong to", await UiEval("[...document.querySelectorAll('.card h3')].every(h => /task #\\d/.test(h.textContent))") == "true");
                T.Check("events are tagged by task, with a task filter", await Until("document.querySelectorAll('#events .ev .s').length > 0 && [...document.querySelectorAll('#events .ev .s')].some(e => /#2/.test(e.textContent)) && !document.getElementById('taskFilter').hidden"));
                T.Eq("three separate tabs", 3, host.Tabs!.Open().Count(t => t.State is "waiting" or "running"));
                T.Eq("two sites held: the ones the tasks were told to start from", 2, host.Locks.Held().Count);

                // Opening a task's tab narrows the panel to that task; its Stop button stops only that task.
                var shown = host.Tabs.Open().First(t => t.State is "waiting" or "running" && t.TabId is not null);
                host.Tabs.UserSelected(shown.TabId!);
                T.Check("tab in front: the panel shows only that task", await Until("document.getElementById('taskFilter').value !== 'all' && !document.getElementById('showAll').hidden"));
                T.Check("tab in front: the Stop button names the task", await UiEval("/^Stop #\\d+$/.test(document.getElementById('stop').textContent)") == "true");
                T.Check("tab in front: only its events are visible", await UiEval("[...document.querySelectorAll('#events .ev')].filter(e => e.style.display !== 'none').every(e => e.dataset.n === document.getElementById('taskFilter').value)") == "true");
                T.Check("tasks are named by site, not only by number", await UiEval("[...document.querySelectorAll('#taskFilter option')].filter(o => o.value !== 'all').every(o => /^#\\d+( · [a-z-]+\\.com)? · /.test(o.textContent)) && [...document.querySelectorAll('#taskFilter option')].some(o => /alpha-one\\.com/.test(o.textContent))") == "true");
                T.Check("the list rows show the site", await UiEval("[...document.querySelectorAll('#runs .runrow .rt b')].length === 2") == "true");
                await UiEval("document.getElementById('showAll').click(); true");
                T.Check("show all tasks again", await Until("document.getElementById('taskFilter').value === 'all' && document.getElementById('stop').textContent === 'Stop all'"));

                // The site stays closed to others until its task ends, however long it takes.
                await Task.Delay(500);
                T.Check("still refused while the first task is open", host.TryStart("Start from this page: https://alpha-one.com/again\n\nAgain", null).Run is null);

                await UiEval("document.querySelectorAll('.card .btn.primary').forEach(b => b.click()); true");
                var allDone = await Until("document.querySelectorAll('.result').length === 3", 30000);
                var res = await Task.WhenAll(r1.Run!, r3.Run!, r4.Run!);
                foreach (var x in res) Console.WriteLine("  [parallel] " + x.Status + ": " + x.Summary);
                Console.WriteLine("  [parallel] panel results: " + await UiEval("document.getElementById('result').innerText"));
                T.Check("all three finish with success", allDone && await UiEval("document.querySelectorAll('.result.success').length") == "3");
                T.Check("each task got its own result", res.All(x => x.Status == "success"));
                T.Check("no site held after the end", host.Locks.Held().Count == 0);
                T.Check("panel: runs list empty", await Until("document.querySelectorAll('#runs .runrow').length === 0"));
                T.Check("panel: task field unlocked", await UiEval("!document.getElementById('task').readOnly && !document.getElementById('run').disabled") == "true");
                T.Check("panel: last finished time is shown", await Until("!document.getElementById('lastFin').hidden && document.getElementById('lastFin').textContent.startsWith('Last task finished: ')"));
                T.Check("panel: last finished time looks like HH:mm", await UiEval("/Last task finished: \\d\\d:\\d\\d \\(/.test(document.getElementById('lastFin').textContent)") == "true");
                var runFiles = Directory.GetFiles(paths.Runs, "run_*.ndjson");
                T.Check("each task has its own run log", runFiles.Length >= 3);

                var diag = host.Diagnostics()!;
                T.Check("diagnostics: last finished", diag["last_task_finished"]?["at"] is not null);

                // The site is free again, and a task can be stopped on its own.
                var r6 = host.TryStart("Start from this page: https://alpha-one.com/\n\nLater job on alpha-one.com", null);
                T.Check("the site is free again", r6.Run is not null);
                T.Check("its card appears", await Until("document.querySelectorAll('.card').length === 1"));
                host.Stop(null);
                var r6res = await r6.Run!;
                T.Eq("a stop by the person is reported as stopped, not as a failure", "stopped", r6res.Status);
                T.Check("the site is free after a stop", host.Locks.Held().Count == 0);

                await conn.DisposeAsync();
            }
            catch { Console.WriteLine("  [chromium] " + chromium.Tail()); throw; }
        });
    }
}
