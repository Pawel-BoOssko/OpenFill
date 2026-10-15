// OpenFill - Metadata: wersja 0.12, data 2026-10-14 22:35
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
    public int Max { get; set; } = 1;
    public int MaxParallel => Max;
    /// <summary>When set and it returns a refusal for the task text, the task is not started (like a site that is in use).</summary>
    public Func<string, RunRefusal?>? Refuse { get; set; }
    public readonly List<string?> Stopped = new();

    public Task<RunConclusion>? StartRun(string task) => StartRun(task, null);

    public Task<RunConclusion>? StartRun(string task, RunOptions? options)
    {
        if (Refuse?.Invoke(task) is not null) return null;
        Started++;
        var cts = new CancellationTokenSource();
        lock (_cts) _cts[options?.TaskKey ?? "-"] = cts;
        var ct = cts.Token;
        return Task.Run(async () =>
        {
            options?.Activity?.Invoke("act of3 type");
            await Task.Delay(150);
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
        });        await T.Section("MCP: task life cycle", async () =>
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