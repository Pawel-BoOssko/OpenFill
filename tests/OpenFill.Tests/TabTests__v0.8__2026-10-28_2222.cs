// OpenFill - Metadata: wersja 0.8, data 2026-10-28 22:22
using System.Text.Json.Nodes;
using OpenFill.Core.Agent;
using OpenFill.Core.Cdp;
using OpenFill.Core.Hosting;
using OpenFill.Core.Mcp;

namespace OpenFill.Tests;

/// <summary>A scripted CDP connection: remembers what was sent, lets the test raise events.</summary>
internal sealed class FakeConn(string name) : ICdpConnection, INativeInput
{
    public List<string> Keys { get; } = new();
    public bool Focus { get; set; } = true;
    public Task<bool> FocusWindowAsync(CancellationToken ct) => Task.FromResult(Focus);
    public Task<bool> SendKeysAsync(IReadOnlyList<string> keys, CancellationToken ct) { if (Focus) Keys.AddRange(keys); return Task.FromResult(Focus); }
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
    public bool FailNavigate { get; set; }
    public bool FailClose { get; set; }
    public TaskCompletionSource<bool>? CloseStarted { get; set; }
    public TaskCompletionSource<bool>? AllowClose { get; set; }
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
    public Task NavigateAsync(string tabId, string url, CancellationToken ct) { if (FailNavigate) throw new InvalidOperationException("Navigation failed"); Open[tabId] = url; Log.Add("navigate " + tabId + " " + url); return Task.CompletedTask; }
    public async Task CloseTabAsync(string tabId)
    {
        CloseStarted?.TrySetResult(true);
        if (AllowClose is not null) await AllowClose.Task;
        if (FailClose) throw new InvalidOperationException("Close failed");
        Open.Remove(tabId);
        Log.Add("close " + tabId);
    }
    public Task<string?> UrlAsync(string tabId) => Task.FromResult(Open.TryGetValue(tabId, out var u) ? u : null);
    public bool Exists(string tabId) => Open.ContainsKey(tabId);
    public void Strip(IReadOnlyList<TabInfo> tabs, string? activeTabId) => LastStrip = tabs;
}

/// <summary>Run host that records the options it was given.</summary>
internal sealed class RecordingRunHost : IRunHost
{
    public readonly List<(string Task, RunOptions? Options)> Runs = new();

    public Task<RunConclusion>? StartRun(string task) => StartRun(task, null);
    public Task<RunConclusion>? StartRun(string task, RunOptions? options)
    {
        Runs.Add((task, options));
        options?.Activity?.Invoke("noop");
        return Task.FromResult(new RunConclusion("success", "result of: " + task, null));
    }
    public void Stop(string? taskKey) { }
    public Task<string?> CaptureScreenshotAsync(string? taskKey, CancellationToken ct) => Task.FromResult<string?>(null);
}

