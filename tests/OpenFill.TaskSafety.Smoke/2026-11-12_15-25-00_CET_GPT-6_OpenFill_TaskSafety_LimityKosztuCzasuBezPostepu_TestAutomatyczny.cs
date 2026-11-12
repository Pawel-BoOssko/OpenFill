// 2026-11-12 15:25 CET | GPT-6 | OpenFill TaskSafety no-Luna watchdog smoke tests
using OpenFill.Core.Agent;
using OpenFill.Core.Hosting;
using OpenFill.Core.Mcp;

static void Check(bool value, string label)
{
    if (!value) throw new Exception("FAIL " + label);
    Console.WriteLine("PASS " + label);
}

Check(TaskSafetyLimits.Validate(0.001,10,15) is null, "lower bounds valid");
Check(TaskSafetyLimits.Validate(50,36000,3600) is null, "upper bounds valid");
Check(TaskSafetyLimits.Validate(0.0001, null, null) is not null, "invalid small budget rejected");
Check(TaskSafetyLimits.Validate(double.NaN, null, null) is not null, "non-finite budget rejected");
Check(TaskSafetyLimits.Validate(null,9,null) is not null, "invalid wall-clock rejected");
Check(TaskSafetyLimits.Validate(null,null,14) is not null, "invalid stall threshold rejected");
Check(TaskSafetyWatchdog.Evaluate(new TaskSafetyLimits(0.02,10,3), 0.03,
    TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1), false) == "max_cost_usd",
    "budget exceeds, highest priority");
Check(TaskSafetyWatchdog.Evaluate(new TaskSafetyLimits(null,10,null), null,
    TimeSpan.FromSeconds(10),TimeSpan.Zero,false) == "max_duration_seconds", "wall clock");
Check(TaskSafetyWatchdog.Evaluate(new TaskSafetyLimits(null,null,3), null,
    TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(3), false) == "stalled_no_progress", "idle no progress");
Check(TaskSafetyWatchdog.Evaluate(new TaskSafetyLimits(null,null,3), null,
    TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(3), true) is null, "human wait is not idle stall");
Check(TaskSafetyWatchdog.Evaluate(new TaskSafetyLimits(0.05,20,10), null,
    TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(2), false) is null, "no false stop without known cost");

static async Task<string?> TestWatchAsync(TaskSafetyLimits limits, Action<RunMeter> update, int delayMs = 1800)
{
    using var linked = new CancellationTokenSource();
    using var stop = new CancellationTokenSource();
    var meter = new RunMeter();
    var guard = new TaskSafetyWatchdog();
    var events = new List<string>();
    var task = guard.MonitorAsync(limits, meter, () => false, linked,
        reason => events.Add(reason), stop.Token);
    update(meter);
    await Task.Delay(delayMs);
    stop.Cancel();
    await task;
    Check(events.Count <= 1, "watchdog at most one cancellation event");
    return guard.StopReason;
}

var maxTime = await TestWatchAsync(new TaskSafetyLimits(null,1,null), _=>{});
Check(maxTime == "max_duration_seconds", "independent deadline timer cancellation");
var idleReason = await TestWatchAsync(new TaskSafetyLimits(null,null,1), _=>{});
Check(idleReason == "stalled_no_progress", "independent stalled timer cancellation");
var spend = await TestWatchAsync(new TaskSafetyLimits(0.005,null,null), m=>m.CostUsd=0.01,1000);
Check(spend == "max_cost_usd", "budget threshold cancels running task");

using (var linked = new CancellationTokenSource())
using (var stop = new CancellationTokenSource())
{
    var meter = new RunMeter();
    var waiting = true;
    var guard = new TaskSafetyWatchdog();
    var t = guard.MonitorAsync(new TaskSafetyLimits(null,null,1), meter,
        () => waiting, linked, _ => { }, stop.Token);
    await Task.Delay(1250);
    Check(guard.StopReason is null && !linked.IsCancellationRequested, "waiting for human excluded");
    waiting = false;
    await Task.Delay(1300);
    Check(guard.StopReason == "stalled_no_progress" && linked.IsCancellationRequested,
        "watchdog resumes after human wait");
    stop.Cancel(); await t;
}
var mcp = new McpTaskManager(new RefusingHost(), Path.Combine(Path.GetTempPath(), "of_limits_" + Guid.NewGuid().ToString("N")), TimeSpan.FromSeconds(1));
var badCost = await mcp.StartAsync("Check example.com", 0, CancellationToken.None,
    requester: "GPT-6", chat: "watchdog smoke", maxCostUsd: 0);
Check(badCost["status"]?.ToString() == "error", "MCP invalid zero budget fails before run");
var badTime = await mcp.StartAsync("Check example.com", 0, CancellationToken.None,
    requester: "GPT-6", chat: "watchdog smoke", maxDurationSeconds: 5);
Check(badTime["status"]?.ToString() == "error", "MCP short invalid deadline fails before run");
var badIdle = await mcp.StartAsync("Check example.com", 0, CancellationToken.None,
    requester: "GPT-6", chat: "watchdog smoke", maxIdleSeconds: 3);
Check(badIdle["status"]?.ToString() == "error", "MCP invalid idle fails before run");
Console.WriteLine("ALL_TASK_SAFETY_TESTS_PASSED");

sealed class RefusingHost : IRunHost
{
    public Task<RunConclusion>? StartRun(string task) => null;
    public void Stop(string? taskKey) { }
    public Task<string?> CaptureScreenshotAsync(string? taskKey, CancellationToken ct) => Task.FromResult<string?>(null);
}

