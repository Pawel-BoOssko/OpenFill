using System.Text.Json.Nodes;
using OpenFill.Core.Agent;

internal static class LocalPlanIntegratedFlowSmoke
{
    private static int _checks;
    private static void Check(bool ok, string what)
    {
        if (!ok) throw new Exception("ARCHFLOW_INTEGRATED_" + what);
        _checks++;
    }

    private static JsonObject Plan() => JsonNode.Parse("""
    {"protocol":"of.plan.v1","expected_host":"example.test","nodes":[
      {"id":"choice","op":"if","role":"textbox","name":"Status: Present",
       "then":[{"id":"yes","op":"assert_visible","role":"textbox","name":"Status: Present"}],
       "else":[{"id":"no","op":"assert_absent","role":"textbox","name":"Status: Present"}]},
      {"id":"rows","op":"foreach","role":"link","name_prefix":"Row ",
       "max_items":3,"body":[{"id":"each","op":"assert_visible","role":"link","name":"$item"}]},
      {"id":"locate","op":"locate","role":"link","name":"Customer 42","bind":"link"},
      {"id":"click","op":"click","ref":"$link","effect":"navigation"},
      {"id":"wait","op":"wait_until","role":"textbox","name":"Customer name","timeout_ms":350},
      {"id":"field","op":"locate","role":"textbox","name":"Customer name","bind":"name"},
      {"id":"fill","op":"fill","ref":"$name","value":"Verified"},
      {"id":"verify","op":"assert_value","ref":"$name","value":"Verified"}
    ]}
    """)!.AsObject();

    private sealed class Fake(string mode = "ok") : ILocalPlanFormBrowser
    {
        private long _revision = 1;
        private bool _form;
        private int _reads;
        private string _value = "Initial";
        public int Clicks, Fills;
        public Task<SemanticPlanSnapshot> ReadAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _reads++;
            var elements = new List<SemanticPlanElement>();
            if (_form)
                elements.Add(new("f", "textbox", "Customer name", true, _value));
            else
            {
                if (mode != "absent")
                    elements.Add(new("s","textbox","Status: Present",true));
                elements.Add(new("a","link","Row A",true));
                elements.Add(new("b","link","Row B",true));
                if (mode == "duplicate") elements.Add(new("b2","link","Row A",true));
                elements.Add(new("c","link","Customer 42",true));
            }
            return Task.FromResult(new SemanticPlanSnapshot(_revision,
                mode == "hostflip" && _reads > 2 ? "other.test" : "example.test",
                elements));
        }
        public Task<SemanticPlanClickResult> ClickNavigationAsync(
            string elementRef, long expectedRevision, CancellationToken ct)
        {
            Clicks++;
            if (elementRef != "c" || expectedRevision != _revision)
                return Task.FromResult(new SemanticPlanClickResult("stale"));
            _form = true; _revision++;
            return Task.FromResult(new SemanticPlanClickResult("applied","fake-nav"));
        }
        public Task<SemanticPlanClickResult> FillTextAsync(
            string elementRef, long expectedRevision, string value, CancellationToken ct)
        {
            Fills++;
            if (elementRef != "f" || expectedRevision != _revision)
                return Task.FromResult(new SemanticPlanClickResult("stale"));
            _value = value; _revision++;
            return Task.FromResult(new SemanticPlanClickResult("applied","fake-fill"));
        }
        public string Value => _value;
    }

    private static void Reject(JsonObject plan, string reason)
    {
        try { PlanProtocolValidator.Parse(plan); }
        catch (ArgumentException e)
        {
            Check(e.Message == reason, "reject_" + reason + "_instead_" + e.Message);
            return;
        }
        throw new Exception("accepted invalid plan " + reason);
    }

    public static async Task RunAsync()
    {
        var parsed = PlanProtocolValidator.Parse(Plan());
        Check(parsed.Nodes.Count == 8, "eight_top_level_nodes");
        Check(parsed.Nodes.Count(n => n.FlowNode is not null) == 2,
            "both_flow_nodes_validated");
        var browser = new Fake();
        var result = await new LocalPlanExecutor().ExecuteAsync(parsed, browser);
        Check(result.Status == "completed", "combined_status_" + result.Reason);
        Check(result.Nodes.Any(n => n.NodeId == "choice" && n.Status == "flow_completed"),
            "if_executed");
        Check(result.Nodes.Any(n => n.NodeId == "rows" && n.Status == "flow_completed"),
            "foreach_executed");
        Check(result.Nodes.Count(n => n.Status == "observed") >= 5,
            "nested_assertions_and_postconditions");
        Check(browser.Clicks == 1 && browser.Fills == 1 && browser.Value == "Verified",
            "no_replayed_navigation_or_fill");

        var absent = new Fake("absent");
        result = await new LocalPlanExecutor().ExecuteAsync(parsed, absent);
        Check(result.Status == "completed", "false_branch_completed");
        Check(result.Nodes.Any(n => n.NodeId == "choice/no" && n.Status == "observed"),
            "false_branch_asserted");

        var duplicate = new Fake("duplicate");
        result = await new LocalPlanExecutor().ExecuteAsync(parsed, duplicate);
        Check(result.Status == "needs_replan" && result.Reason == "foreach_ambiguous",
            "duplicate_row_stops");
        Check(duplicate.Clicks == 0 && duplicate.Fills == 0, "duplicate_no_actions");

        var hostflip = new Fake("hostflip");
        result = await new LocalPlanExecutor().ExecuteAsync(parsed, hostflip);
        Check(result.Status == "needs_replan" && result.Reason == "flow_host_changed",
            "flow_detects_host_changed");
        Check(hostflip.Clicks == 0, "host_changed_no_actions");

        var malformed = Plan();
        malformed["nodes"]![0]!["then"]![0]!["op"] = "fill";
        Reject(malformed, "flow_write_not_supported");

        malformed = Plan();
        malformed["nodes"]![1]!["max_items"] = 6;
        Reject(malformed, "plan_flow_budget_exceeded");

        malformed = Plan();
        malformed["nodes"]![1]!["body"]![0]!["id"] = "choice";
        Reject(malformed, "plan_duplicate_node_id");

        malformed = Plan();
        malformed["nodes"]!.AsArray().Insert(4,
            JsonNode.Parse("""
             {"id":"premature","op":"if","role":"textbox","name":"Status: Present",
             "then":[{"id":"ok","op":"assert_visible","role":"textbox","name":"Status: Present"}],
             "else":[{"id":"no2","op":"assert_absent","role":"textbox","name":"Status: Present"}]}
             """));
        Reject(malformed, "plan_missing_postcondition");

        malformed = Plan();
        malformed["nodes"]![2]!["body"] = new JsonArray();
        Reject(malformed, "plan_invalid_flow_fields");

        Console.WriteLine($"ARCHFLOW_INTEGRATED_FLOW_{_checks}_CHECKS_OK");
    }
}
