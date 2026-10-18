// OpenFill - Metadata: wersja 0.15, data 2026-10-18 21:20
using System.Text;
using System.Text.Json.Nodes;
using OpenFill.Core.Agent;
using OpenFill.Core.Mcp;

namespace OpenFill.Tests;

/// <summary>Run host without a browser or a model: scripted by words in the task text. Several tasks may run at once (Max).</summary>
internal sealed class FakeRunHost : IRunHost
{
    private readonly Dictionary<string, CancellationTokenSource> _cts = new();
    public int Started;
    public Action<string>? EmitActivity;
    public Func<VerifiedAction, int>? EmitVerified;
    public Func<string, JsonObject?>? TabStatus;
    public JsonObject? TabDetailsOf(string taskKey) => TabStatus?.Invoke(taskKey);
    public int Max { get; set; } = 1;
    private bool _maintenanceActive;
    public JsonObject Maintenance(int? minutes, string? reason, string? by)
    {
        if (minutes.HasValue) _maintenanceActive = minutes.Value > 0;
        return new JsonObject
        {
            ["active"] = _maintenanceActive,
            ["running_tasks"] = _cts.Count,
            ["reason"] = reason ?? "test maintenance"
        };
    }
    public int MaxParallel => Max;
    /// <summary>When set and it returns a refusal for the task text, the task is not started (like a site that is in use).</summary>
    public Func<string, RunRefusal?>? Refuse { get; set; }
    public readonly List<string?> Stopped = new();

    public Task<RunConclusion>? StartRun(string task) => StartRun(task, null);

    public Task<RunConclusion>? StartRun(string task, RunOptions? options)
    {
        if (Refuse?.Invoke(task) is not null) return null;
        Started++;
        EmitActivity = options?.Activity;
        EmitVerified = options?.Verified;
        var cts = new CancellationTokenSource();
        lock (_cts) _cts[options?.TaskKey ?? "-"] = cts;
        var ct = cts.Token;
        return Task.Run(async () =>
        {
            options?.Activity?.Invoke("act of3 type");
            await Task.Delay(150);
            if (task.Contains("PARTIAL_REGISTRATION_TEST"))
                return new RunConclusion("partial", "Organizer confirmed participant A; participant B not confirmed. Check external effects before retrying.", null);
            if (task.Contains("REFUSED_REGISTRATION_TEST"))
                return new RunConclusion("failed", "Organizer rejected the registration. No confirmation received.", null);
            if (task.Contains("TEMP_CREDENTIAL_TEST"))
                return new RunConclusion("success", "Temporary account credential created", null,
                    new[] { new TemporaryCredential("example.invalid", "local-demo", "mock-value-123", true) });
            if (task.Contains("ASKME"))
            {
                var ans = await options!.Ask!("Which date?", new[] { "May", "June" }, ct);
                return new RunConclusion("success", "answered: " + ans, null);
            }
            if (task.Contains("NEEDCODE"))
            {
                options?.UserWaiting?.Invoke("Verification code from the e-mail?");
                await Task.Delay(1500);
                options?.UserWaiting?.Invoke(null);
                return new RunConclusion("success", "code entered by the user", null);
            }
            if (task.Contains("USERSTOP"))
                return new RunConclusion("stopped", "The task was stopped by the person at the OpenFill window, on purpose.", null);
            if (task.Contains("SLOW"))
            {
                try { await Task.Delay(30000, ct); }
                catch (OperationCanceledException) { return new RunConclusion("failed", "stopped", null); }
            }
            return new RunConclusion("success", "done: " + task, null);
        });
    }

    public StartResult TryStart(string task, RunOptions? options)
    {
        if (_maintenanceActive) return new StartResult(null, new RunRefusal("maintenance", "OpenFill is upgrading; try after maintenance."));
        if (Refuse?.Invoke(task) is { } refusal) return new StartResult(null, refusal);
        var run = StartRun(task, options);
        return new StartResult(run, run is null ? new RunRefusal("other", "could not start") : null);
    }

    public void Stop(string? taskKey)
    {
        lock (_cts)
        {
            Stopped.Add(taskKey);
            foreach (var kv in _cts)
                if (taskKey is null || kv.Key == taskKey) kv.Value.Cancel();
        }
    }

    /// <summary>Directory where the fake "browser" saves its screenshots (null = cannot take one).</summary>
    public string? ShotsDir { get; set; }
    public string? LastShotTask;

    public Task<string?> CaptureScreenshotAsync(string? taskKey, CancellationToken ct)
    {
        LastShotTask = taskKey;
        if (ShotsDir is null) return Task.FromResult<string?>(null);
        Directory.CreateDirectory(ShotsDir);
        var name = "shot-test-" + Guid.NewGuid().ToString("N")[..6] + ".jpg";
        File.WriteAllBytes(Path.Combine(ShotsDir, name), new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 });
        return Task.FromResult<string?>(name);
    }
}

public static class McpTests
{
    private static string S(JsonObject o, string key) => o[key]?.GetValue<string>() ?? "";

