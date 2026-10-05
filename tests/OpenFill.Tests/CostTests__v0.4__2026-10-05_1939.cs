// OpenFill - Metadata: wersja 0.4, data 2026-10-05 19:39
using System.Text.Json.Nodes;
using OpenFill.Core.Agent;
using OpenFill.Core.Browser;
using OpenFill.Core.Cdp;
using OpenFill.Core.Config;
using OpenFill.Core.Logging;
using OpenFill.Core.Mcp;
using OpenFill.Core.Model;

namespace OpenFill.Tests;

/// <summary>CDP channel that always reports the same page address (for the domain the cost is charged to).</summary>
internal sealed class UrlCdpConnection(string url) : ICdpConnection
{
    public event Action<CdpEvent>? Event { add { } remove { } }
    public bool IsConnected => true;
    public Task<JsonNode?> SendAsync(string method, JsonObject? @params = null, string? sessionId = null, CancellationToken ct = default)
        => Task.FromResult<JsonNode?>(method == "Runtime.evaluate"
            ? new JsonObject { ["result"] = new JsonObject { ["value"] = url } }
            : new JsonObject());
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>Person at the computer: answers every cost question with the given word and remembers what was asked.</summary>
internal sealed class CostAsker(string answer) : IUserInteraction
{
    public List<string> Human { get; } = new();
    public List<string> Plain { get; } = new();
    public Task<string> AskAsync(string question, IReadOnlyList<string>? options, CancellationToken ct) { Plain.Add(question); return Task.FromResult("(plain)"); }
    public Task<bool> ConfirmAsync(string what, CancellationToken ct) => Task.FromResult(false);
    public Task<string> AskHumanAsync(string question, IReadOnlyList<string>? options, CancellationToken ct) { Human.Add(question); return Task.FromResult(answer); }
}

/// <summary>Person who needs a while to answer (slower than the tool time limit).</summary>
internal sealed class SlowAsker(int delayMs, string answer) : IUserInteraction
{
    public Task<string> AskAsync(string question, IReadOnlyList<string>? options, CancellationToken ct) => Task.FromResult("(plain)");
    public Task<bool> ConfirmAsync(string what, CancellationToken ct) => Task.FromResult(false);
    public async Task<string> AskHumanAsync(string question, IReadOnlyList<string>? options, CancellationToken ct) { await Task.Delay(delayMs, ct); return answer; }
}

/// <summary>Run host that blocks one site and reports progress, for the MCP side of the cost limits.</summary>
internal sealed class CostRunHost : IRunHost
{
    public AskHandler? AskInterceptor { get; set; }
    public event Action<string>? Activity { add { } remove { } }
    public event Action<string?>? UserWaiting { add { } remove { } }
    public int Started;
    public Task<RunConclusion>? StartRun(string task)
    {
        Started++;
        return Task.Run(async () =>
        {
            await Task.Delay(500);
            return task.Contains("BLOCKME")
                ? new RunConclusion("blocked", "COST LIMIT: the domain example.org is blocked.", null)
                : new RunConclusion("success", "done", null);
        });
    }
    public string? CheckBlocked(string task) => task.Contains("stepstone") ? "The site stepstone.de has reached its hard cost limit. Only the user can lift this, in the OpenFill window." : null;
    public RunProgress? Progress => new(12, 0.0634, "Unusual cost");
    public string? LastUrlOf(string taskKey) => "https://example.org/profile";
    public void Stop() { }
    public Task<string?> CaptureScreenshotAsync(CancellationToken ct) => Task.FromResult<string?>(null);
}

public static class CostTests
{
    private static JsonObject Resp(string tool, string args = "{}", int inTokens = 60_000)
        => JsonNode.Parse($$"""
           {"id":"r","usage":{"input_tokens":{{inTokens}},"output_tokens":0},
            "output":[{"type":"function_call","id":"f","call_id":"c","name":"{{tool}}","arguments":{{JsonValue.Create(args)!.ToJsonString()}}}]}
           """)!.AsObject();

    private static JsonObject Finish() => JsonNode.Parse("""
        {"id":"r","usage":{"input_tokens":1000,"output_tokens":0},
         "output":[{"type":"function_call","id":"f","call_id":"c","name":"finish","arguments":"{\"status\":\"success\",\"summary\":\"ok\"}"}]}
        """)!.AsObject();

