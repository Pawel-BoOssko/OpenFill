// OpenFill - Metadata: wersja 0.2, data 2026-10-05 17:47
using System.Text.Json.Nodes;
using OpenFill.Core.Agent;
using OpenFill.Core.Cdp;
using OpenFill.Core.Hosting;
using OpenFill.Core.Mcp;

namespace OpenFill.Tests;

/// <summary>A scripted CDP connection: remembers what was sent, lets the test raise events.</summary>
internal sealed class FakeConn(string name) : ICdpConnection
{
    public string Name { get; } = name;
    public List<string> Sent { get; } = new();
    public event Action<CdpEvent>? Event;
    public bool IsConnected => true;
    public void Raise(string method) => Event?.Invoke(new CdpEvent(method, new JsonObject(), null));
    public Task<JsonNode?> SendAsync(string method, JsonObject? @params = null, string? sessionId = null, CancellationToken ct = default)
    {
        Sent.Add(method);
        return Task.FromResult<JsonNode?>(new JsonObject { ["from"] = Name });
    }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>The window's tabs, without a window.</summary>
internal sealed class FakeTabs : IBrowserTabs
{
    public Dictionary<string, string?> Open { get; } = new();   // tab id -> current address
    public List<string> Log { get; } = new();
    public string? Selected { get; private set; }
    public IReadOnlyList<TabInfo> LastStrip { get; private set; } = Array.Empty<TabInfo>();
    private int _n;

    public string NewBlank() { var id = "tab" + (++_n); Open[id] = "about:blank"; Selected = id; return id; }

    public Task<string> OpenTabAsync(string? url, string label, CancellationToken ct)
    {
        var id = "tab" + (++_n);
        Open[id] = url ?? "about:blank";
        Selected = id;
        Log.Add("open " + id + " " + (url ?? "-"));
        return Task.FromResult(id);
    }
    public Task SelectTabAsync(string tabId, CancellationToken ct) { Selected = tabId; Log.Add("select " + tabId); return Task.CompletedTask; }
    public Task NavigateAsync(string tabId, string url, CancellationToken ct) { Open[tabId] = url; Log.Add("navigate " + tabId + " " + url); return Task.CompletedTask; }
    public Task CloseTabAsync(string tabId) { Open.Remove(tabId); Log.Add("close " + tabId); return Task.CompletedTask; }
    public Task<string?> UrlAsync(string tabId) => Task.FromResult(Open.TryGetValue(tabId, out var u) ? u : null);
    public bool Exists(string tabId) => Open.ContainsKey(tabId);
    public void Strip(IReadOnlyList<TabInfo> tabs, string? activeTabId) => LastStrip = tabs;
}

/// <summary>Run host that records the options it was given.</summary>
internal sealed class RecordingRunHost : IRunHost
{
    public AskHandler? AskInterceptor { get; set; }
    public event Action<string>? Activity;
    public event Action<string?>? UserWaiting;
    public readonly List<(string Task, RunOptions? Options)> Runs = new();