    public static async Task RunAsync()
    {
        await T.Section("MCP: temporary account data survives tool handoff", async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "openfill_cred_test_" + Guid.NewGuid().ToString("N")[..8]);
            var mgr = new McpTaskManager(new FakeRunHost(), dir, TimeSpan.FromSeconds(5));
            var result = await mgr.StartAsync("TEMP_CREDENTIAL_TEST", 5, default, requester: "Test", chat: "Test");
            T.Eq("temporary result finishes", "done", S(result, "status"));
            var item = result["temporary_credentials"]?[0];
            T.Eq("temporary service", "example.invalid", item?["service"]?.GetValue<string>());
            T.Eq("temporary account", "local-demo", item?["login"]?.GetValue<string>());
            T.Eq("temporary value", "mock-value-123", item?["password"]?.GetValue<string>());
            T.Eq("change flag", true, item?["change_immediately"]?.GetValue<bool>());
            T.Eq("vault flag", true, item?["vault_saved"]?.GetValue<bool>());
            var later = new McpTaskManager(new FakeRunHost(), dir, TimeSpan.FromSeconds(5));
            var retrieved = await later.StatusAsync(S(result, "task_id"), 0, default);
            T.Eq("temporary data persists", "mock-value-123", retrieved["temporary_credentials"]?[0]?["password"]?.GetValue<string>());
            try { Directory.Delete(dir, true); } catch { }
        });
        await T.Section("OF-161: partial and rejected registrations are not success", async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "openfill_reg_outcomes_" + Guid.NewGuid().ToString("N")[..8]);
            var host = new FakeRunHost { Max = 3 };
            var mgr = new McpTaskManager(host, dir, TimeSpan.FromSeconds(5));
            const string key = "radar:partial:organizer-event";
            var partial = await mgr.StartAsync("PARTIAL_REGISTRATION_TEST", 5, default,
                requester: "Networking Radar", chat: "Registration", idempotencyKey: key);
            T.Eq("partial registration yields task done with partial outcome", "done", S(partial, "status"));
            T.Eq("partial outcome is not success", "partial", S(partial, "outcome"));
            T.Contains("partial summary preserves organizer uncertainty", S(partial, "summary"), "participant B not confirmed");
            var reloadedHost = new FakeRunHost();
            var reloaded = new McpTaskManager(reloadedHost, dir, TimeSpan.FromSeconds(5));
            var repeated = await reloaded.StartAsync("PARTIAL_REGISTRATION_TEST", 0, default,
                requester: "Networking Radar", chat: "Registration", idempotencyKey: key);
            T.Eq("partial result keeps task id after process restart", S(partial, "task_id"), S(repeated, "task_id"));
            T.Eq("retry does not send another registration", 0, reloadedHost.Started);
            T.Eq("partial outcome survives restart", "partial", S(repeated, "outcome"));
            var refused = await reloaded.StartAsync("REFUSED_REGISTRATION_TEST", 5, default,
                requester: "Networking Radar", chat: "Registration", idempotencyKey: "radar:refused:organizer-event");
            T.Eq("rejected registration status is failed", "failed", S(refused, "status"));
            T.Eq("rejected registration outcome is failed", "failed", S(refused, "outcome"));
            T.Contains("rejected registration reports organizer reason", S(refused, "summary"), "Organizer rejected");
            try { Directory.Delete(dir, true); } catch { }
        });
        await T.Section("MCP: external idempotency keys", async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "openfill_idempotency_" + Guid.NewGuid().ToString("N")[..8]);
            var host = new FakeRunHost { Max = 3 };
            var m = new McpTaskManager(host, dir, TimeSpan.FromSeconds(5));
            var key = "networking-radar:user7:event42";
            var first = await m.StartAsync("Register user7 for event42", 5, default, requester: "Networking Radar", chat: "Registrations", idempotencyKey: key);
            T.Eq("first event registration finishes", "done", S(first, "status"));
            T.Eq("event key exposed", key, S(first, "idempotency_key"));
            var again = await m.StartAsync("Register user7 for event42", 0, default, requester: "Networking Radar", chat: "Registrations", idempotencyKey: key);
            T.Eq("repeated key returns same task", S(first, "task_id"), S(again, "task_id"));
            T.Eq("repeat did not start another run", 1, host.Started);
            var changed = await m.StartAsync("Register user7 for DIFFERENT event", 0, default, requester: "Networking Radar", chat: "Registrations", idempotencyKey: key);
            T.Eq("key collision rejected", "idempotency_conflict", S(changed, "status"));
            T.Eq("collision does not start task", 1, host.Started);
            var wrongKey = await m.StartAsync("Register user7 for event42", 0, default, requester: "Networking Radar", chat: "Registrations", idempotencyKey: "../bad");
            T.Eq("unsafe key rejected", "error", S(wrongKey, "status"));
            var reloaded = new McpTaskManager(new FakeRunHost(), dir, TimeSpan.FromSeconds(5));
            var afterRestart = await reloaded.StartAsync("Register user7 for event42", 0, default, requester: "Networking Radar", chat: "Registrations", idempotencyKey: key);
            T.Eq("key survives OpenFill restart", S(first, "task_id"), S(afterRestart, "task_id"));
            var other = await reloaded.StartAsync("Register user7 for event42", 5, default, requester: "Separate app", chat: "Registrations", idempotencyKey: key);
            T.Check("key is scoped to caller", S(other, "task_id") != S(first, "task_id"));
            try { Directory.Delete(dir, true); } catch { }
        });
        await T.Section("OF-219: failed orphan persistence must not duplicate external task", async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "openfill_idempotency_iofail_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(dir);
            var id = "OF-20261018-2045-abcd";
            var path = Path.Combine(dir, id + ".json");
            var rec = new McpTaskRecord
            {
                Id = id, TaskText = "REGISTER_ONE_EVENT_FIXTURE", Status = "running",
                Requester = "Networking Radar · Registrations", IdempotencyKey = "radar:registration:test1",
                CreatedUtc = DateTime.UtcNow.AddMinutes(-3),
                UpdatedUtc = DateTime.UtcNow.AddMinutes(-2)
            };
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(rec));
            var host = new FakeRunHost();
            var recovered = new McpTaskManager(host, dir, TimeSpan.FromSeconds(2));
            File.SetAttributes(path, FileAttributes.ReadOnly);
            try
            {
                var blocked = false;
                try
                {
                    await recovered.StartAsync(rec.TaskText, 0, default, requester: "Networking Radar",
                        chat: "Registrations", idempotencyKey: rec.IdempotencyKey);
                }
                catch (IOException) { blocked = true; }
                catch (UnauthorizedAccessException) { blocked = true; }
                T.Check("orphan recovery persistence failure surfaces to caller", blocked);
                T.Eq("failure did not start a second registration", 0, host.Started);
                T.Eq("original task file was not overwritten", "running",
                    System.Text.Json.JsonSerializer.Deserialize<McpTaskRecord>(File.ReadAllText(path))!.Status);
                File.SetAttributes(path, FileAttributes.Normal);
                var replay = await recovered.StartAsync(rec.TaskText, 0, default, requester: "Networking Radar",
                    chat: "Registrations", idempotencyKey: rec.IdempotencyKey);
                T.Eq("recovered request has original task ID", id, S(replay, "task_id"));
                T.Eq("recovered request status interrupted", "interrupted", S(replay, "status"));
                T.Eq("recovered outcome marks external effects uncertain", "partial", S(replay, "outcome"));
                T.Eq("no duplicate after file restored", 0, host.Started);
                T.Eq("recovery persisted after write became possible", "interrupted",
                    System.Text.Json.JsonSerializer.Deserialize<McpTaskRecord>(File.ReadAllText(path))!.Status);
            }
            finally
            {
                if (File.Exists(path)) File.SetAttributes(path, FileAttributes.Normal);
                try { Directory.Delete(dir, true); } catch { }
            }
        });
        await T.Section("OF-219: locked task record fails closed rather than duplicate registration", async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "openfill_key_locked_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(dir);
            var id = "OF-20261018-2120-ccdd";
            var path = Path.Combine(dir, id + ".json");
            var rec = new McpTaskRecord
            {
                Id = id, TaskText = "REGISTER_LOCKED_HISTORY_TEST", Status = "running",
                Requester = "Networking Radar · Registrations", IdempotencyKey = "radar:locked:registration",
                CreatedUtc = DateTime.UtcNow.AddMinutes(-4), UpdatedUtc = DateTime.UtcNow.AddMinutes(-3)
            };
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(rec));
            var host = new FakeRunHost();
            var manager = new McpTaskManager(host, dir, TimeSpan.FromSeconds(2));
            try
            {
                var denied = false;
                var message = "";
                using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    try
                    {
                        await manager.StartAsync(rec.TaskText, 0, default, requester: "Networking Radar",
                            chat: "Registrations", idempotencyKey: rec.IdempotencyKey);
                    }
                    catch (IOException ex) { denied = true; message = ex.Message; }
                    T.Check("locked history prevents new start", denied);
                    T.Contains("locked history explains refusal", message, "no new task started");
                    T.Eq("locked record did not trigger a new registration", 0, host.Started);
                }
                T.Eq("locked file remains original running task", "running",
                    System.Text.Json.JsonSerializer.Deserialize<McpTaskRecord>(File.ReadAllText(path))!.Status);
                var restored = await manager.StartAsync(rec.TaskText, 0, default, requester: "Networking Radar",
                    chat: "Registrations", idempotencyKey: rec.IdempotencyKey);
                T.Eq("after unlock: original task id preserved", id, S(restored, "task_id"));
                T.Eq("after unlock: interrupted instead of duplicate", "interrupted", S(restored, "status"));
                T.Eq("after unlock: result is partial/unknown", "partial", S(restored, "outcome"));
                T.Eq("after unlock: still zero duplicate actions", 0, host.Started);
            }
            finally { try { Directory.Delete(dir, true); } catch { } }
        });
        await T.Section("MCP: close persists interrupted and notifies polling clients", async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "openfill_shutdown_" + Guid.NewGuid().ToString("N")[..6]);
            var host = new FakeRunHost();
            var m = new McpTaskManager(host, dir, TimeSpan.FromSeconds(30));
            var pending = await m.StartAsync("SLOW test task", 0, default, requester: "Test", chat: "Shutdown");
            T.Eq("shutdown: task initially running", "running", S(pending, "status"));
            var count = m.PrepareShutdown("OpenFill scheduled restart");
            T.Eq("shutdown: one pending task marked", 1, count);
            var interrupted = await m.StatusAsync(S(pending, "task_id"), 0, default);
            T.Eq("shutdown: client sees interrupted", "interrupted", S(interrupted, "status"));
            T.Contains("shutdown: retry advice", S(interrupted, "summary"), "continue_task_id=");
            await Task.Delay(50);
            var restored = new McpTaskManager(new FakeRunHost(), dir, TimeSpan.FromSeconds(5));
            var persisted = await restored.StatusAsync(S(pending, "task_id"), 0, default);
            T.Eq("shutdown: interruption survives restart", "interrupted", S(persisted, "status"));
            try { Directory.Delete(dir, true); } catch { }
        });
        await T.Section("MCP: maintenance refuses unsafe shutdown if task history cannot persist", async () =>
        {
            var root = Path.Combine(Path.GetTempPath(), "openfill_shutdown_writefail_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(root);
            var invalidTasksDir = Path.Combine(root, "tasks-path-is-a-file");
            File.WriteAllText(invalidTasksDir, "fixture: directory creation must fail");
            var host = new FakeRunHost();
            var m = new McpTaskManager(host, invalidTasksDir, TimeSpan.FromSeconds(2));
            var active = await m.StartAsync("SLOW job before maintenance", 0, default, requester: "Test", chat: "Persist-failure");
            T.Eq("writefail: active job initially running", "running", S(active, "status"));
            var refused = false;
            try { m.PrepareShutdown("maintenance failure test", 10); }
            catch (IOException) { refused = true; }
            T.Check("writefail: failure to persist is reported", refused);
            T.Check("writefail: task is not stopped without durable record", !host.Stopped.Contains(S(active, "task_id")));
            var afterFailure = await m.StatusAsync(S(active, "task_id"), 0, default);
            T.Eq("writefail: task remains running in memory", "running", S(afterFailure, "status"));
            try { Directory.Delete(root, true); } catch { }
        });        await T.Section("MCP: orphaned task after unexpected restart is interrupted, not retried", async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "openfill_crash_restore_" + Guid.NewGuid().ToString("N")[..8]);
            var host = new FakeRunHost();
            var original = new McpTaskManager(host, dir, TimeSpan.FromSeconds(5));
            const string key = "unique-crash-recovery-fixture";
            var live = await original.StartAsync("SLOW external operation, unconfirmed result", 0, default,
                requester: "CrashTest", chat: "Recovery", idempotencyKey: key);
            T.Eq("crash: task originally running", "running", S(live, "status"));
            var afterRestartHost = new FakeRunHost();
            var afterRestart = new McpTaskManager(afterRestartHost, dir, TimeSpan.FromSeconds(5));
            var recovered = await afterRestart.StatusAsync(S(live, "task_id"), 0, default);
            T.Eq("crash: persisted running becomes interrupted", "interrupted", S(recovered, "status"));
            T.Eq("crash: side effects are partial or unknown", "partial", S(recovered, "outcome"));
            T.Contains("crash: do not blind retry", S(recovered, "summary"), "UNKNOWN");
            T.Contains("crash: recover using original task id", S(recovered, "summary"),
                "continue_task_id=" + S(live, "task_id"));
            var duplicate = await afterRestart.StartAsync("SLOW external operation, unconfirmed result", 0, default,
                requester: "CrashTest", chat: "Recovery", idempotencyKey: key);
            T.Eq("crash: duplicate request maps to original task", S(live, "task_id"), S(duplicate, "task_id"));
            T.Eq("crash: no external operation started again", 0, afterRestartHost.Started);
            var verifyDisk = new McpTaskManager(new FakeRunHost(), dir, TimeSpan.FromSeconds(5));
            var persisted = await verifyDisk.StatusAsync(S(live, "task_id"), 0, default);
            T.Eq("crash: interruption persisted durably", "interrupted", S(persisted, "status"));
            original.Cancel(S(live, "task_id"));
            try { Directory.Delete(dir, true); } catch { }
        });        await T.Section("OF-218: old persisted running/waiting tasks are interrupted on read", async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "openfill_orphans_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(dir);
            var states = new[] { "running", "needs_input", "waiting_for_user" };
            var ids = new List<string>();
            for (var i = 0; i < states.Length; i++)
            {
                var rec = new McpTaskRecord
                {
                    Id = "OF-20261016-010" + i + "-fe0" + i,
                    TaskText = "Simulated interrupted legacy task " + i,
                    Status = states[i],
                    Requester = "Historical client",
                    CreatedUtc = DateTime.UtcNow.AddDays(-2),
                    UpdatedUtc = DateTime.UtcNow.AddDays(-2),
                    Question = "Previous unanswered question"
                };
                ids.Add(rec.Id);
                File.WriteAllText(Path.Combine(dir, rec.Id + ".json"),
                    System.Text.Json.JsonSerializer.Serialize(rec));
            }
            var host = new FakeRunHost();
            var m = new McpTaskManager(host, dir, TimeSpan.FromSeconds(5));
            for (var i = 0; i < states.Length; i++)
            {
                var response = await m.StatusAsync(ids[i], 0, default);
                T.Eq("orphan " + states[i] + " becomes interrupted", "interrupted", S(response, "status"));
                T.Eq("orphan preserves continue_task_id", ids[i], S(response, "continue_task_id"));
                T.Contains("orphan reports uncertain external effects", S(response, "summary"), "UNKNOWN");
                var after = System.Text.Json.JsonSerializer.Deserialize<McpTaskRecord>(
                    File.ReadAllText(Path.Combine(dir, ids[i] + ".json")))!;
                T.Eq("orphan transition persisted for " + states[i], "interrupted", after.Status);
            }
            T.Eq("reading orphans did not execute tasks", 0, host.Started);
            var recent = m.Info()["recent_tasks"]!.AsArray();
            T.Check("info does not advertise any orphan as running", recent.All(item => item?["status"]?.GetValue<string>() == "interrupted"));
            try { Directory.Delete(dir, true); } catch { }
        });
        await T.Section("MCP: start waits for actual state change despite early activity", async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "openfill_startwait_" + Guid.NewGuid().ToString("N")[..8]);
            var host = new FakeRunHost();
            var m = new McpTaskManager(host, dir, TimeSpan.FromSeconds(3));
            var started = await m.StartAsync("START_WAIT_TEST", 3, default, requester: "Test", chat: "startwait");
            T.Eq("start returns final state, not activity running", "done", S(started, "status"));
            T.Eq("start was executed once", 1, host.Started);
            try { Directory.Delete(dir, true); } catch { }
        });
        await T.Section("MCP: activity wakes polling and does not invent ETA", async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "openfill_poll_" + Guid.NewGuid().ToString("N")[..8]);
            var host = new FakeRunHost();
            var m = new McpTaskManager(host, dir, TimeSpan.FromSeconds(5));
            var started = await m.StartAsync("SLOW polling", 0, default, requester: "Test", chat: "Poll");
            T.Eq("poll ceiling", 30, started["poll_wait_max_seconds"]?.GetValue<int>() ?? -1);
            T.Check("no invented ETA", started.ContainsKey("estimated_completion_seconds") && started["estimated_completion_seconds"] is null);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var pending = m.StatusAsync(S(started, "task_id"), 3, default);
            await Task.Delay(100);
            host.EmitActivity?.Invoke("Next action");
            var response = await pending;
            T.Check("poll: activity wakes before full timeout", stopwatch.ElapsedMilliseconds < 2000);
            T.Check("progress woke waiting status", response["last_tool_activity_at"] is not null);
            T.Eq("task still running", "running", S(response, "status"));
            m.Cancel(S(started, "task_id"));
        });
        await T.Section("MCP: per-recipient verified batch progress survives restart", async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "openfill_progress_" + Guid.NewGuid().ToString("N")[..8]);
            var host = new FakeRunHost { TabStatus = key => new JsonObject { ["tab_id"] = "tab-test", ["open"] = true, ["reason"] = "existing_session_reused" } };
            var m = new McpTaskManager(host, dir, TimeSpan.FromSeconds(5));
            var started = await m.StartAsync("SLOW send invitations", 0, default, requester: "Test", chat: "Progress");
            var id = S(started, "task_id");
            T.Check("steps are not confirmed invitations", started["confirmed_progress"] is null);
            T.Eq("MCP response exposes own tab id", "tab-test", started["browser_tab"]?["tab_id"]?.GetValue<string>());
            T.Eq("MCP response exposes tab reuse reason", "existing_session_reused", started["browser_tab"]?["reason"]?.GetValue<string>());
            host.EmitVerified!(new VerifiedAction("User A", 5, "Invitation sent", "https://example.test/a", DateTime.UtcNow));
            var status = await m.StatusAsync(id, 0, default);
            var progress = status["confirmed_progress"];
            T.Eq("one verified recipient", 1, progress?["confirmed"]?.GetValue<int>() ?? -1);
            T.Eq("planned five", 5, progress?["total"]?.GetValue<int>() ?? -1);
            T.Eq("four remaining", 4, progress?["remaining"]?.GetValue<int>() ?? -1);
            T.Check("progress has no invented ETA", progress?["estimated_completion_seconds"] is null);
            host.EmitVerified!(new VerifiedAction("user a", 5, "Invitation sent", "https://example.test/a", DateTime.UtcNow));
            status = await m.StatusAsync(id, 0, default);
            T.Eq("same recipient does not increment twice", 1, status["confirmed_progress"]?["confirmed"]?.GetValue<int>() ?? -1);
            var restored = new McpTaskManager(new FakeRunHost(), dir, TimeSpan.FromSeconds(5));
            var persisted = await restored.StatusAsync(id, 0, default);
            T.Eq("confirmed progress survives restart", 1, persisted["confirmed_progress"]?["confirmed"]?.GetValue<int>() ?? -1);
            T.Eq("restored task is interrupted", "interrupted", S(persisted, "status"));
            m.Cancel(id);
            var continuedHost = new FakeRunHost();
            var continued = new McpTaskManager(continuedHost, dir, TimeSpan.FromSeconds(5));
            var next = await continued.StartAsync("SLOW finish remaining invitations", 0, default,
                continueTaskId: id, requester: "Test", chat: "Progress");
            var nextId = S(next, "task_id");
            T.Eq("confirmed recipients inherited by continuation", 1, next["confirmed_progress"]?["confirmed"]?.GetValue<int>() ?? -1);
            var repeated = continuedHost.EmitVerified!(new VerifiedAction("User A", 5, "Invitation sent", "https://example.test/a", DateTime.UtcNow));
            T.Eq("same recipient on continuation stays counted once", 1, repeated);
            var second = continuedHost.EmitVerified!(new VerifiedAction("User B", 5, "Invitation sent", "https://example.test/b", DateTime.UtcNow));
            T.Eq("new recipient after continuation makes two", 2, second);
            var nextStatus = await continued.StatusAsync(nextId, 0, default);
            T.Eq("continued task reports two of five", 2, nextStatus["confirmed_progress"]?["confirmed"]?.GetValue<int>() ?? -1);
            continued.Cancel(nextId);
        });
        await T.Section("MCP: batch progress never wakes StartAsync before state transition", async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "openfill_progress_wait_" + Guid.NewGuid().ToString("N")[..8]);
            var host = new FakeRunHost();
            var m = new McpTaskManager(host, dir, TimeSpan.FromSeconds(3));
            var pending = m.StartAsync("SLOW batch with interim confirmation", 2, default, requester: "Test", chat: "ProgressWait");
            await Task.Delay(150);
            T.Check("proof callback is available", host.EmitVerified is not null);
            host.EmitVerified?.Invoke(new VerifiedAction("User A", 5, "Service confirmed", "https://example.test/a", DateTime.UtcNow));
            await Task.Delay(250);
            T.Check("StartAsync does not return running on progress-only activity", !pending.IsCompleted);
            var started = await pending;
            T.Eq("task is still running when start timeout expires", "running", S(started, "status"));
            T.Eq("interim confirmation still included", 1, started["confirmed_progress"]?["confirmed"]?.GetValue<int>() ?? -1);
            m.Cancel(S(started, "task_id"));
        });        await T.Section("MCP: controlled upgrade interrupts concurrent waiters and persists ETA", async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "openfill_upgrade_" + Guid.NewGuid().ToString("N")[..8]);
            var host = new FakeRunHost { Max = 4 };
            var m = new McpTaskManager(host, dir, TimeSpan.FromSeconds(30));
            var a = await m.StartAsync("SLOW upgrade task A", 0, default, requester: "Client-A", chat: "Chat-A");
            var b = await m.StartAsync("SLOW upgrade task B", 0, default, requester: "Client-B", chat: "Chat-B");
            var c = await m.StartAsync("ASKME upgrade task C", 0, default, requester: "Client-C", chat: "Chat-C");
            await Task.Delay(350);
            var pendingA = m.StatusAsync(S(a, "task_id"), 20, default);
            var pendingB = m.StatusAsync(S(b, "task_id"), 20, default);
            var affected = m.PrepareShutdown("maintenance for upgrade", 7);
            T.Eq("upgrade interrupts three separate clients", 3, affected);
            var late = await m.StartAsync("SLOW should not start during upgrade", 0, default, requester: "Late", chat: "Chat");
            T.Eq("upgrade blocks new tasks", "maintenance", S(late, "status"));
            T.Eq("upgrade did not start extra task", 3, host.Started);
            var resultA = await pendingA;
            var resultB = await pendingB;
            T.Eq("upgrade wakes long poll A", "interrupted", S(resultA, "status"));
            T.Eq("upgrade wakes long poll B", "interrupted", S(resultB, "status"));
            T.Contains("upgrade advertises ETA", S(resultA, "summary"), "7 minute(s)");
            T.Contains("upgrade gives precise recovery ID", S(resultA, "summary"), "continue_task_id=" + S(a, "task_id"));
            T.Contains("upgrade warns about side effects", S(resultB, "summary"), "side effects");
            T.Eq("upgrade marks partial result", "partial", S(resultA, "outcome"));
            T.Check("upgrade stopped task A", host.Stopped.Contains(S(a, "task_id")));
            T.Check("upgrade stopped task B", host.Stopped.Contains(S(b, "task_id")));
            T.Check("upgrade stopped task C", host.Stopped.Contains(S(c, "task_id")));
            var reloaded = new McpTaskManager(new FakeRunHost(), dir, TimeSpan.FromSeconds(3));
            var diskA = await reloaded.StatusAsync(S(a, "task_id"), 0, default);
            T.Eq("upgrade persisted interrupted status", "interrupted", S(diskA, "status"));
            T.Contains("upgrade persisted ETA across restart", S(diskA, "summary"), "7 minute(s)");
            T.Eq("upgrade kept original task ID", S(a, "task_id"), S(diskA, "task_id"));
            try { Directory.Delete(dir, true); } catch { }
        });
        await T.Section("MCP: task life cycle", async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "openfill_mcp_" + Guid.NewGuid().ToString("N")[..6]);
            var host = new FakeRunHost();
            var m = new McpTaskManager(host, dir, TimeSpan.FromSeconds(30));

            var noWho = await m.StartAsync("Fill the Xing profile with the name Jan", 5, default);
            T.Eq("no requester and no chat: refused", "needs_identity", noWho["status"]?.GetValue<string>());
            var noChat = await m.StartAsync("Fill the Xing profile with the name Jan", 5, default, requester: "Claude Bishop");
            T.Eq("requester without chat: refused", "needs_identity", noChat["status"]?.GetValue<string>());
            var r1 = await m.StartAsync("Fill the Xing profile with the name Jan", 5, default, requester: "Test", chat: "Chat");
            T.Eq("simple task finishes", "done", S(r1, "status"));
            T.Check("task id carries the date", S(r1, "task_id").StartsWith("OF-" + DateTime.Now.ToString("yyyyMMdd")));
            T.Contains("summary returned", S(r1, "summary"), "done: Fill the Xing profile");
            T.Check("task saved to disk", File.Exists(Path.Combine(dir, S(r1, "task_id") + ".json")));

            var r2 = await m.StartAsync("ASKME about the booking date", 5, default, requester: "Test", chat: "Chat");
            T.Eq("question reaches the caller", "needs_input", S(r2, "status"));
            T.Eq("question text", "Which date?", S(r2, "question"));
            T.Eq("options listed", 2, r2["options"]?.AsArray().Count);
            var r2b = await m.ReplyAsync(S(r2, "task_id"), "June", 5, default);
            T.Eq("task finishes after the answer", "done", S(r2b, "status"));
            T.Contains("answer reached the inner model", S(r2b, "summary"), "answered: June");

            var r3 = await m.StartAsync("SLOW comparison of flight offers to Lisbon", 0, default, requester: "Test", chat: "Chat");
            T.Eq("long task is running", "running", S(r3, "status"));
            var busy = await m.StartAsync("Completely different job: update the LinkedIn headline", 0, default, requester: "Test", chat: "Chat");
            T.Eq("second task is refused", "busy", S(busy, "status"));
            T.Eq("busy names the open task", S(r3, "task_id"), S(busy, "open_task_id"));
            var same = await m.StartAsync("SLOW comparison of flight offers to Lisbon", 0, default, requester: "Test", chat: "Chat");
            T.Eq("same content returns the open task", S(r3, "task_id"), S(same, "task_id"));
            T.Eq("only one run was started for it", 3, host.Started);
            var st = await m.StatusAsync(null, 1, default);
            T.Eq("status without id finds the open task", "running", S(st, "status"));
            var cancelled = m.Cancel(S(r3, "task_id"));
            T.Eq("cancel", "cancelled", S(cancelled, "status"));
            await Task.Delay(300);

            var r4 = await m.StartAsync("A fresh job after the cancel", 5, default, requester: "Test", chat: "Chat");
            T.Eq("new task allowed after cancel", "done", S(r4, "status"));

            var hist = await m.StatusAsync(S(r1, "task_id"), 0, default);
            T.Eq("older task is read from disk", "done", S(hist, "status"));
            var nf = await m.StatusAsync("OF-nonexistent", 0, default);
            T.Eq("unknown id", "not_found", S(nf, "status"));
            var nothing = await m.ReplyAsync(S(r4, "task_id"), "x", 0, default);
            T.Contains("reply without a question explains", S(nothing, "note"), "No question");

            // A stop by the person at the window is final: no hint to continue, an explicit ban on restarting.
            var us = await m.StartAsync("USERSTOP job on some page", 5, default, requester: "Test", chat: "Chat");
            T.Eq("stop by the person has its own status", "stopped_by_user", S(us, "status"));
            var usNext = S(us, "next_step");
            T.Contains("do not restart", usNext, "Do NOT start it again");
            T.Check("no hint to continue it", !usNext.Contains("start a new task with continue_task_id=", StringComparison.OrdinalIgnoreCase));
            T.Contains("a new task on the same site stays allowed", usNext, "a new, different task");

            // A question only the user can answer: status waiting_for_user, the caller is told not to send the code.
            var h1 = await m.StartAsync("NEEDCODE log in to the site", 5, default, requester: "Test", chat: "Chat");
            T.Eq("waiting for the user", "waiting_for_user", S(h1, "status"));
            T.Contains("the question is shown", S(h1, "question"), "Verification code");
            T.Contains("caller is told to wait for the person in the OpenFill window", S(h1, "next_step"), "interactive authentication step");
            var h2 = await m.ReplyAsync(S(h1, "task_id"), "123456", 0, default);
            T.Contains("reply is refused for a user-only question", S(h2, "note"), "only be answered by the user");
            T.Eq("still waiting after the refused reply", "waiting_for_user", S(h2, "status"));
            var h3 = await m.StatusAsync(S(h1, "task_id"), 10, default);
            for (var i = 0; i < 5 && S(h3, "status") != "done"; i++) h3 = await m.StatusAsync(S(h1, "task_id"), 10, default);
            T.Eq("finishes when the user has entered it", "done", S(h3, "status"));
            T.Contains("summary after the code", S(h3, "summary"), "code entered");
        });

        await T.Section("MCP: concurrent HTTP waiting clients receive upgrade notices", async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "openfill_upgrade_http_" + Guid.NewGuid().ToString("N")[..8]);
            var host = new FakeRunHost { Max = 3 };
            var mgr = new McpTaskManager(host, dir, TimeSpan.FromSeconds(10));
            const string secret = "local-upgrade-test-secret";
            await using var server = new McpServer(mgr, secret, 0, null);
            server.Start();
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var url = $"http://127.0.0.1:{server.Port}/mcp/{secret}";
            var a = await mgr.StartAsync("SLOW task for client one", 0, default, requester: "A", chat: "One");
            var b = await mgr.StartAsync("SLOW task for client two", 0, default, requester: "B", chat: "Two");
            async Task<JsonObject> LongPoll(string taskId, int rpcId)
            {
                var body = new JsonObject
                {
                    ["jsonrpc"] = "2.0", ["id"] = rpcId, ["method"] = "tools/call",
                    ["params"] = new JsonObject
                    {
                        ["name"] = "openfill_task_status",
                        ["arguments"] = new JsonObject { ["task_id"] = taskId, ["wait_seconds"] = 8 }
                    }
                };
                using var response = await http.PostAsync(url, new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"));
                response.EnsureSuccessStatusCode();
                var rpc = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
                return JsonNode.Parse(rpc["result"]!["content"]![0]!["text"]!.GetValue<string>())!.AsObject();
            }
            var first = LongPoll(S(a, "task_id"), 71);
            var second = LongPoll(S(b, "task_id"), 72);
            await Task.Delay(250);
            var interrupted = mgr.PrepareShutdown("upgrade authorized by local operator", 12);
            T.Eq("HTTP upgrade interrupted both models", 2, interrupted);
            var answers = await Task.WhenAll(first, second);
            T.Eq("HTTP client one receives interrupted", "interrupted", S(answers[0], "status"));
            T.Eq("HTTP client two receives interrupted", "interrupted", S(answers[1], "status"));
            T.Contains("HTTP client one receives downtime", S(answers[0], "summary"), "12 minute(s)");
            T.Contains("HTTP client two receives recovery instructions", S(answers[1], "next_step"), "continue_task_id=");
        });
        await T.Section("MCP: HTTP server", async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "openfill_mcp_" + Guid.NewGuid().ToString("N")[..6]);
            const string secret = "test-secret-0123456789abcdef";
            var shots = Path.Combine(dir, "shots");
            var m = new McpTaskManager(new FakeRunHost { ShotsDir = shots }, dir, TimeSpan.FromSeconds(30));
            await using var server = new McpServer(m, secret, 0, null, shots);
            server.Start();
            using var http = new HttpClient();
            var url = $"http://127.0.0.1:{server.Port}/mcp/{secret}";

            async Task<JsonObject> Rpc(string json)
            {
                using var resp = await http.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));
                return JsonNode.Parse(await resp.Content.ReadAsStringAsync())!.AsObject();
            }

            var init = await Rpc("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"t","version":"1"}}}""");
            T.Eq("protocol version echoed", "2025-11-25", init["result"]?["protocolVersion"]?.GetValue<string>());
            T.Check("tools capability", init["result"]?["capabilities"]?["tools"] is not null);

            var list = await Rpc("""{"jsonrpc":"2.0","id":2,"method":"tools/list"}""");
            var tools = list["result"]!["tools"]!.AsArray();
            var names = tools.Select(t => t!["name"]!.GetValue<string>()).ToList();
            T.Check("eight tools", names.Count == 8 && names.Contains("openfill_info") && names.Contains("openfill_start_task") && names.Contains("openfill_reply") && names.Contains("openfill_screenshot") && names.Contains("openfill_file_transfer"));
            T.Check("every schema is strict and has no $schema", tools.All(t =>
                t!["inputSchema"]!["additionalProperties"]!.GetValue<bool>() == false && t["inputSchema"]!["$schema"] is null));
            T.Check("every tool has annotations", tools.All(t => t!["annotations"]?["readOnlyHint"] is not null));
            T.Check("initialize reports the server version", init["result"]?["serverInfo"]?["version"]?.GetValue<string>() == OpenFill.Core.BuildInfo.Version);
            var ver = await Rpc("""{"jsonrpc":"2.0","id":6,"method":"tools/call","params":{"name":"openfill_info","arguments":{}}}""");
            var verBody = System.Text.Json.Nodes.JsonNode.Parse(ver["result"]!["content"]![0]!["text"]!.GetValue<string>())!.AsObject();
            T.Eq("info tool ok", "ok", S(verBody, "status"));
            T.Eq("info tool reports the version", OpenFill.Core.BuildInfo.Version, verBody["app"]!["version"]!.GetValue<string>());
            T.Check("info tool has runtime and recent tasks", verBody["runtime"] is not null && verBody["recent_tasks"] is not null);

            var call = await Rpc("""{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"openfill_start_task","arguments":{"task":"Simple http test task","wait_seconds":5,"requester":"Claude Test","chat":"Test chat"}}}""");
            var payload = JsonNode.Parse(call["result"]!["content"]![0]!["text"]!.GetValue<string>())!.AsObject();
            T.Eq("tools/call runs a task", "done", S(payload, "status"));
            T.Eq("isError false", false, call["result"]!["isError"]!.GetValue<bool>());

            var shot = await Rpc("""{"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"openfill_screenshot","arguments":{}}}""");
            var shotPayload = JsonNode.Parse(shot["result"]!["content"]![0]!["text"]!.GetValue<string>())!.AsObject();
            T.Eq("screenshot ok", "ok", S(shotPayload, "status"));
            var shotImage = shot["result"]!["content"]!.AsArray().FirstOrDefault(c => c?["type"]?.GetValue<string>() == "image");
            T.Check("screenshot comes back as an image the model can look at", shotImage is not null && shotImage["mimeType"]?.GetValue<string>() == "image/jpeg" && (shotImage["data"]?.GetValue<string>() ?? "").Length > 0);
            T.Check("screenshot image is not repeated in the text", !shot["result"]!["content"]![0]!["text"]!.GetValue<string>().Contains("_image"));
            var shotUrl = S(shotPayload, "url");
            T.Check("screenshot link is under the secret path", shotUrl.StartsWith(url + "/shots/shot-test-") && shotUrl.EndsWith(".jpg"));
            using (var img = await http.GetAsync(shotUrl))
            {
                T.Eq("screenshot link downloads", 200, (int)img.StatusCode);
                T.Eq("screenshot content type", "image/jpeg", img.Content.Headers.ContentType?.MediaType);
            }
            using (var badShot = await http.GetAsync(url + "/shots/..%2Fmcp-secret.jpg"))
                T.Eq("screenshot path tricks are 404", 404, (int)badShot.StatusCode);
            using (var noSecret = await http.GetAsync($"http://127.0.0.1:{server.Port}/mcp/wrong/shots/" + shotUrl[(shotUrl.LastIndexOf('/') + 1)..]))
                T.Eq("screenshot link needs the secret", 404, (int)noSecret.StatusCode);

            var bad = await Rpc("""{"jsonrpc":"2.0","id":4,"method":"nope"}""");
            T.Eq("unknown method", -32601, bad["error"]!["code"]!.GetValue<int>());

            using (var wrong = await http.PostAsync($"http://127.0.0.1:{server.Port}/mcp/wrong", new StringContent("{}", Encoding.UTF8, "application/json")))
                T.Eq("wrong secret is 404", 404, (int)wrong.StatusCode);
            using (var get = await http.GetAsync(url))
                T.Eq("GET is 405", 405, (int)get.StatusCode);
            using (var note = await http.PostAsync(url, new StringContent("""{"jsonrpc":"2.0","method":"notifications/initialized"}""", Encoding.UTF8, "application/json")))
                T.Eq("notification is 202", 202, (int)note.StatusCode);
        });
    }
}

/// <summary>Answers "Continue" to the first question and "Stop" to every later one; remembers the questions.</summary>
public sealed class ContinueOnceInteraction : IUserInteraction
{
    public List<string> Questions { get; } = new();
    public Task<string> AskAsync(string question, IReadOnlyList<string>? options, CancellationToken ct)
    {
        Questions.Add(question);
        return Task.FromResult(Questions.Count == 1 ? "Continue" : "Stop");
    }
    public Task<bool> ConfirmAsync(string what, CancellationToken ct) => Task.FromResult(false);
}

/// <summary>Records which kind of question reached the person: plain ask_user or user-only (verification code).</summary>
public sealed class RecordingInteraction : IUserInteraction
{
    public List<string> Plain { get; } = new();
    public List<string> HumanOnly { get; } = new();
    public Task<string> AskAsync(string question, IReadOnlyList<string>? options, CancellationToken ct) { Plain.Add(question); return Task.FromResult("plain-answer"); }
    public Task<string> AskHumanAsync(string question, IReadOnlyList<string>? options, CancellationToken ct) { HumanOnly.Add(question); return Task.FromResult("481516"); }
    public Task<bool> ConfirmAsync(string what, CancellationToken ct) => Task.FromResult(false);
}