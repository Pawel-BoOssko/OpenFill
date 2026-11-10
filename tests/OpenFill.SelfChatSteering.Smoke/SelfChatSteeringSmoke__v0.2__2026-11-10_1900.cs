using OpenFill.Core.Mcp;
using System.Text.Json.Nodes;

var n = 0;
void Check(bool value, string label)
{
    if (!value) throw new Exception("FAIL " + label);
    n++;
    Console.WriteLine("PASS " + label);
}

var empty = new Dictionary<string,string?>(StringComparer.OrdinalIgnoreCase);
var chatgpt = new JsonObject { ["platform"] = "chatgpt" };
var none = SelfChatSteering.Resolve(empty, chatgpt);
Check(none.Platform == "chatgpt" && none.NativeChatId is null, "without MCP metadata native ID unavailable");
Check(SelfChatSteering.IdentityStatus(none)["status"]!.ToString() == "identity_unavailable", "unavailable not fabricated");
Check(SelfChatSteering.ValidateAction(none,"rename",null)?["status"]!.ToString() == "fallback_id_required", "no mutation without printed fallback");
var session = new Dictionary<string,string?>(StringComparer.OrdinalIgnoreCase) { ["Mcp-Session-Id"] = "abc" };
Check(SelfChatSteering.Resolve(session,chatgpt).NativeChatId is null, "transport session ID not conversation ID");

var meta = new JsonObject { ["openai/session"] = "v1/test-session-a-not-native-uuid" };
var a = SelfChatSteering.Resolve(empty, new JsonObject(),meta);
var again = SelfChatSteering.Resolve(empty, new JsonObject(),meta);
var b = SelfChatSteering.Resolve(empty, new JsonObject(),new JsonObject {["openai/session"] = "v1/test-session-b"});
Check(a.Platform == "chatgpt", "ChatGPT detected from metadata");
Check(a.SelfSessionHash?.Length == 16, "session fingerprint stable size");
Check(a.SelfSessionHash == again.SelfSessionHash, "same caller stable");
Check(a.SelfSessionHash != b.SelfSessionHash, "different caller isolated");
var status = SelfChatSteering.IdentityStatus(a);
Check(status["status"]!.ToString() == "transport_identity_available", "ID method without fallback");
Check(status["native_chat_id"] is null, "do not fabricate browser URL");
Check(status["self_chat_id"]!.ToString().StartsWith("of-chat-"), "own identifier returned");
Check(!status.ToJsonString().Contains("v1/test-session-a"), "raw session not returned");

var marker="OFSCWDWQHHA9LDYSLJ95QIRME87Q";
Check(marker.Length == 28 && SelfChatSteering.ValidFallback(marker), "real chat visible marker valid");
Check(SelfChatSteering.ValidFallback(new string('a',20)), "20 char minimum");
Check(SelfChatSteering.ValidFallback(new string('x',32)), "32 char maximum");
Check(!SelfChatSteering.ValidFallback(new string('a',19)), "too short rejected");
Check(!SelfChatSteering.ValidFallback(new string('z',33)), "too long rejected");
Check(!SelfChatSteering.ValidFallback("OFSC1234567890123456-A"), "dash rejected");
Check(!SelfChatSteering.ValidFallback("OFSC1234567890123456 A"), "space rejected");
Check(!SelfChatSteering.ValidFallback("OFSC1234567890123456.A"), "dot rejected");
Check(!SelfChatSteering.ValidFallback("OFSC1234567890123456_"), "underscore rejected");
var form=new JsonObject { ["platform"]="chatgpt",["fallback_id"]=marker,["new_name"]="OpenFill Development"};
var context=SelfChatSteering.Resolve(empty,form,meta);
Check(context.HasFallbackId, "fallback exists");
Check(SelfChatSteering.ValidateAction(context,"rename",form) is null, "rename validated with fallback");
var fastCandidate=new Dictionary<string,string?>(StringComparer.OrdinalIgnoreCase) {
    ["X-OpenFill-Self-Platform"]="chatgpt",["X-OpenFill-Self-Conversation-Id"]="8fae14dc-2a64-4afd-a0b8-63b852cc03b8"
};
var direct=SelfChatSteering.Resolve(fastCandidate,form,meta);
var fast=SelfChatSteering.BuildBrowserTask(direct,"name",form);
Check(fast.Contains("FAST PATH") && fast.Contains("chatgpt.com/c/8fae14dc"), "known URL opens direct");
Check(fast.Contains(marker) && fast.Contains("verify"),"direct path must validate marker");
var fallback=SelfChatSteering.BuildBrowserTask(context,"rename",form);
Check(fallback.Contains("FALLBACK") && fallback.Contains("Search chats ONCE"), "one exact-token search");
Check(fallback.Contains(marker) && !fallback.Contains("conversation_evidence"), "no long text search");
Check(fallback.Contains("OPENFILL SELF-CHAT OPERATION rename"), "rename step exists");
Check(SelfChatSteering.TaskChatLabel(context)==SelfChatSteering.TaskChatLabel(direct), "same MCP chat remains stable");
var badTitle=new JsonObject {["new_name"]=new string('X',121)};
Check(SelfChatSteering.ValidateAction(context,"rename",badTitle)?["status"]!.ToString()=="invalid_name", "title validation");
var invalidProject=new JsonObject {["project_name"]=" "};
Check(SelfChatSteering.ValidateAction(context,"move_project",invalidProject)?["status"]!.ToString()=="invalid_project","project validation");
foreach(var operation in new[]{"identity","name","rename","archive","move_project"})
    Check(SelfChatSteering.BuildBrowserTask(context,operation,form).Contains("BEFORE",StringComparison.OrdinalIgnoreCase) ||
          SelfChatSteering.BuildBrowserTask(context,operation,form).Contains("Before"), "pre-mutation check "+operation);