public static class TabTests
{
    public static async Task RunAsync()
    {
        await T.Section("TabManager: manual plus obeys five-tab cap", async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "openfill_manual_limit_" + Guid.NewGuid().ToString("N")[..8] + ".json");
            var ui = new FakeTabs();
            var first = ui.NewBlank();
            var mgr = new TabManager(ui, dir, 2);
            mgr.RegisterFree(first, "Start");
            T.Check("first manual tab added", await mgr.TryOpenUserTabAsync());
            T.Eq("two physical tabs", 2, ui.Open.Count);
            T.Check("second manual tab replaces free tab", await mgr.TryOpenUserTabAsync());
            T.Eq("manual tab creation never exceeds cap", 2, ui.Open.Count);
            T.Check("manual tabs cannot evict other user tabs", !await mgr.TryOpenUserTabAsync());
            T.Eq("both user tabs stay alive", 2, ui.Open.Count);
            try { File.Delete(dir); } catch { }
        });
        await T.Section("TabManager: manual plus cannot evict active tasks", async () =>
        {
            var file = Path.Combine(Path.GetTempPath(), "openfill_manual_active_" + Guid.NewGuid().ToString("N")[..8] + ".json");
            var ui = new FakeTabs();
            var first = ui.NewBlank();
            var mgr = new TabManager(ui, file, 2);
            mgr.RegisterFree(first, "Start");
            var a = await mgr.BeginAsync("running-a", "A", null, default);
            var b = await mgr.BeginAsync("running-b", "B", null, default);
            T.Check("two active task tabs exist", ui.Exists(a.TabId) && ui.Exists(b.TabId));
            T.Check("manual plus refused when all slots active", !await mgr.TryOpenUserTabAsync());
            T.Eq("still two tabs", 2, ui.Open.Count);
            mgr.SetState("running-b", "finished");
            T.Check("manual plus evicts only finished task", await mgr.TryOpenUserTabAsync());
            T.Eq("running task preserved", true, ui.Exists(a.TabId));
            T.Eq("finished tab closed", false, ui.Exists(b.TabId));
            T.Eq("still capped at two", 2, ui.Open.Count);
            try { File.Delete(file); } catch { }
        });
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

        await T.Section("SwitchableCdpConnection: a pop-up window of the tab", async () =>
        {
            var sw = new SwitchableCdpConnection();
            var tab = new FakeConn("tab");
            var pop = new FakeConn("pop");
            var seen = new List<string>();
            sw.Event += ev => seen.Add(ev.Method);
            sw.Attach(tab, "t1");
            T.Check("no pop-up at first", !sw.HasPopup && !sw.OnPopup && sw.Select(true) == false);
            T.Eq("selecting a missing pop-up changes nothing", "t1", sw.ActiveTargetKey);

            sw.AttachPopup(pop, "t1:popup:a");
            T.Check("the task follows the pop-up that opened", sw.HasPopup && sw.OnPopup && sw.ActiveTargetKey == "t1:popup:a");
            var r = await sw.SendAsync("Page.enable");
            T.Eq("calls go to the pop-up", "pop", r?["from"]?.GetValue<string>());
            tab.Raise("from.tab"); pop.Raise("from.pop");
            T.Eq("only the pop-up's events arrive while it is in front", "from.pop", string.Join(",", seen));

            T.Check("the task can go back to the page", sw.Select(false) && !sw.OnPopup && sw.HasPopup && sw.ActiveTargetKey == "t1");
            seen.Clear(); tab.Raise("from.tab"); pop.Raise("from.pop");
            T.Eq("then only the page's events arrive", "from.tab", string.Join(",", seen));
            T.Check("and return to the pop-up", sw.Select(true) && sw.ActiveTargetKey == "t1:popup:a");

            sw.DetachPopup(new FakeConn("other"));
            T.Check("detaching another connection changes nothing", sw.OnPopup);
            sw.DetachPopup(pop);
            T.Check("when the pop-up closes the task is back on the page", !sw.HasPopup && !sw.OnPopup && sw.ActiveTargetKey == "t1");
            r = await sw.SendAsync("Page.enable");
            T.Eq("calls go to the page again", "tab", r?["from"]?.GetValue<string>());

            // Real keys go to the window of the page the connection is on now (the saved-password list ignores CDP keys).
            var tab2 = new FakeConn("tab2"); var pop2 = new FakeConn("pop2");
            var sw2 = new SwitchableCdpConnection();
            T.Check("no page, no native input", !await sw2.FocusWindowAsync(default) && !await sw2.SendKeysAsync(new[] { "Enter" }, default));
            sw2.Attach(tab2, "t2");
            T.Check("focus and keys go to the page", await sw2.FocusWindowAsync(default) && await sw2.SendKeysAsync(new[] { "ArrowDown", "Enter" }, default) && string.Join(",", tab2.Keys) == "ArrowDown,Enter");
            sw2.AttachPopup(pop2, "t2:popup:a");
            await sw2.SendKeysAsync(new[] { "Enter" }, default);
            T.Check("while a pop-up is in front the keys go to the pop-up", string.Join(",", pop2.Keys) == "Enter" && tab2.Keys.Count == 2);
            pop2.Focus = false;
            T.Check("a window that has no focus gets no keys", !await sw2.SendKeysAsync(new[] { "Enter" }, default) && pop2.Keys.Count == 1);
        });

        await T.Section("PopupRegistry: completed tab owns its pop-ups", () =>
        {
            var popups = new OpenFill.Core.Browser.PopupRegistry<object>();
            var first = new object();
            var second = new object();
            popups.Add("https://login.a.invalid/popup", first, "task-tab-a");
            popups.Add("https://login.b.invalid/popup", second, "task-tab-b");
            T.Eq("pop-ups: only owning tab returned", 1, popups.ForOwner("task-tab-a").Count);
            T.Check("pop-ups: tab a cannot access tab b's pop-up",
                popups.ForOwner("task-tab-a").Single() == first &&
                popups.ForOwner("task-tab-b").Single() == second);
            T.Eq("pop-ups: lookup does not dispose before window closes", 2, popups.Count);
            popups.Remove(first);
            T.Eq("pop-ups: FormClosed removes owner's pop-up", 0, popups.ForOwner("task-tab-a").Count);
            T.Eq("pop-ups: other tab still owns its window", 1, popups.ForOwner("task-tab-b").Count);
            return Task.CompletedTask;
        });
        await T.Section("OF-231: manual close and new task cannot race", async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "openfill_close_race_" + Guid.NewGuid().ToString("N")[..8]);
            var ui = new FakeTabs();
            var tm = new TabManager(ui, Path.Combine(dir, "tabs.json"), 5);
            var initial = ui.NewBlank();
            tm.RegisterFree(initial, "ready");
            ui.CloseStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            ui.AllowClose = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var closing = tm.UserCloseAsync(initial);
            await ui.CloseStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var nextTask = tm.BeginAsync("race-next", "new task", null, default);
            await Task.Delay(200);
            T.Check("new task waits while user tab is still being closed", !nextTask.IsCompleted);
            T.Check("closing physical tab stays visible until close completes", ui.Exists(initial));
            ui.AllowClose.TrySetResult(true);
            T.Check("user closure completes", await closing);
            var started = await nextTask.WaitAsync(TimeSpan.FromSeconds(3));
            T.Check("new task receives a live physical tab after closure", ui.Exists(started.TabId) && started.TabId != initial);
            T.Eq("one physical tab after serialized close and begin", 1, ui.Open.Count);
        });
        await T.Section("OF-231: failed manual tab closure preserves its record", async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "openfill_userclose_" + Guid.NewGuid().ToString("N")[..8]);
            var ui = new FakeTabs();
            var tm = new TabManager(ui, Path.Combine(dir, "tabs.json"), 5);
            var userTab = ui.NewBlank();
            tm.RegisterUser(userTab, "manual tab");
            ui.FailClose = true;
            var refused = false;
            try { await tm.UserCloseAsync(userTab); } catch (InvalidOperationException) { refused = true; }
            T.Check("failure is reported without silently losing the tab", refused);
            T.Eq("failed closure still owns original user tab", userTab, tm.Find("user:" + userTab)?.TabId);
            T.Check("failed closure still has a physical tab", tm.IsTabPresent("user:" + userTab));
            ui.FailClose = false;
            T.Check("user can retry closing the same tab", await tm.UserCloseAsync(userTab));
            T.Check("successful close removes user tab and metadata", !ui.Exists(userTab) && tm.Find("user:" + userTab) is null);
        });

        await T.Section("OF-231: five user tabs occupy the five-card limit", async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "openfill_userlimit_" + Guid.NewGuid().ToString("N")[..8]);
            var ui = new FakeTabs();
            var tm = new TabManager(ui, Path.Combine(dir, "tabs.json"), 5);
            var manual = new List<string>();
            for (var i = 0; i < 5; i++)
            {
                var id = ui.NewBlank();
                manual.Add(id);
                tm.RegisterUser(id, "User tab " + i);
            }
            using var stop = new CancellationTokenSource();
            var queued = tm.BeginAsync("user-limit", "agent task", null, stop.Token);
            await Task.Delay(250);
            T.Check("agent waits if five protected user tabs exist", !queued.IsCompleted);
            T.Eq("no sixth tab opened behind user tabs", 5, ui.Open.Count);
            stop.Cancel();
            var cancelled = false;
            try { await queued; } catch (OperationCanceledException) { cancelled = true; }
            T.Check("cancelled queued task leaves all user tabs untouched", cancelled && manual.All(ui.Open.ContainsKey));
            var removed = await tm.UserCloseAsync(manual[0]);
            T.Check("user can explicitly close their own tab", removed);
            var next = await tm.BeginAsync("user-after-close", "agent task", null, default);
            T.Check("agent starts only when one physical slot is freed", ui.Open.Count == 5 && !manual.Contains(next.TabId));
            T.Check("remaining user tabs survive", manual.Skip(1).All(ui.Open.ContainsKey));
        });

        await T.Section("OF-231: stale completed tab is not an eviction slot", async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "openfill_stalelimit_" + Guid.NewGuid().ToString("N")[..8]);
            var ui = new FakeTabs();
            var tm = new TabManager(ui, Path.Combine(dir, "tabs.json"), 5);
            var old = await tm.BeginAsync("finished-and-closed", "old task", null, default);
            await tm.EndAsync("finished-and-closed");
            ui.Open.Remove(old.TabId); // The browser closed it externally; metadata is stale.
            var manual = new List<string>();
            for (var i = 0; i < 5; i++)
            {
                var id = ui.NewBlank();
                manual.Add(id);
                tm.RegisterUser(id, "protected " + i);
            }
            using var stop = new CancellationTokenSource();
            var queued = tm.BeginAsync("after-stale", "new task", null, stop.Token);
            await Task.Delay(250);
            T.Check("stale finished metadata does not bypass physical limit", !queued.IsCompleted);
            T.Eq("stale entry does not open a sixth tab", 5, ui.Open.Count);
            stop.Cancel();
            var cancelled = false;
            try { await queued; } catch (OperationCanceledException) { cancelled = true; }
            T.Check("queue cancellation works with stale metadata", cancelled && manual.All(ui.Open.ContainsKey));
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

            // All three tabs are busy; a fourth task waits without exceeding the limit.
            tm.SetState("T2", "running");
            tm.SetState("T3", "running");
            tm.SetState("T5", "running");
            ui.Log.Clear();
            var queued = tm.BeginAsync("T6", "task six", null, default);
            await Task.Delay(300);
            T.Check("sixth task is queued while all tabs are busy", !queued.IsCompleted);
            T.Eq("no sixth physical tab", 3, ui.Open.Count);
            T.Check("busy tabs stay open", !ui.Log.Any(l => l.StartsWith("close")));
            await tm.EndAsync("T5");
            var s6 = await queued.WaitAsync(TimeSpan.FromSeconds(3));
            T.Eq("queued task starts once a tab is released", 3, ui.Open.Count);
            // the strip got the open tabs
            T.Eq("strip shows the open tabs", ui.Open.Count, ui.LastStrip.Count);
            T.Check("strip carries states", ui.LastStrip.Any(t => t.State == "waiting") && ui.LastStrip.Any(t => t.State == "running"));
            _ = s6;
        });

        await T.Section("OF-230: checkout tab survives completed task for user payment", async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "openfill_checkout_" + Guid.NewGuid().ToString("N")[..8]);
            var ui = new FakeTabs();
            var manager = new TabManager(ui, Path.Combine(dir, "tabs.json"), 5, releaseFinishedTabs: false);
            var start = ui.NewBlank();
            manager.RegisterFree(start, "Start");
            var task = await manager.BeginAsync("checkout-1", "shopping checkout", null, default,
                requester: "ChatGPT / shop", preferredDomain: "example.com");
            ui.Open[task.TabId] = "https://example.com/checkout/payment";
            await manager.EndAsync("checkout-1");
            T.Eq("finished checkout keeps same physical tab", task.TabId, manager.Find("checkout-1")?.TabId);
            T.Eq("checkout URL still open", "https://example.com/checkout/payment", ui.Open[task.TabId]);
            T.Check("checkout not navigated to blank", !ui.Log.Any(x => x.Contains("about:blank")));
            T.Check("checkout not automatically closed", !ui.Log.Any(x => x == "close " + task.TabId));
            var resumed = await manager.BeginAsync("checkout-2", "continue payment", null, default,
                requester: "ChatGPT / shop", preferredDomain: "example.com");
            T.Eq("same chat continues original browser session", task.TabId, resumed.TabId);
            T.Check("session reused, no reopening URL", resumed.Continued && !resumed.Reopened);
            await manager.EndAsync("checkout-2");
            T.Check("user can manually close finished checkout", await manager.UserCloseAsync(task.TabId));
            T.Check("only manual action removed checkout tab", !ui.Exists(task.TabId));
        });
        await T.Section("TabManager: resource release and continuation", async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "openfill_release_" + Guid.NewGuid().ToString("N")[..6]);
            var ui = new FakeTabs();
            var tm = new TabManager(ui, Path.Combine(dir, "tabs.json"), 5, releaseFinishedTabs: true);
            var initial = ui.NewBlank();
            tm.RegisterFree(initial, "Start");
            var a = await tm.BeginAsync("A", "first", null, default);
            ui.Open[a.TabId] = "https://one.example/form";
            var b = await tm.BeginAsync("B", "second", null, default);
            ui.Open[b.TabId] = "https://two.example/results";
            await tm.EndAsync("A");
            await tm.EndAsync("B");
            T.Eq("resource release: one blank tab after parallel tasks", 1, ui.Open.Count);
            T.Eq("resource release: blank tab address", "about:blank", ui.Open.Values.Single());
            T.Eq("resource release: saved first URL", "https://one.example/form", tm.Find("A")?.LastUrl);
            T.Eq("resource release: saved second URL", "https://two.example/results", tm.Find("B")?.LastUrl);
            T.Check("resource release: both task tabs detached", tm.Find("A")?.TabId is null && tm.Find("B")?.TabId is null);
            var next = await tm.BeginAsync("C", "continue", "A", default);
            T.Check("resource release: continuation reopened URL", next.Continued && next.Reopened);
            T.Eq("resource release: resumed URL", "https://one.example/form", ui.Open[next.TabId]);
            T.Eq("resource release: recycled physical tab", initial, next.TabId);
        });

        await T.Section("TabManager: failed WebView2 release does not orphan live tabs", async () =>
        {
            var folder = Path.Combine(Path.GetTempPath(), "openfill_failed_release_" + Guid.NewGuid().ToString("N")[..6]);
            var ui = new FakeTabs();
            var tabs = new TabManager(ui, Path.Combine(folder, "tabs.json"), 3, releaseFinishedTabs: true);
            var initial = ui.NewBlank();
            tabs.RegisterFree(initial, "Start");

            var a = await tabs.BeginAsync("failure-nav", "web page", null, default);
            ui.Open[a.TabId] = "https://big.example/dashboard";
            ui.FailNavigate = true;
            await tabs.EndAsync("failure-nav");
            T.Eq("failed blank navigation: original tab still tracked", a.TabId, tabs.Find("failure-nav")?.TabId);
            T.Eq("failed blank navigation: original address still live", "https://big.example/dashboard", ui.Open[a.TabId]);
            T.Check("failed blank navigation: no fake free tab created", !tabs.Open().Any(x => x.State == "free"));
            ui.FailNavigate = false;

            var b = await tabs.BeginAsync("success-b", "second task", null, default);
            ui.Open[b.TabId] = "https://other.example/page";
            await tabs.EndAsync("success-b");
            T.Eq("successful release: blank free tab exists", "about:blank", ui.Open[b.TabId]);

            var c = await tabs.BeginAsync("failure-close", "third task", null, default);
            ui.Open[c.TabId] = "https://third.example/work";
            var helper = await tabs.BeginAsync("helper", "spare free tab", null, default);
            await tabs.EndAsync("helper");
            T.Check("failed close test: spare free tab exists", tabs.Open().Any(x => x.State == "free"));
            ui.FailClose = true;
            await tabs.EndAsync("failure-close");
            T.Eq("failed tab close: live browser is still tracked", c.TabId, tabs.Find("failure-close")?.TabId);
            T.Check("failed tab close: browser not dropped from UI", ui.Open.ContainsKey(c.TabId));
            T.Eq("failed tab close: URL preserved", "https://third.example/work", tabs.Find("failure-close")?.LastUrl);
        });
        await T.Section("OF-226: explicit navigation command identifies target domain", () =>
        {
            T.Eq("English Open URL", "linkedin.com", AppHost.InitialTaskDomain("Open https://www.linkedin.com/in/example"));
            T.Eq("Polish open URL", "linkedin.com", AppHost.InitialTaskDomain("Otworz https://www.linkedin.com/in/example"));
            T.Eq("Polish go to URL", "linkedin.com", AppHost.InitialTaskDomain("Przejdz do https://www.linkedin.com/mynetwork/"));
            T.Eq("legacy panel start URL", "linkedin.com", AppHost.InitialTaskDomain("Start from this page: https://www.linkedin.com/mynetwork/"));
            T.Check("URL merely to enter in a form does not lock the site", AppHost.InitialTaskDomain("Type https://linkedin.com into the website field") is null);
            T.Check("URL mentioned in task body is not automatically a browser target", AppHost.InitialTaskDomain("Send a note containing https://linkedin.com/in/example") is null);
            return Task.CompletedTask;
        });
        await T.Section("TabManager: same chat/domain automatically reuses finished tab", async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "openfill_reuse_" + Guid.NewGuid().ToString("N")[..8]);
            var ui = new FakeTabs();
            var tm = new TabManager(ui, Path.Combine(dir, "tabs.json"), 5);
            var first = await tm.BeginAsync("first", "visit LinkedIn", null, default,
                requester: "ChatGPT / Chat A", preferredDomain: "linkedin.com");
            ui.Open[first.TabId] = "https://www.linkedin.com/in/example";
            await tm.EndAsync("first");
            var second = await tm.BeginAsync("second", "follow-up", null, default,
                requester: "ChatGPT / Chat A", preferredDomain: "linkedin.com");
            T.Eq("same chat automatically takes its finished tab", first.TabId, second.TabId);
            T.Check("automatic reuse is treated as continuation", second.Continued);
            T.Eq("reason identifies reused session", "existing_session_reused", tm.Find("second")?.Reason);
            await tm.EndAsync("second");
            var otherChat = await tm.BeginAsync("other", "separate chat", null, default,
                requester: "ChatGPT / Chat B", preferredDomain: "linkedin.com");
            T.Check("different chat never takes the original tab", otherChat.TabId != second.TabId);
            T.Eq("reason explains new chat tab", "no_matching_finished_chat_domain_tab", tm.Find("other")?.Reason);
            await tm.EndAsync("other");
            var otherDomain = await tm.BeginAsync("other-domain", "another domain", null, default,
                requester: "ChatGPT / Chat A", preferredDomain: "example.org");
            T.Check("same chat does not reuse unrelated domain", otherDomain.TabId != second.TabId);
            await tm.EndAsync("other-domain");
        });        await T.Section("TabManager: failed eviction preserves live tab and strict limit", async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "openfill_eviction_" + Guid.NewGuid().ToString("N")[..8]);
            var ui = new FakeTabs();
            var tm = new TabManager(ui, Path.Combine(dir, "tabs.json"), 1);
            var a = await tm.BeginAsync("eviction-a", "first", null, default);
            ui.Open[a.TabId] = "https://example.org/session";
            await tm.EndAsync("eviction-a");
            ui.FailClose = true;
            bool rejected = false;
            try { await tm.BeginAsync("eviction-b", "second", null, default); }
            catch (IOException) { rejected = true; }
            T.Check("eviction failure refuses extra tab", rejected && ui.Open.Count == 1);
            T.Eq("failed eviction still tracks live browser", a.TabId, tm.Find("eviction-a")?.TabId);
            T.Eq("saved session URL survives failed eviction", "https://example.org/session", tm.Find("eviction-a")?.LastUrl);
            ui.FailClose = false;
            var b = await tm.BeginAsync("eviction-b", "second", null, default);
            T.Check("later retry can free capacity", b.TabId != a.TabId && ui.Open.Count == 1);
        });        await T.Section("OF-234: three sequential batches in one chat and domain", async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "openfill_batches_" + Guid.NewGuid().ToString("N")[..8]);
            var ui = new FakeTabs();
            var tm = new TabManager(ui, Path.Combine(dir, "tabs.json"), 5, releaseFinishedTabs: true);
            var initial = ui.NewBlank();
            tm.RegisterFree(initial, "Start");
            var stable = true;
            var continued = 0;
            for (var batch = 1; batch <= 3; batch++)
            {
                for (var entry = 1; entry <= 5; entry++)
                {
                    var id = $"batch-{batch}-entry-{entry}";
                    var started = await tm.BeginAsync(id, "LinkedIn batch", null, default,
                        requester: "Model / same chat", preferredDomain: "linkedin.com");
                    stable &= started.TabId == initial && ui.Open.Count == 1;
                    if (started.Continued && started.Reopened) continued++;
                    ui.Open[started.TabId] = "https://www.linkedin.com/mynetwork/";
                    await tm.EndAsync(id);
                }
            }
            T.Check("15 synthetic tasks stay on one reusable physical tab", stable);
            T.Eq("later 14 tasks reuse saved same-chat URL", 14, continued);
            T.Eq("one idle renderer after three batches", 1, ui.Open.Count);
            T.Eq("previous page URL persisted", "https://www.linkedin.com/mynetwork/", tm.Find("batch-3-entry-5")?.LastUrl);
        });
        await T.Section("OF-234: five busy cards queue and cancellation", async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "openfill_five_" + Guid.NewGuid().ToString("N")[..8]);
            var ui = new FakeTabs();
            var tm = new TabManager(ui, Path.Combine(dir, "tabs.json"), 5);
            var ids = new List<string>();
            for (var n = 0; n < 5; n++)
            {
                var id = $"busy-{n}";
                var t = await tm.BeginAsync(id, "busy site", null, default);
                ids.Add(t.TabId);
                if (n == 1) tm.SetState(id, "waiting");
            }
            using var cancelled = new CancellationTokenSource();
            var sixth = tm.BeginAsync("cancelled-sixth", "sixth", null, cancelled.Token);
            await Task.Delay(350);
            T.Check("sixth task waits behind five busy cards", !sixth.IsCompleted && ui.Open.Count == 5);
            cancelled.Cancel();
            bool interrupted = false;
            try { await sixth; } catch (OperationCanceledException) { interrupted = true; }
            T.Check("cancelled queued task exits without taking a card", interrupted && ui.Open.Count == 5);
            await tm.EndAsync("busy-0");
            var later = await tm.BeginAsync("replacement", "replacement", null, default);
            T.Check("new task starts after a finished card is available", later.TabId != ids[0] && ui.Open.Count == 5);
            T.Check("waiting-for-user card is never evicted", ui.Open.ContainsKey(ids[1]));
        });
        await T.Section("OF-229: lost session without URL reports why new tab opens", async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "openfill_losttab_" + Guid.NewGuid().ToString("N")[..8]);
            var ui = new FakeTabs();
            var tm = new TabManager(ui, Path.Combine(dir, "tabs.json"), 5);
            var a = await tm.BeginAsync("lost-a", "first", null, default);
            await tm.EndAsync("lost-a");
            ui.Open.Remove(a.TabId);
            T.Check("stale tracked tab is not reported open", !tm.IsTabPresent("lost-a"));
            var b = await tm.BeginAsync("lost-b", "continue", "lost-a", default);
            T.Check("missing physical tab causes a new tab", b.TabId != a.TabId && !b.Reopened);
            T.Eq("reason identifies unrecoverable missing URL", "previous_session_unavailable_no_saved_url", tm.Find("lost-b")?.Reason);
            T.Check("unavailable prior tab ownership cleared", tm.Find("lost-a")?.TabId is null);
        });
        await T.Section("OF-234: aged persisted session resumes by domain after restart", async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "openfill_aged_" + Guid.NewGuid().ToString("N")[..8]);
            var store = Path.Combine(dir, "tabs.json");
            var firstUi = new FakeTabs();
            var first = new TabManager(firstUi, store, 5);
            var visit = await first.BeginAsync("before-night", "LinkedIn", null, default,
                requester: "Model / same chat", preferredDomain: "linkedin.com");
            firstUi.Open[visit.TabId] = "https://www.linkedin.com/mynetwork/";
            await first.EndAsync("before-night");
            var persisted = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(store))!.AsArray();
            persisted[0]!["LastUsedUtc"] = DateTime.UtcNow.AddHours(-7);
            File.WriteAllText(store, persisted.ToJsonString());
            var nextUi = new FakeTabs();
            var after = new TabManager(nextUi, store, 5);
            var resumed = await after.BeginAsync("after-night", "Next batch", null, default,
                requester: "Model / same chat", preferredDomain: "linkedin.com");
            T.Check("7-hour-old stored session is eligible to resume", resumed.Continued && resumed.Reopened);
            T.Eq("aged session restores last address", "https://www.linkedin.com/mynetwork/", nextUi.Open[resumed.TabId]);
            T.Eq("reopen reason survives source state", "previous_tab_closed_reopened_saved_url", after.Find("after-night")?.Reason);
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
            var tm = new TabManager(ui, Path.Combine(dir, "tabs.json"), 5, releaseFinishedTabs: true);
            var blank = ui.NewBlank();
            tm.RegisterFree(blank, "Start");
            var s = await tm.BeginAsync("X", "job", null, default);
            T.Check("running tab cannot be closed by hand", !await tm.UserCloseAsync(s.TabId) && ui.Open.ContainsKey(s.TabId));
            ui.Open[s.TabId] = "https://x.example/done";
            await tm.EndAsync("X");
            T.Eq("resource mode releases the finished task tab", null, tm.Find("X")?.TabId);
            T.Eq("resource mode blanks the reusable browser", "about:blank", ui.Open[s.TabId]);
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

            var r1 = await m.StartAsync("Update the Malt profile headline to Senior Engineer", 5, default, requester: "Test", chat: "Chat");
            var id1 = r1["task_id"]!.GetValue<string>();
            T.Eq("first task: run options carry the task id", id1, host.Runs[0].Options?.TaskKey);
            T.Check("first task continues nothing", host.Runs[0].Options?.ContinueFromKey is null && host.Runs[0].Options?.PriorContext is null);

            var r2 = await m.StartAsync("Now add three skills to it: Go, Rust, SQL", 5, default, id1, requester: "Test", chat: "Chat");
            T.Eq("explicit continue: tied to the earlier task", id1, host.Runs[1].Options?.ContinueFromKey);
            T.Contains("explicit continue: earlier task described", host.Runs[1].Options?.PriorContext ?? "", "Malt profile headline");
            T.Contains("explicit continue: earlier result handed on", host.Runs[1].Options?.PriorContext ?? "", "result of: Update the Malt");
            T.Eq("snapshot says what it continues", id1, r2["continued_from"]?.GetValue<string>());

            var r3 = await m.StartAsync("Update the Malt profile headline to Senior Engineer", 5, default, requester: "Test", chat: "Chat");
            T.Eq("the very same job again: found by content", id1, host.Runs[2].Options?.ContinueFromKey);

            var r4 = await m.StartAsync("Book a table for four at a Lisbon restaurant on Friday", 5, default, requester: "Test", chat: "Chat");
            T.Check("unrelated task continues nothing", host.Runs[3].Options?.ContinueFromKey is null);

            var s1 = await m.StartAsync("Open https://www.wikipedia.org and report the page title only.", 5, default, requester: "Test", chat: "Chat");
            var s2 = await m.StartAsync("Open https://www.iana.org and report the page title only.", 5, default, requester: "Test", chat: "Chat");
            T.Check("same sentence about another site: not the same job", host.Runs[5].Options?.ContinueFromKey is null);
            var s3 = await m.StartAsync("Open https://www.wikipedia.org and report the page title only.", 5, default, requester: "Test", chat: "Chat");
            T.Eq("same sentence, same site: found by content", s1["task_id"]?.GetValue<string>(), host.Runs[6].Options?.ContinueFromKey);
            T.Check("SameJob: edited wording is not enough", !McpTaskManager.SameJob("Update the Malt profile headline to Senior Engineer", "Update the Malt profile headline to Staff Engineer"));

            var bad = await m.StartAsync("Anything", 0, default, "OF-nope", requester: "Test", chat: "Chat");
            T.Eq("unknown continue id is an error", "error", bad["status"]?.GetValue<string>());
            T.Eq("and no run was started", 7, host.Runs.Count);
            _ = r3; _ = r4; _ = s2; _ = s3;

            var tools = McpServer.ToolList().ToJsonString();
            T.Contains("start_task offers continue_task_id", tools, "continue_task_id");
        });
    }
}
