using OpenFill.Core.Mcp;
using System.Text.Json.Nodes;

var passed = 0;
void Assert(bool ok, string name)
{
    if (!ok) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
    passed++;
}
var empty = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
var a = new JsonObject { ["platform"] = "chatgpt" };
var noData = SelfChatSteering.Resolve(empty, a);
Assert(noData.Platform == "chatgpt" && !noData.HasNativeId, "no native ID assumed");
Assert(SelfChatSteering.IdentityStatus(noData)["status"]!.ToString() == "identity_unavailable", "unavailable reported");
Assert(SelfChatSteering.ValidateAction(noData, "rename", new JsonObject { ["new_name"] = "new" })?["changed"]?.ToString() == "false", "no mutation with missing evidence");
var transport = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase) { ["Mcp-Session-Id"] = "287a" };
var sessionOnly = SelfChatSteering.Resolve(transport, a);
Assert(!sessionOnly.HasNativeId, "MCP transport session NOT native chat ID");
var clientClaim = new Dictionary<string,string?>(StringComparer.OrdinalIgnoreCase) {
    ["X-OpenFill-Self-Platform"] = "chatgpt",
    ["X-OpenFill-Self-Conversation-Id"] = "8fae14dc-2a64-4afd-a0b8-63b852cc03b8",
    ["X-OpenFill-Self-Account"] = "test@example.invalid"
};
var claim = SelfChatSteering.Resolve(clientClaim, a);
Assert(claim.HasNativeId && claim.IdentitySource == "http_header_unverified", "client claimed native ID flagged unverified");
Assert(SelfChatSteering.ValidateAction(claim, "archive", a)?["status"]!.ToString() == "identity_unavailable", "header alone cannot archive");
var evidence = new string('A', 90);
var renameArgs = new JsonObject { ["platform"] = "chatgpt", ["conversation_evidence"] = evidence, ["new_name"] = "Pilot XYZ" };
var ctx = SelfChatSteering.Resolve(clientClaim, renameArgs);
Assert(ctx.HasEvidence && ctx.HasNativeId, "evidence and candidate bound");
Assert(SelfChatSteering.ValidateAction(ctx, "rename", renameArgs) is null, "rename may run verification");
var task = SelfChatSteering.BuildBrowserTask(ctx, "rename", renameArgs);
Assert(task.Contains(evidence) && task.Contains("EXACTLY ONE") &&
       task.Contains("immediately before taking any action") &&
       task.Contains(claim.NativeChatId!), "browser instructions include exact match and identity invariants");
Assert(SelfChatSteering.TaskChatLabel(ctx).StartsWith("self-chat-"), "deterministic label");
var badName = new JsonObject { ["new_name"] = new string('z', 121) };
Assert(SelfChatSteering.ValidateAction(ctx, "rename", badName)?["status"]!.ToString() == "invalid_name", "reject invalid long title");
var badProject = new JsonObject { ["project_name"] = "  " };
Assert(SelfChatSteering.ValidateAction(ctx, "move_project", badProject)?["status"]!.ToString() == "invalid_project", "reject blank project");
var claude = SelfChatSteering.Resolve(empty, new JsonObject { ["platform"] = "claude", ["conversation_evidence"] = evidence });
Assert(claude.Platform == "claude" && claude.HasEvidence, "Claude evidence supported");
foreach (var op in new[] { "identity", "name", "rename", "archive", "move_project" })
    Assert(SelfChatSteering.BuildBrowserTask(ctx, op, renameArgs).Contains("same", StringComparison.OrdinalIgnoreCase) ||
           SelfChatSteering.BuildBrowserTask(ctx, op, renameArgs).Contains("SAME"), "same-ID check for " + op);
var tools = McpServer.ToolList().OfType<JsonObject>().ToArray();
var expected = new[] { "openfill_self_chat_identity", "openfill_self_chat_name", "openfill_self_chat_rename",
                       "openfill_self_chat_archive", "openfill_self_chat_move_project" };
foreach (var name in expected)
{
    var tool = tools.Single(x => x["name"]?.ToString() == name);
    var props = (JsonObject)tool["inputSchema"]!["properties"]!;
    Assert(!props.ContainsKey("chat_id") && !props.ContainsKey("target_chat_id") &&
           !props.ContainsKey("url"), "no explicit target allowed: " + name);
}
var meta = new JsonObject { ["openai/session"] = "v1/example-of-opaque-chat-session" };
var current = SelfChatSteering.Resolve(empty, new JsonObject(), meta);
var current2 = SelfChatSteering.Resolve(empty, new JsonObject(), meta);
var other = SelfChatSteering.Resolve(empty, new JsonObject(), new JsonObject { ["openai/session"] = "v1/another-chat" });
Assert(current.Platform == "chatgpt" && current.SelfSessionHash?.Length == 16, "ChatGPT metadata gives self session identity");
Assert(current.SelfSessionHash == current2.SelfSessionHash && current.SelfSessionHash != other.SelfSessionHash, "stable per-session identity distinguishes chats");
var selfStatus = SelfChatSteering.IdentityStatus(current);
Assert(selfStatus["status"]!.ToString() == "transport_identity_available", "what is my ID works without preceding operation");
Assert(selfStatus["native_chat_id"] is null, "native browser conversation ID is not fabricated");
Assert(!selfStatus.ToJsonString().Contains("v1/example-of-opaque-chat-session"), "raw opaque metadata never exposed");
Console.WriteLine($"TOTAL: {passed} tests passed");
