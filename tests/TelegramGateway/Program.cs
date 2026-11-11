using System.Text.Json.Nodes;
using OpenFill.Core.Hosting;

if (args.Contains("--pipe-server")) { var g = new MobileCardGateway((id, answer) => true); g.Open(new JsonObject { ["kind"]="ask", ["id"]="pipe-a", ["human"]=true, ["question"]="IPC test" }, "task-pipe", TimeSpan.FromMinutes(1)); g.Open(new JsonObject { ["kind"]="confirm", ["id"]="pipe-b", ["what"]="Approve IPC test" }, "task-pipe-b", TimeSpan.FromMinutes(1)); await using var server = new MobileCardPipeServer(g); server.Start(); Console.WriteLine("PIPE_READY"); await Task.Delay(30000); return; }
var passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS " + name);
    passed++;
}
var responses = new Dictionary<string,string>();
var gateway = new MobileCardGateway((id, answer) => responses.TryAdd(id, answer));
JsonObject Ask(string id, bool human) => new()
{
    ["kind"] = "ask", ["id"] = id, ["question"] = "Question", ["human"] = human,
    ["options"] = new JsonArray()
};
gateway.Open(Ask("nonhuman", false), "task-x", TimeSpan.FromMinutes(1));
Check(gateway.OpenCards().Length == 0, "Non-human MCP card not forwarded");
gateway.Open(Ask("a", true), "task-a", TimeSpan.FromMinutes(1));
gateway.Open(Ask("b", true), "task-b", TimeSpan.FromMinutes(1));
Check(gateway.OpenCards().Length == 2 && gateway.ReadEvents(0).Length == 2, "Two cards and ordered events");
Check(gateway.Submit(new("1","wrong","a","task-a","x")).Status == "wrong_instance", "Instance guard");
Check(gateway.Submit(new("2",gateway.InstanceId,"a","task-b","x")).Status == "wrong_task", "Task guard");
Check(gateway.Submit(new("3",gateway.InstanceId,"b","task-b","B")).Accepted, "Second card first");
Check(gateway.Submit(new("4",gateway.InstanceId,"a","task-a","A")).Accepted, "First card second");
Check(responses["a"] == "A" && responses["b"] == "B", "Exact card routing");
Check(!gateway.Submit(new("5",gateway.InstanceId,"a","task-a","overwrite")).Accepted, "First answer wins");
Check(gateway.Submit(new("3",gateway.InstanceId,"b","task-b","B")).Duplicate, "Idempotent action replay");
gateway.Close("a","answered");
Check(gateway.OpenCards().Length == 1 && gateway.ReadEvents(2).Length == 1, "Close event");
var expired = new MobileCardGateway((id, answer) => true);
expired.Open(Ask("expired",true), "task-e", TimeSpan.FromSeconds(-1));
Check(expired.Submit(new("6",expired.InstanceId,"expired","task-e","late")).Status == "expired", "Timeout guard");
var confirmations = new MobileCardGateway((id, answer) => true);
confirmations.Open(new JsonObject { ["kind"]="confirm", ["id"]="confirm", ["what"]="Approve?" }, "task-c", TimeSpan.FromMinutes(1));
Check(confirmations.Submit(new("7",confirmations.InstanceId,"confirm","task-c","maybe")).Status == "invalid", "Confirm format guard");
Check(confirmations.Submit(new("8",confirmations.InstanceId,"confirm","task-c","yes\nnote")).Accepted, "Confirm note");
var pipe = new MobileCardPipeServer(confirmations);
var health = pipe.Handle("{\"kind\":\"health\"}");
Check(health.Contains(confirmations.InstanceId), "IPC health");
var list = pipe.Handle("{\"kind\":\"list\"}");
Check(list.Contains("confirm"), "IPC list");
await pipe.DisposeAsync();
Console.WriteLine($"TOTAL PASS {passed}");