    public Task<RunConclusion>? StartRun(string task) => StartRun(task, null);
    public Task<RunConclusion>? StartRun(string task, RunOptions? options)
    {
        Runs.Add((task, options));
        Activity?.Invoke("noop");
        _ = UserWaiting;
        return Task.FromResult(new RunConclusion("success", "result of: " + task, null));
    }
    public void Stop() { }
    public Task<string?> CaptureScreenshotAsync(CancellationToken ct) => Task.FromResult<string?>(null);
}

public static class TabTests
{
    public static async Task RunAsync()
    {
        await T.Section("SwitchableCdpConnection: follows the active tab", async () =>
        {
            var sw = new SwitchableCdpConnection();
            var a = new FakeConn("a");
            var b = new FakeConn("b");
            var seen = new List<string>();
            sw.Event += ev => seen.Add(ev.Method);

            bool threw = false;
            try { await sw.SendAsync("Page.enable"); } catch (CdpException) { threw = true; }
            T.Check("sending with no tab fails clearly", threw);

            sw.Attach(a, "ka");
            T.Eq("key of the active tab", "ka", sw.ActiveTargetKey);
            var r = await sw.SendAsync("Page.enable");
            T.Eq("call goes to the active tab", "a", r?["from"]?.GetValue<string>());
            a.Raise("from.a");
            b.Raise("from.b.inactive");
            T.Eq("only the active tab's events arrive", "from.a", string.Join(",", seen));

            sw.Attach(b, "kb");
            seen.Clear();
            a.Raise("from.a.old");
            b.Raise("from.b");
            T.Eq("after switching, the old tab is silent", "from.b", string.Join(",", seen));
            r = await sw.SendAsync("Runtime.enable");
            T.Eq("call goes to the new tab", "b", r?["from"]?.GetValue<string>());

            sw.Detach(a); // not the active one: nothing changes
            T.Eq("detaching an inactive tab changes nothing", "kb", sw.ActiveTargetKey);
            sw.Detach(b);
            T.Check("detaching the active tab leaves none", sw.ActiveTargetKey is null);
        });

        await T.Section("TabManager: one tab per task, limit, never closes a busy tab", async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "openfill_tabs_" + Guid.NewGuid().ToString("N")[..6]);
            var ui = new FakeTabs();
            var tm = new TabManager(ui, Path.Combine(dir, "tabs.json"), 3);
            var first = ui.NewBlank();
            tm.RegisterFree(first, "Start");

            var s1 = await tm.BeginAsync("T1", "task one", null, default);
            T.Eq("the first task takes over the blank tab", first, s1.TabId);
            T.Check("no extra tab was opened", !ui.Log.Any(l => l.StartsWith("open")));
            ui.Open[s1.TabId] = "https://one.example/profile";
            await tm.EndAsync("T1");
            T.Eq("tab stays open after the task", 1, ui.Open.Count);
            T.Eq("its address is remembered", "https://one.example/profile", tm.Find("T1")?.LastUrl);
            T.Eq("state is finished", "finished", tm.Find("T1")?.State);

            var s2 = await tm.BeginAsync("T2", "task two", null, default);
            T.Check("a new task gets a new tab", s2.TabId != s1.TabId && ui.Open.Count == 2);
            await tm.EndAsync("T2");
            var s3 = await tm.BeginAsync("T3", "task three", null, default);
            await tm.EndAsync("T3");
            T.Eq("three tabs fit the limit of three", 3, ui.Open.Count);

            var s4 = await tm.BeginAsync("T4", "task four", null, default);
            T.Eq("limit holds: still three tabs", 3, ui.Open.Count);
            T.Check("the oldest finished tab (task one) was closed", !ui.Open.ContainsKey(s1.TabId) && ui.Log.Contains("close " + s1.TabId));
            T.Check("newer tabs survive", ui.Open.ContainsKey(s2.TabId) && ui.Open.ContainsKey(s3.TabId) && ui.Open.ContainsKey(s4.TabId));
            T.Check("closed task keeps its address", tm.Find("T1")?.TabId is null && tm.Find("T1")?.LastUrl == "https://one.example/profile");

            // T4 is running; mark it waiting for the person. A new task must not close it, even over the limit.
            tm.SetState("T4", "waiting");
            await tm.EndAsync("T2"); // T2 finished again
            ui.Log.Clear();
            var s5 = await tm.BeginAsync("T5", "task five", null, default);
            T.Check("the running/waiting tab was not closed", ui.Open.ContainsKey(s4.TabId) && !ui.Log.Contains("close " + s4.TabId));
            T.Eq("limit still holds (a finished tab made room)", 3, ui.Open.Count);

            // everything open is busy: the limit gives way instead of closing a busy tab
            tm.SetState("T2", "running");
            tm.SetState("T5", "running");
            ui.Log.Clear();
            var s6 = await tm.BeginAsync("T6", "task six", null, default);
            T.Check("with only busy tabs, nothing is closed", !ui.Log.Any(l => l.StartsWith("close")));
            T.Eq("the limit gave way by one", 4, ui.Open.Count);

            // the strip got the open tabs
            T.Eq("strip shows the open tabs", ui.Open.Count, ui.LastStrip.Count);
            T.Check("strip carries states", ui.LastStrip.Any(t => t.State == "waiting") && ui.LastStrip.Any(t => t.State == "running"));
            _ = s6;
        });