    private static (AgentRunner Runner, ScriptedModel Model, RunMeter Meter, CostGuard Guard) Build(
        string url, IUserInteraction ui, CostGuard guard, ScriptedModel model, string root, int toolTimeout = 60)
    {
        var paths = new AppPaths("test", root);
        paths.EnsureCreated();
        var log = new EventLog(paths.Logs, paths.Runs);
        var browser = new BrowserController(new CdpSession(new UrlCdpConnection(url)), log);
        var config = new AppConfig { Model = "gpt-6.1-sol", MaxSteps = 100, CostInfoUsd = 0.10, CostWarnUsd = 0.15, DomainLimitUsd = 0.40, ToolTimeoutSeconds = toolTimeout };
        // 1.0 USD per 1M input tokens: a 60 000-token call costs 6 cents.
        config.Prices["gpt-6.1-sol"] = new ModelPrice { Input = 1.0, Output = 0 };
        var registry = new ToolRegistry(browser, log, config, new OutputLimiter(paths.Overflow, 1000), new SiteNotesStore(paths.Notes, log), ui, new GapRecorder(paths.GapsFile, log), guard);
        var meter = new RunMeter();
        return (new AgentRunner(model, registry, config, log, guard, meter), model, meter, guard);
    }

    public static async Task RunAsync()
    {
        await T.Section("CostGuard: domains, charge, block, unblock, persistence", () =>
        {
            T.Eq("www is dropped", "stepstone.de", CostGuard.DomainOf("https://www.stepstone.de/jobs?q=1"));
            T.Eq("subdomain reduced to the site", "stepstone.de", CostGuard.DomainOf("https://de.stepstone.de/x"));
            T.Eq("two-part suffix kept", "example.co.uk", CostGuard.DomainOf("https://shop.example.co.uk/a"));
            T.Eq("bare host", "malt.de", CostGuard.DomainOf("Malt.DE"));
            T.Eq("port dropped", "localhost", CostGuard.DomainOf("http://localhost:8080/x"));
            T.Eq("blank page has no domain", "", CostGuard.DomainOf("about:blank"));
            T.Eq("empty", "", CostGuard.DomainOf(null));

            var file = Path.Combine(Path.GetTempPath(), "openfill_costs_" + Guid.NewGuid().ToString("N")[..6] + ".json");
            var g = new CostGuard(file);
            var changed = 0;
            g.Changed += () => changed++;
            T.Check("charge returns the running total", Math.Abs(g.Charge("stepstone.de", 0.25) - 0.25) < 1e-9);
            T.Check("charges add up", Math.Abs(g.Charge("stepstone.de", 0.10) - 0.35) < 1e-9);
            T.Check("a charge raises Changed", changed >= 2);
            T.Check("not blocked yet", !g.IsBlocked("stepstone.de"));
            g.Block("stepstone.de");
            T.Check("blocked", g.IsBlocked("stepstone.de"));
            T.Eq("blocked domain found in a task text", "stepstone.de", g.BlockedIn("Fill in my profile at https://www.stepstone.de/profile please"));
            T.Eq("bare name in a task text", "stepstone.de", g.BlockedIn("Go to stepstone.de and look"));
            T.Eq("other site is not blocked", null, g.BlockedIn("Open https://www.malt.de/ and e.g. this"));
            T.Eq("no addresses at all", null, g.BlockedIn("Fill in the form"));

            var reloaded = new CostGuard(file);
            T.Check("block survives a restart", reloaded.IsBlocked("stepstone.de"));
            T.Check("total survives a restart", Math.Abs(reloaded.Spent("stepstone.de") - 0.35) < 1e-9);

            T.Check("unblock lifts the block", reloaded.Unblock("stepstone.de") && !reloaded.IsBlocked("stepstone.de"));
            T.Check("and counts from zero again", reloaded.Spent("stepstone.de") < 1e-9);
            var row = reloaded.List().Single();
            T.Check("lifetime total is kept", Math.Abs(row.Lifetime - 0.35) < 1e-9);
            T.Check("unblock of a free domain does nothing", !reloaded.Unblock("malt.de"));
            reloaded.Block("stepstone.de");
            T.Check("unblock refused past the lifetime ceiling", !reloaded.Unblock("stepstone.de", 0.30) && reloaded.IsBlocked("stepstone.de"));
            T.Check("list marks it locked", reloaded.List(0.30).Single().Locked);
            T.Check("unblock allowed below the ceiling", reloaded.Unblock("stepstone.de", 1.00));
            return Task.CompletedTask;
        });

        await T.Section("Cost limits in the model loop: hard domain limit, questions, loops", async () =>
        {
            var root = Path.Combine(Path.GetTempPath(), "openfill_test_cost_" + Guid.NewGuid().ToString("N")[..6]);

            // A domain that already cost 30 cents: the second 6-cent call crosses 40 cents -> blocked, task ends.
            var guardA = new CostGuard();
            guardA.Charge("stepstone.de", 0.30);
            var a = Build("https://www.stepstone.de/jobs", new CostAsker("Continue"), guardA, new ScriptedModel(Resp("get_console")), root + "_a");
            var ra = await a.Runner.RunAsync("fill the profile");
            T.Eq("hard limit -> blocked", "blocked", ra.Status);
            T.Contains("summary says cost limit", ra.Summary, "COST LIMIT");
            T.Contains("summary says only the user can lift it", ra.Summary, "Only the user");
            T.Eq("stopped on the second call", 2, a.Model.Requests.Count);
            T.Check("domain is now blocked for good", guardA.IsBlocked("stepstone.de"));
            T.Check("meter shows steps and cost", a.Meter.Steps == 2 && a.Meter.CostUsd > 0.11);

            // Unblocking resets the 40-cent count, but the lifetime ceiling (1 dollar) still ends it.
            var guardL = new CostGuard();
            guardL.Charge("lifetime.com", 0.95); guardL.Block("lifetime.com"); guardL.Unblock("lifetime.com");
            var l = Build("https://www.lifetime.com/x", new CostAsker("Continue"), guardL, new ScriptedModel(Resp("get_console")), root + "_l");
            var rl = await l.Runner.RunAsync("go on");
            T.Eq("lifetime ceiling -> blocked on the first call", "blocked", rl.Status);
            T.Eq("one model call only", 1, l.Model.Requests.Count);
            T.Contains("message names the lifetime limit", rl.Summary, "lifetime");

            // Starting on a blocked domain: no model call at all.
            var b = Build("https://www.stepstone.de/jobs", new CostAsker("Continue"), guardA, new ScriptedModel(Finish()), root + "_b");
            var rb = await b.Runner.RunAsync("again");
            T.Eq("blocked domain -> blocked at once", "blocked", rb.Status);
            T.Eq("no model call", 0, b.Model.Requests.Count);

            // Navigating to a blocked domain is refused by the tool.
            var c = Build("about:blank", new CostAsker("Continue"), guardA,
                new ScriptedModel(Resp("navigate", "{\"url\":\"https://www.stepstone.de/jobs\"}", 1000), Finish()), root + "_c");
            var rc = await c.Runner.RunAsync("open it");
            T.Eq("task still ends normally", "success", rc.Status);
            T.Contains("navigate refused", c.Model.Requests[1]["input"]!.ToJsonString(), "is blocked");

            // Per-task thresholds on a free domain: notice at 10 cents, a question to the person at 15 cents.
            var askStop = new CostAsker("Stop");
            var d = Build("https://www.malt.de/profile", askStop, new CostGuard(), new ScriptedModel(Resp("get_console")), root + "_d");
            var rd = await d.Runner.RunAsync("fill malt");
            T.Eq("person says stop -> partial", "partial", rd.Status);
            T.Eq("asked on the third call (18 cents)", 3, d.Model.Requests.Count);
            T.Eq("one question, to the person only", 1, askStop.Human.Count);
            T.Eq("never asked through the normal door", 0, askStop.Plain.Count);
            T.Check("question names the cost", askStop.Human[0].Contains("$0.18") || askStop.Human[0].Contains("$0,18"));
            T.Check("warning is set for the status", d.Meter.Warning is { } w && w.Contains("unusual"));

            var askGo = new CostAsker("Continue");
            var e = Build("https://www.malt.de/profile", askGo,
                new CostGuard(), new ScriptedModel(Resp("get_console"), Resp("get_console"), Resp("get_console"), Finish()), root + "_e");
            var re = await e.Runner.RunAsync("fill malt");
            T.Eq("person says continue -> the task goes on and finishes", "success", re.Status);
            T.Eq("asked only once up to 24 cents", 1, askGo.Human.Count);
            T.Check("domain cost recorded", e.Guard.Spent("malt.de") > 0.17);

            // A model that clicks the same thing again and again is warned, then stopped.
            var repeating = new ScriptedModel(Resp("act", "{\"id\":\"of1\",\"action\":\"click\"}", 100));
            var f = Build("https://www.malt.de/profile", new CostAsker("Continue"), new CostGuard(), repeating, root + "_f");
            var rf = await f.Runner.RunAsync("click forever");
            T.Eq("loop -> partial", "partial", rf.Status);
            T.Contains("summary says repeated", rf.Summary, "repeated");
            T.Eq("stopped after the third warning", 15, repeating.Requests.Count);
            T.Contains("model got a loop warning", repeating.Requests[5]["input"]!.ToJsonString(), "LOOP WARNING");
        });

        await T.Section("Waiting for the person is not cut off by the tool time limit", async () =>
        {
            var root = Path.Combine(Path.GetTempPath(), "openfill_test_slow_" + Guid.NewGuid().ToString("N")[..6]);
            // Tool limit 1 s, the person needs 2.5 s: the answer must still arrive.
            var s = Build("https://www.cloudflare.com/signup", new SlowAsker(2500, "hunter2"), new CostGuard(),
                new ScriptedModel(Resp("ask_user", "{\"question\":\"password?\",\"user_only\":true}", 1000), Finish()), root, toolTimeout: 1);
            var rs = await s.Runner.RunAsync("sign up");
            T.Eq("task finishes", "success", rs.Status);
            var seen = s.Model.Requests[1]["input"]!.ToJsonString();
            T.Contains("model received the answer", seen, "hunter2");
            T.Check("no time-limit error", !seen.Contains("exceeded the time limit"));
        });

        await T.Section("MCP: blocked sites, progress, calibration", async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "openfill_mcp_cost_" + Guid.NewGuid().ToString("N")[..6]);
            var host = new CostRunHost();
            var m = new McpTaskManager(host, dir, TimeSpan.FromSeconds(30));

            var r1 = await m.StartAsync("Fill my profile on https://www.stepstone.de/", 1, default);
            T.Eq("blocked site is refused", "blocked", r1["status"]?.GetValue<string>());
            T.Eq("no task was started", 0, host.Started);
            T.Contains("tells the caller it cannot lift the block", r1["next_step"]!.GetValue<string>(), "you cannot do it");

            var r2 = await m.StartAsync("A normal long job BLOCKME", 0, default);
            T.Eq("running", "running", r2["status"]?.GetValue<string>());
            T.Eq("steps in the status", 12, r2["steps"]?.GetValue<int>());
            T.Check("cost in the status", Math.Abs((r2["cost_usd"]?.GetValue<double>() ?? 0) - 0.0634) < 1e-9);
            T.Eq("warning in the status", "Unusual cost", r2["warning"]?.GetValue<string>());
            var step = r2["next_step"]!.GetValue<string>();
            T.Contains("calibration for the caller", step, "10-30 steps");
            T.Contains("warning repeated in the advice", step, "WARNING: Unusual cost");

            var r3 = await m.StatusAsync(r2["task_id"]!.GetValue<string>(), 10, default);
            T.Eq("the run ended blocked", "blocked", r3["status"]?.GetValue<string>());
            T.Contains("explains who can lift it", r3["next_step"]!.GetValue<string>(), "only by the user");
            T.Contains("summary kept", r3["summary"]!.GetValue<string>(), "COST LIMIT");
            // A task left running by a crash: it comes back as interrupted, with the last address and the way to pick it up.
            var crashed = new McpTaskRecord { Id = "OF-20260101-0000-aaaa", TaskText = "Fill the profile", Status = "running", CreatedUtc = DateTime.UtcNow, UpdatedUtc = DateTime.UtcNow };
            File.WriteAllText(Path.Combine(dir, crashed.Id + ".json"), System.Text.Json.JsonSerializer.Serialize(crashed));
            var r4 = await m.StatusAsync(crashed.Id, 0, default);
            T.Eq("cut-off task is interrupted", "interrupted", r4["status"]?.GetValue<string>());
            T.Eq("last address given", "https://example.org/profile", r4["last_url"]?.GetValue<string>());
            T.Contains("hint names continue_task_id", r4["next_step"]!.GetValue<string>(), "continue_task_id=OF-20260101-0000-aaaa");
            T.Eq("a new task can start afterwards", "done", (await m.StartAsync("A normal job", 5, default))["status"]?.GetValue<string>());
        });
    }
}
