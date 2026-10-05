// OpenFill - Metadata: wersja 0.5, data 2026-10-05 21:31
using System.Text;
using System.Text.Json.Nodes;
using OpenFill.Core.Agent;
using OpenFill.Core.Mcp;

namespace OpenFill.Tests;

/// <summary>Run host without a browser or a model: scripted by words in the task text.</summary>
internal sealed class FakeRunHost : IRunHost
{
    public AskHandler? AskInterceptor { get; set; }
    public event Action<string>? Activity;
    public event Action<string?>? UserWaiting;
    private CancellationTokenSource? _cts;
    public int Started;

    public Task<RunConclusion>? StartRun(string task)
    {
        Started++;
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        return Task.Run(async () =>
        {
            Activity?.Invoke("act of3 type");
            await Task.Delay(150);
            if (task.Contains("ASKME"))
            {
                var ans = await AskInterceptor!("Which date?", new[] { "May", "June" }, ct);
                return new RunConclusion("success", "answered: " + ans, null);
            }
            if (task.Contains("NEEDCODE"))
            {
                UserWaiting?.Invoke("Verification code from the e-mail?");
                await Task.Delay(1500);
                UserWaiting?.Invoke(null);
                return new RunConclusion("success", "code entered by the user", null);
            }
            if (task.Contains("SLOW"))
            {
                try { await Task.Delay(30000, ct); }
                catch (OperationCanceledException) { return new RunConclusion("failed", "stopped", null); }
            }
            return new RunConclusion("success", "done: " + task, null);
        });
    }

    public void Stop() => _cts?.Cancel();

    /// <summary>Directory where the fake "browser" saves its screenshots (null = cannot take one).</summary>
    public string? ShotsDir { get; set; }

    public Task<string?> CaptureScreenshotAsync(CancellationToken ct)
    {
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
        await T.Section("MCP: task life cycle", async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "openfill_mcp_" + Guid.NewGuid().ToString("N")[..6]);
            var host = new FakeRunHost();
            var m = new McpTaskManager(host, dir, TimeSpan.FromSeconds(30));

            var r1 = await m.StartAsync("Fill the Xing profile with the name Jan", 5, default);
            T.Eq("simple task finishes", "done", S(r1, "status"));
            T.Check("task id carries the date", S(r1, "task_id").StartsWith("OF-" + DateTime.Now.ToString("yyyyMMdd")));
            T.Contains("summary returned", S(r1, "summary"), "done: Fill the Xing profile");
            T.Check("task saved to disk", File.Exists(Path.Combine(dir, S(r1, "task_id") + ".json")));
            T.Check("interceptor cleared after the task", host.AskInterceptor is null);

            var r2 = await m.StartAsync("ASKME about the booking date", 5, default);
            T.Eq("question reaches the caller", "needs_input", S(r2, "status"));
            T.Eq("question text", "Which date?", S(r2, "question"));
            T.Eq("options listed", 2, r2["options"]?.AsArray().Count);
            var r2b = await m.ReplyAsync(S(r2, "task_id"), "June", 5, default);
            T.Eq("task finishes after the answer", "done", S(r2b, "status"));
            T.Contains("answer reached the inner model", S(r2b, "summary"), "answered: June");

            var r3 = await m.StartAsync("SLOW comparison of flight offers to Lisbon", 0, default);
            T.Eq("long task is running", "running", S(r3, "status"));
            var busy = await m.StartAsync("Completely different job: update the LinkedIn headline", 0, default);
            T.Eq("second task is refused", "busy", S(busy, "status"));
            T.Eq("busy names the open task", S(r3, "task_id"), S(busy, "open_task_id"));
            var same = await m.StartAsync("SLOW comparison of flight offers to Lisbon", 0, default);
            T.Eq("same content returns the open task", S(r3, "task_id"), S(same, "task_id"));
            T.Eq("only one run was started for it", 3, host.Started);
            var st = await m.StatusAsync(null, 1, default);
            T.Eq("status without id finds the open task", "running", S(st, "status"));
            var cancelled = m.Cancel(S(r3, "task_id"));
            T.Eq("cancel", "cancelled", S(cancelled, "status"));
            await Task.Delay(300);

            var r4 = await m.StartAsync("A fresh job after the cancel", 5, default);
            T.Eq("new task allowed after cancel", "done", S(r4, "status"));

            var hist = await m.StatusAsync(S(r1, "task_id"), 0, default);
            T.Eq("older task is read from disk", "done", S(hist, "status"));
            var nf = await m.StatusAsync("OF-nonexistent", 0, default);
            T.Eq("unknown id", "not_found", S(nf, "status"));
            var nothing = await m.ReplyAsync(S(r4, "task_id"), "x", 0, default);
            T.Contains("reply without a question explains", S(nothing, "note"), "No question");

            // A question only the user can answer: status waiting_for_user, the caller is told not to send the code.
            var h1 = await m.StartAsync("NEEDCODE log in to the site", 5, default);
            T.Eq("waiting for the user", "waiting_for_user", S(h1, "status"));
            T.Contains("the question is shown", S(h1, "question"), "Verification code");
            T.Contains("caller is told to show the code, not send it", S(h1, "next_step"), "Do NOT send it to OpenFill");
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
            T.Check("five tools", names.Count == 6 && names.Contains("openfill_info") && names.Contains("openfill_start_task") && names.Contains("openfill_reply") && names.Contains("openfill_screenshot"));
            T.Check("every schema is strict and has no $schema", tools.All(t =>
                t!["inputSchema"]!["additionalProperties"]!.GetValue<bool>() == false && t["inputSchema"]!["$schema"] is null));
            T.Check("every tool has annotations", tools.All(t => t!["annotations"]?["readOnlyHint"] is not null));
            T.Check("initialize reports the server version", init["result"]?["serverInfo"]?["version"]?.GetValue<string>() == OpenFill.Core.BuildInfo.Version);
            var ver = await Rpc("""{"jsonrpc":"2.0","id":6,"method":"tools/call","params":{"name":"openfill_info","arguments":{}}}""");
            var verBody = System.Text.Json.Nodes.JsonNode.Parse(ver["result"]!["content"]![0]!["text"]!.GetValue<string>())!.AsObject();
            T.Eq("info tool ok", "ok", S(verBody, "status"));
            T.Eq("info tool reports the version", OpenFill.Core.BuildInfo.Version, verBody["app"]!["version"]!.GetValue<string>());
            T.Check("info tool has runtime and recent tasks", verBody["runtime"] is not null && verBody["recent_tasks"] is not null);

            var call = await Rpc("""{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"openfill_start_task","arguments":{"task":"Simple http test task","wait_seconds":5}}}""");
            var payload = JsonNode.Parse(call["result"]!["content"]![0]!["text"]!.GetValue<string>())!.AsObject();
            T.Eq("tools/call runs a task", "done", S(payload, "status"));
            T.Eq("isError false", false, call["result"]!["isError"]!.GetValue<bool>());

            var shot = await Rpc("""{"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"openfill_screenshot","arguments":{}}}""");
            var shotPayload = JsonNode.Parse(shot["result"]!["content"]![0]!["text"]!.GetValue<string>())!.AsObject();
            T.Eq("screenshot ok", "ok", S(shotPayload, "status"));
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