        await T.Section("TabManager: returning to a session", async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "openfill_tabs_" + Guid.NewGuid().ToString("N")[..6]);
            var store = Path.Combine(dir, "tabs.json");
            var ui = new FakeTabs();
            var tm = new TabManager(ui, store, 2);
            var a = await tm.BeginAsync("A", "malt profile", null, default);
            ui.Open[a.TabId] = "https://malt.example/edit";
            await tm.EndAsync("A");
            var b = await tm.BeginAsync("B", "xing profile", null, default);
            ui.Open[b.TabId] = "https://xing.example/me";
            await tm.EndAsync("B");

            // tab of A still open: the continuing task takes it over
            ui.Log.Clear();
            var c = await tm.BeginAsync("C", "malt profile, second part", "A", default);
            T.Check("continuing a task with an open tab", c.Continued && !c.Reopened);
            T.Eq("it reuses that very tab", a.TabId, c.TabId);
            T.Check("nothing opened or closed", ui.Log.SequenceEqual(new[] { "select " + a.TabId }));
            T.Eq("tab now belongs to the new task", a.TabId, tm.Find("C")?.TabId);
            T.Check("the earlier task no longer owns a tab", tm.Find("A")?.TabId is null);
            await tm.EndAsync("C");

            // tab of B gets closed by newer work, then B is continued: a new tab at B's last address
            var d = await tm.BeginAsync("D", "something else", null, default);
            T.Check("B's tab was closed to make room", !ui.Open.ContainsKey(b.TabId));
            await tm.EndAsync("D");
            ui.Log.Clear();
            var e = await tm.BeginAsync("E", "xing profile again", "B", default);
            T.Check("continuing a closed session reopens it", e.Continued && e.Reopened);
            T.Eq("at the last known address", "https://xing.example/me", e.Url);
            T.Check("a new tab was opened at that address", ui.Log.Any(l => l == "open " + e.TabId + " https://xing.example/me"));
            T.Eq("limit still holds", 2, ui.Open.Count);

            // an unknown session is simply a fresh tab
            await tm.EndAsync("E");
            var f = await tm.BeginAsync("F", "task", "does-not-exist", default);
            T.Check("unknown session: not continued", !f.Continued && !f.Reopened);

