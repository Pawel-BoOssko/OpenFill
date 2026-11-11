using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using OpenFill.Core.Agent;
using OpenFill.Core.Mcp;

public static class SelfChatHttpProtocolSmoke
{
    private sealed class FakeHost : IRunHost
    {
        public int Runs;
        public int DirectTitleReads;
        public bool RealMarkerPresent = true;
        public bool CorrectVerifiedUrl = true;
        public Task<JsonObject> ReadSelfChatTitleAsync(string platform, string nativeChatId, CancellationToken ct, string? expectedFallbackMarker = null)
        {
            DirectTitleReads++;
            return Task.FromResult(new JsonObject {
                ["status"]="ok", ["platform"]=platform, ["native_chat_id"]=nativeChatId,
                ["verified_url"]=CorrectVerifiedUrl ? "https://chatgpt.com/c/" + nativeChatId : "https://chatgpt.com/c/11111111-1111-1111-1111-111111111111",
                ["name"]="Nic nie rób", ["browser_model_calls"]=0, ["search_calls"]=0,
                ["fallback_visible"]=expectedFallbackMarker is null ? null : (RealMarkerPresent && expectedFallbackMarker==Fallback)
            });
        }
        public string LastTask = "";
        public const string Fallback = "OFSCWDWQHHA9LDYSLJ95QIRME87Q";
        public const string NativeId = "8fae14dc-2a64-4afd-a0b8-63b852cc03b8";

        public Task<RunConclusion>? StartRun(string task)
        {
            Runs++;
            LastTask = task;
            var proof = "OPENFILL_SELF_CHAT_RESULT: " + new JsonObject {
                ["status"]="ok", ["platform"]="chatgpt",
                ["native_chat_id"]=NativeId, ["verified_url"]="https://chatgpt.com/c/" + NativeId,
                ["fallback_id"]=Fallback, ["fallback_visible"]=true,
                ["same_conversation"]=true, ["account_verified"]=true
            }.ToJsonString();
            return Task.FromResult(new RunConclusion("success", proof, null));
        }

        public Task<RunConclusion>? StartRun(string task, RunOptions? options) => StartRun(task);
        public void Stop(string? taskKey) { }
        public Task<string?> CaptureScreenshotAsync(string? taskKey, CancellationToken ct) =>
            Task.FromResult<string?>(null);
    }

