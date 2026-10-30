from pathlib import Path
import shutil
r=Path(r"C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill")
p=r/"tests/OpenFill.Tests/McpTests__v0.17__2026-10-28_2216.cs"
s=p.read_text(encoding="utf-8-sig")
if "DIRECT_CAPTURE_TEST_20261030" in s:
    print("ALREADY")
    raise SystemExit(0)
shutil.copy2(p,r/"backups/source_capture_20261030/McpTests__v0.17__2026-10-28_2216.cs")
old='''    public void Stop(string? taskKey)
    {'''
new='''    // DIRECT_CAPTURE_TEST_20261030: deterministic no-model response.
    public string? LastCapturedTask;
    public Task<JsonObject> CapturePageAsync(string taskKey, string? expectedUrl, string mode,
        string selector, string regex, string? directory, int maxMatches, CancellationToken ct)
    {
        LastCapturedTask = taskKey;
        return Task.FromResult(new JsonObject { ["status"] = "saved", ["mode"] = mode,
            ["selector"] = selector, ["files"] = new JsonArray(new JsonObject { ["path"] = "C:/test/capture.txt", ["bytes"] = 21 }) });
    }

    public void Stop(string? taskKey)
    {'''
if s.count(old)!=1:raise RuntimeError('missing fake method anchor')
s=s.replace(old,new,1)
anchor='''        await T.Section("MCP: short status exposes only who and site", async () =>'''
suite='''        await T.Section("MCP model-free page snapshot: ownership, no Luna, real HTTP", async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "openfill_direct_capture_" + Guid.NewGuid().ToString("N")[..8]);
            var host = new FakeRunHost();
            var manager = new McpTaskManager(host, dir, TimeSpan.FromSeconds(5));
            var started = await manager.StartAsync("ordinary test", 5, default, requester: "ChatGPT", chat: "QA");
            var id = S(started, "task_id");
            var missing = await manager.PageSnapshotAsync(null, "ChatGPT", "QA", null, "text", "body", "", null, 50, default);
            T.Eq("MCP requires physical tab id", "error", S(missing,"status"));
            var denied = await manager.PageSnapshotAsync(id, "Claude", "QA", null, "text", "body", "", null, 50, default);
            T.Eq("MCP refuses foreign requester/chat", "error", S(denied,"status"));
            T.Check("MCP foreign requester never accesses browser", host.LastCapturedTask is null);
            var good = await manager.PageSnapshotAsync(id, "ChatGPT", "QA", null, "text", "main", "speaker", null, 50, default);
            T.Eq("MCP own tab capture succeeds", "saved", S(good,"status"));
            T.Eq("MCP passes exact tab identity", id, host.LastCapturedTask);
            T.Contains("MCP tool is advertised", McpServer.ToolList().ToJsonString(), "openfill_page_snapshot");
            const string secret = "source-direct-capture-test";
            await using var server = new McpServer(manager, secret, 0);
            server.Start();
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var requestBody = new JsonObject {
                ["jsonrpc"]="2.0",["id"]=99,["method"]="tools/call",
                ["params"]=new JsonObject {
                    ["name"]="openfill_page_snapshot",
                    ["arguments"]=new JsonObject { ["task_id"]=id,["requester"]="ChatGPT",["chat"]="QA",["mode"]="elements" }
                }
            };
            using var response = await http.PostAsync("http://127.0.0.1:" + server.Port + "/mcp/" + secret,
                new System.Net.Http.StringContent(requestBody.ToJsonString(), System.Text.Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();
            var rpc = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
            var body = JsonNode.Parse(rpc["result"]!["content"]![0]!["text"]!.GetValue<string>())!.AsObject();
            T.Eq("MCP HTTP page capture JSON status", "saved", S(body,"status"));
            T.Eq("MCP HTTP page capture mode", "elements", S(body,"mode"));
            try { Directory.Delete(dir,true); } catch { }
        });
        await T.Section("MCP: short status exposes only who and site", async () =>'''
if s.count(anchor)!=1:raise RuntimeError('missing test start')
s=s.replace(anchor,suite,1)
p.write_text(s,encoding="utf-8")
print("MCP_DIRECT_TEST_PATCHED")
