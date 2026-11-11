using System.Text.Json.Nodes;
using OpenFill.Core.Agent;

internal static class LocalPlanFlowSmoke
{
    private static int _checks;
    private static void Check(bool b, string code)
    {
        if (!b) throw new Exception("ARCHFLOW_FLOW_" + code);
        _checks++;
    }

    private static JsonObject Plan() => JsonNode.Parse("""
    {"protocol":"of.flow.v1","expected_host":"example.test","nodes":[
       {"id":"choice","op":"if","role":"textbox","name":"Status: Present",
        "then":[{"id":"yes","op":"assert_visible","role":"textbox","name":"Status: Present"}],
        "else":[{"id":"no","op":"assert_absent","role":"textbox","name":"Status: Present"}]},
       {"id":"items","op":"foreach","role":"link","name_prefix":"Customer ",
        "max_items":5,
        "body":[{"id":"exists","op":"assert_visible","role":"link","name":"$item"}]}
    ]}
    """)!.AsObject();

    private sealed class Browser(string mode) : ILocalPlanBrowser
    {
        private int _reads;
        public Task<SemanticPlanSnapshot> ReadAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _reads++;
            var elements = new List<SemanticPlanElement>();
            if (mode != "missing_status") elements.Add(new("t","textbox","Status: Present",true));
            elements.Add(new("a","link","Customer A",true));
            if (mode == "duplicate") elements.Add(new("dup","link","Customer A",true));
            else if (mode != "disappearing" || _reads < 6)
                elements.Add(new("b","link","Customer B",true));
            var host = mode == "host_changed" && _reads >= 4 ? "other.test" : "example.test";
            return Task.FromResult(new SemanticPlanSnapshot(1,host,elements));
        }
        public Task<SemanticPlanClickResult> ClickNavigationAsync(
            string elementRef, long expectedRevision, CancellationToken ct)
            => throw new Exception("READ_ONLY_FLOW_PERFORMED_CLICK");
    }

    private static void Reject(JsonObject json, string reason)
    {
        try { LocalPlanControlFlow.Validate(json); }
        catch (ArgumentException ex)
        {
            Check(ex.Message == reason, "validator_" + ex.Message);
            return;
        }
        throw new Exception("FLOW accepted invalid " + reason);
    }

    public static async Task RunAsync()
    {
        var plan = Plan();
        var ok = await new LocalPlanControlFlow(new Browser("ok"),"example.test").ExecuteAsync(plan);
        Check(ok.Status == "completed", "ok");
        Check(ok.Iterations == 2, "two_items");
        Check(ok.Observed.Any(s => s == "choice:then"), "true_branch");
        Check(ok.Observed.Count(s => s.StartsWith("exists:")) == 2, "two_live_asserts");

        var missing = await new LocalPlanControlFlow(new Browser("missing_status"),"example.test").ExecuteAsync(plan);
        Check(missing.Status == "completed", "missing_branch_completes");
        Check(missing.Observed.Any(s => s == "choice:else"), "else_branch");
        Check(missing.Iterations == 2, "missing_branch_items");

        var duplicate = await new LocalPlanControlFlow(new Browser("duplicate"),"example.test").ExecuteAsync(plan);
        Check(duplicate.Reason == "foreach_ambiguous", "ambiguous_rejected");
        var shortPlan = Plan();
        shortPlan["nodes"]![1]!["max_items"] = 1;
        var exceeded = await new LocalPlanControlFlow(new Browser("ok"),"example.test").ExecuteAsync(shortPlan);
        Check(exceeded.Reason == "foreach_limit_exceeded", "limit_rejected");
        var stale = await new LocalPlanControlFlow(new Browser("disappearing"),"example.test").ExecuteAsync(plan);
        Check(stale.Status == "completed" || stale.Reason == "foreach_item_missing",
            "changing_list_re_resolved");
        var host = await new LocalPlanControlFlow(new Browser("host_changed"),"example.test").ExecuteAsync(plan);
        Check(host.Reason == "flow_host_changed", "host_guard");

        var mismatch = Plan();
        mismatch["expected_host"] = "different.test";
        var mismatchResult = await new LocalPlanControlFlow(new Browser("ok"), "example.test")
            .ExecuteAsync(mismatch);
        Check(mismatchResult.Reason == "flow_plan_host_mismatch" && mismatchResult.Reads == 0,
            "plan_host_rejected_before_read");
        var write = Plan();
        write["nodes"]![0]!["then"]![0]!["op"] = "fill";
        Reject(write, "flow_write_not_supported");
        var duplicateIds = Plan();
        duplicateIds["nodes"]![1]!["id"] = "choice";
        Reject(duplicateIds, "flow_duplicate_node");
        var huge = Plan();
        huge["nodes"]![1]!["max_items"] = 21;
        Reject(huge, "flow_invalid_max_items");
        Console.WriteLine($"ARCHFLOW_FLOW_{_checks}_CHECKS_OK");
    }
}