    public static async Task<int> Run()
    {
        var passed = 0;
        void Check(bool ok, string name)
        {
            if (!ok) throw new Exception("FAIL HTTP " + name);
            passed++;
            Console.WriteLine("PASS HTTP " + name);
        }

        var dir = Path.Combine(Path.GetTempPath(), "OFSC_HTTP_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var host = new FakeHost();
        var manager = new McpTaskManager(host, Path.Combine(dir, "tasks"), TimeSpan.FromSeconds(3));
        var secret = "self-chat-smoke-" + Guid.NewGuid().ToString("N");
        await using var server = new McpServer(manager, secret, operationalDatabasePath:Path.Combine(dir,"operational.db"));
        server.Start();
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var uri = new Uri($"http://127.0.0.1:{server.Port}/mcp/{secret}");
        long id = 0;

        async Task<JsonObject> Call(string method, JsonObject? parameters = null)
        {
            var rpc = new JsonObject {
                ["jsonrpc"]="2.0",["id"]=++id,["method"]=method,
                ["params"]=parameters ?? new JsonObject()
            };
            using var content = new StringContent(rpc.ToJsonString(), Encoding.UTF8,"application/json");
            using var response = await client.PostAsync(uri, content);
            var text=await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new Exception("HTTP status " + response.StatusCode + " " + text);
            return JsonNode.Parse(text) as JsonObject ?? throw new Exception("Invalid JSON-RPC");
        }

        async Task<JsonObject> Tool(string name, JsonObject? arguments = null, string? session = null)
        {
            var p = new JsonObject{["name"]=name,["arguments"]=arguments ?? new JsonObject()};
            if(session is not null)
                p["_meta"]=new JsonObject{["openai/session"]=session};
            var rpc=await Call("tools/call",p);
            var inner=rpc["result"]?["content"]?[0]?["text"]?.GetValue<string>() 
                ?? throw new Exception("No MCP text content");
            return JsonNode.Parse(inner) as JsonObject ?? throw new Exception("No tool JSON");
        }

        var tools = await Call("tools/list");
        var names=(tools["result"]?["tools"] as JsonArray)!.OfType<JsonObject>()
            .Select(x=>x["name"]?.ToString()).ToArray();
        Check(names.Contains("openfill_self_chat_identity") && names.Contains("openfill_self_chat_rename"),
              "five new tools listed over JSON-RPC");
        var sessionA = "v1/mcp-http-test-caller-a";
        var sessionB = "v1/mcp-http-test-caller-b";
        var one = await Tool("openfill_self_chat_identity",session:sessionA);
        Check(one["status"]?.ToString()=="transport_identity_available","opaque session metadata reaches self-ID");
        Check(one["native_chat_id"] is null,"never invent native URL on first contact");
        Check(host.Runs==0,"self-ID costs zero agent runs");
        var rejected=await Tool("openfill_self_chat_rename",
            new JsonObject{["new_name"]="OpenFill test"},sessionA);
        Check(rejected["status"]?.ToString()=="fallback_id_required" && host.Runs==0,
              "mutation without visible fallback fails before browser");
        var first=await Tool("openfill_self_chat_name",
            new JsonObject{["fallback_id"]=FakeHost.Fallback},sessionA);
        Check(first["status"]?.ToString() is "done" or "running", "first match starts one task");

        var taskId=first["task_id"]?.ToString();
        if(taskId is null) throw new Exception("No task ID");
        JsonObject status=new();
        for(var attempt=0;attempt<6;attempt++)
        {
            status=await Tool("openfill_task_status",new JsonObject{["task_id"]=taskId,["wait_seconds"]=0});
            if(status["status"]?.ToString()=="done")break;
            await Task.Delay(40);
        }
        Check(status["status"]?.ToString()=="done", "browser task completes");
        var completed = first["status"]?.ToString()=="done" ? first : status;
        Check(completed["independent_cdp_marker_verified"]?.ToString()=="true",
              "server independently verified exact marker in the conversation via CDP");
        Check(completed["native_url_bound"]?.ToString()=="true", "verified completion binds native URL");
        Check(completed["binding_saved_now"]?.ToString()=="true", "first completion writes binding");

        var second=await Tool("openfill_self_chat_identity",session:sessionA);
        Check(second["status"]?.ToString()=="native_id_verified","second ID lookup returns native URL");
        Check(second["native_chat_id"]?.ToString()==FakeHost.NativeId,"stored native UUID is exact");
        Check(host.Runs==1,"native-ID lookup after binding incurs no model calls");

        var other=await Tool("openfill_self_chat_identity",session:sessionB);
        Check(other["native_chat_id"] is null && other["status"]?.ToString()=="transport_identity_available",
              "another MCP session cannot access native ID");
        var fast=await Tool("openfill_self_chat_name",session:sessionA);
        Check(fast["status"]?.ToString()=="ok" && fast["name"]?.ToString()=="Nic nie rób", "cached title read returns title immediately");
        Check(host.Runs==1 && host.DirectTitleReads==2 && fast["browser_model_calls"]?.ToString()=="0",
              "cached title read zero model calls");

        host.RealMarkerPresent = false;
        var missingMarkerSession = "v1/mcp-http-test-marker-absent";
        var absent = await Tool("openfill_self_chat_name",
            new JsonObject{["fallback_id"]=FakeHost.Fallback},missingMarkerSession);
        var absentId=absent["task_id"]?.ToString();
        if (absentId is null) throw new Exception("No absent-marker task ID");
        var absentFinal=absent;
        for(int i=0; i<8 && absentFinal["status"]?.ToString()!="done"; i++)
        {
            absentFinal=await Tool("openfill_task_status",new JsonObject{["task_id"]=absentId,["wait_seconds"]=0});
            await Task.Delay(35);
        }
        Check(absentFinal["status"]?.ToString()=="done" &&
              absentFinal["independent_cdp_marker_verified"]?.ToString()=="false" &&
              absentFinal["binding_saved_now"]?.ToString()=="false",
              "agent claim rejected when exact marker absent from actual chat DOM");
        var notBound=await Tool("openfill_self_chat_identity",session:missingMarkerSession);
        Check(notBound["native_chat_id"] is null, "missing marker never creates native ID");

        host.RealMarkerPresent = true;
        host.CorrectVerifiedUrl = false;
        var mismatchSession = "v1/mcp-http-test-wrong-url";
        var wrongUrl = await Tool("openfill_self_chat_name",
            new JsonObject{["fallback_id"]=FakeHost.Fallback},mismatchSession);
        var wrongId=wrongUrl["task_id"]?.ToString();
        if(wrongId is null) throw new Exception("No mismatch task ID");
        var wrongFinal=wrongUrl;
        for(int i=0;i<8 && wrongFinal["status"]?.ToString()!="done";i++)
        {
            wrongFinal=await Tool("openfill_task_status",new JsonObject{["task_id"]=wrongId,["wait_seconds"]=0});
            await Task.Delay(35);
        }
        Check(wrongFinal["status"]?.ToString()=="done" &&
              wrongFinal["independent_cdp_marker_verified"]?.ToString()=="false" &&
              wrongFinal["binding_saved_now"]?.ToString()=="false",
              "browser-reported wrong URL rejected despite model claiming success");
        var wrongNotBound=await Tool("openfill_self_chat_identity",session:mismatchSession);
        Check(wrongNotBound["native_chat_id"] is null, "wrong URL never creates native chat mapping");

        try { Directory.Delete(dir,true); } catch {}
        return passed;
    }
}