            // the map survives a restart
            var tm2 = new TabManager(new FakeTabs(), store, 2);
            T.Eq("address restored after restart", "https://xing.example/me", tm2.Find("B")?.LastUrl);
            T.Check("no tab is open after restart", tm2.Find("B")?.TabId is null && tm2.Open().Count == 0);
            var ui2 = new FakeTabs();
            var tm3 = new TabManager(ui2, store, 2);
            var g = await tm3.BeginAsync("G", "xing again", "B", default);
            T.Check("after restart a continued task reopens the address", g.Reopened && g.Url == "https://xing.example/me");
        });

        await T.Section("TabManager: closing by hand and the free tab", async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "openfill_tabs_" + Guid.NewGuid().ToString("N")[..6]);
            var ui = new FakeTabs();
            var tm = new TabManager(ui, Path.Combine(dir, "tabs.json"), 5);
            var blank = ui.NewBlank();
            tm.RegisterFree(blank, "Start");
            var s = await tm.BeginAsync("X", "job", null, default);
            T.Check("running tab cannot be closed by hand", !await tm.UserCloseAsync(s.TabId) && ui.Open.ContainsKey(s.TabId));
            ui.Open[s.TabId] = "https://x.example/done";
            await tm.EndAsync("X");
            T.Check("finished tab can be closed", await tm.UserCloseAsync(s.TabId) && !ui.Open.ContainsKey(s.TabId));
            T.Eq("its address is kept for later", "https://x.example/done", tm.Find("X")?.LastUrl);

            var blank2 = ui.NewBlank();
            tm.RegisterFree(blank2, "New tab");
            T.Check("a free tab can be closed and is forgotten", await tm.UserCloseAsync(blank2) && tm.Find("free:" + blank2) is null);

            tm.Limit = 0;
            T.Eq("limit is at least one", 1, tm.Limit);
        });

        await T.Section("Prompt for a continued task", () =>
        {
            var plain = AppHost.BuildPrompt("Do X", null, new TabStart("t", false, false, null));
            T.Eq("a fresh task is unchanged", "Do X", plain);
            var opts = new RunOptions("N", "O", "Earlier task: \"Do W\". It ended success; its result: ok.");
            var reopened = AppHost.BuildPrompt("Do X", opts, new TabStart("t", true, true, "https://a.example/p"));
            T.Contains("reopened: names the address", reopened, "https://a.example/p");
            T.Contains("reopened: says forms are gone", reopened, "typed into forms there is gone");
            T.Contains("earlier task included", reopened, "Earlier task: \"Do W\"");
            T.Contains("task text at the end", reopened, "Task: Do X");
            T.Contains("tells it to look first", reopened, "get_page");
            var same = AppHost.BuildPrompt("Do X", opts, new TabStart("t", true, false, null));
            T.Contains("open tab: left as it was", same, "still open, exactly as it was left");
            return Task.CompletedTask;
        });

        await T.Section("MCP: continuing an earlier task", async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "openfill_mcp_cont_" + Guid.NewGuid().ToString("N")[..6]);
            var host = new RecordingRunHost();
            var m = new McpTaskManager(host, dir, TimeSpan.FromSeconds(30));

            var r1 = await m.StartAsync("Update the Malt profile headline to Senior Engineer", 5, default);
            var id1 = r1["task_id"]!.GetValue<string>();
            T.Eq("first task: run options carry the task id", id1, host.Runs[0].Options?.TaskKey);
            T.Check("first task continues nothing", host.Runs[0].Options?.ContinueFromKey is null && host.Runs[0].Options?.PriorContext is null);

            var r2 = await m.StartAsync("Now add three skills to it: Go, Rust, SQL", 5, default, id1);
            T.Eq("explicit continue: tied to the earlier task", id1, host.Runs[1].Options?.ContinueFromKey);
            T.Contains("explicit continue: earlier task described", host.Runs[1].Options?.PriorContext ?? "", "Malt profile headline");
            T.Contains("explicit continue: earlier result handed on", host.Runs[1].Options?.PriorContext ?? "", "result of: Update the Malt");
            T.Eq("snapshot says what it continues", id1, r2["continued_from"]?.GetValue<string>());

            var r3 = await m.StartAsync("Update the Malt profile headline to Senior Engineer", 5, default);
            T.Eq("the very same job again: found by content", id1, host.Runs[2].Options?.ContinueFromKey);

            var r4 = await m.StartAsync("Book a table for four at a Lisbon restaurant on Friday", 5, default);
            T.Check("unrelated task continues nothing", host.Runs[3].Options?.ContinueFromKey is null);

            var s1 = await m.StartAsync("Open https://www.wikipedia.org and report the page title only.", 5, default);
            var s2 = await m.StartAsync("Open https://www.iana.org and report the page title only.", 5, default);
            T.Check("same sentence about another site: not the same job", host.Runs[5].Options?.ContinueFromKey is null);
            var s3 = await m.StartAsync("Open https://www.wikipedia.org and report the page title only.", 5, default);
            T.Eq("same sentence, same site: found by content", s1["task_id"]?.GetValue<string>(), host.Runs[6].Options?.ContinueFromKey);
            T.Check("SameJob: edited wording is not enough", !McpTaskManager.SameJob("Update the Malt profile headline to Senior Engineer", "Update the Malt profile headline to Staff Engineer"));

            var bad = await m.StartAsync("Anything", 0, default, "OF-nope");
            T.Eq("unknown continue id is an error", "error", bad["status"]?.GetValue<string>());
            T.Eq("and no run was started", 7, host.Runs.Count);
            _ = r3; _ = r4; _ = s2; _ = s3;

            var tools = McpServer.ToolList().ToJsonString();
            T.Contains("start_task offers continue_task_id", tools, "continue_task_id");
        });
    }
}