var defs=McpServer.ToolList().OfType<JsonObject>().ToList();
foreach(var name in new[]{"openfill_self_chat_identity","openfill_self_chat_name","openfill_self_chat_rename","openfill_self_chat_archive","openfill_self_chat_move_project"})
{
    var definition=defs.Single(t=>t["name"]?.ToString()==name);
    var properties=(JsonObject)definition["inputSchema"]!["properties"]!;
    Check(properties.ContainsKey("fallback_id") && !properties.ContainsKey("conversation_evidence"),"fallback contract "+name);
    Check(!properties.ContainsKey("target_chat_id") && !properties.ContainsKey("url"),"no arbitrary target "+name);
    Check(definition.ToJsonString().Contains("visibl",StringComparison.OrdinalIgnoreCase) ||
          definition.ToJsonString().Contains("print",StringComparison.OrdinalIgnoreCase),
          "definition requires visible marker "+name);
}
var claude=SelfChatSteering.Resolve(empty,new JsonObject {["platform"]="claude",["fallback_id"]=marker});
Check(claude.Platform=="claude" && claude.HasFallbackId,"Claude fallback allowed");
var tmp = Path.Combine(Path.GetTempPath(), "OpenFillSelfChatBindingSmoke_" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(tmp);
var bindingStore = new SelfChatBindingStore(Path.Combine(tmp, "operational.db"));
var nativeGuid = "8fae14dc-2a64-4afd-a0b8-63b852cc03b8";
var goodProof = "OPENFILL_SELF_CHAT_RESULT: " +
    """{"status":"ok","platform":"chatgpt","native_chat_id":"8fae14dc-2a64-4afd-a0b8-63b852cc03b8","verified_url":"https://chatgpt.com/c/8fae14dc-2a64-4afd-a0b8-63b852cc03b8","fallback_id":"OFSCWDWQHHA9LDYSLJ95QIRME87Q","fallback_visible":true,"same_conversation":true,"account_verified":true}""";
Check(!bindingStore.TryRecord(context, goodProof, "partial"), "partial agent result not cached");
Check(!bindingStore.TryRecord(context, goodProof.Replace(marker,"OFSC111111111111111111111111"), "success"), "wrong marker cannot bind");
Check(!bindingStore.TryRecord(context, goodProof.Replace("chatgpt.com", "wrong.example"), "success"), "wrong URL domain cannot bind");
Check(!bindingStore.TryRecord(context, goodProof.Replace("account_verified\":true", "account_verified\":false"), "success"), "unverified account cannot bind");
Check(!bindingStore.TryRecord(context, "Agent said success but supplied no JSON proof", "success"), "unstructured model summary cannot bind");
Check(bindingStore.TryRecord(context, goodProof, "success"), "exact verified marker may be bound");
Check(bindingStore.TryGet(context.SelfSessionHash, "chatgpt", out var cachedId) && cachedId == nativeGuid, "cached native chat ID returned");
Check(!bindingStore.TryGet(b.SelfSessionHash, "chatgpt", out _), "second caller cannot reuse binding");
var cachedCtx = context with { NativeChatId = cachedId, FallbackId = null, IdentitySource = "server_verified_binding" };
Check(SelfChatSteering.ValidateAction(cachedCtx, "name", new JsonObject()) is null, "verified binding permits direct read without fallback");
var cachedTask = SelfChatSteering.BuildBrowserTask(cachedCtx, "name", new JsonObject());
Check(cachedTask.Contains("FAST PATH") && !cachedTask.Contains("Search chats ONCE"), "after bind no search");
Check(SelfChatSteering.IdentityStatus(cachedCtx)["native_url_bound"]!.ToString() == "true", "identity returns native URL from verified cache");
var bindingFile = Path.Combine(tmp, SelfChatBindingStore.FileName);
Check(File.Exists(bindingFile) && !File.ReadAllText(bindingFile).Contains(marker), "binding durable and raw fallback excluded");
Directory.Delete(tmp, true);
Check(cachedTask.Length < 900, "direct task has bounded prompt length");
Check(fallback.Length < 1600, "fallback task has bounded prompt length");
Console.WriteLine($"TOTAL {n} PASSED");
