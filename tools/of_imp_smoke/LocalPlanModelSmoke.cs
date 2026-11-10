using System.Text.Json.Nodes;
using OpenFill.Core.Agent;
using OpenFill.Core.Model;

internal static class LocalPlanModelSmoke
{
    private static int _checks;
    private static void Check(bool ok, string label)
    {
        if (!ok) throw new Exception("ARCHFLOW_PLANNER_" + label);
        _checks++;
    }

    private const string Plan = """
    {"protocol":"of.plan.v1","expected_host":"example.test","nodes":[
      {"id":"link","op":"locate","role":"link","name":"Customer 42","bind":"row"},
      {"id":"open","op":"click","ref":"$row","effect":"navigation"},
      {"id":"wait","op":"wait_until","role":"textbox","name":"Customer name","timeout_ms":500},
      {"id":"field","op":"locate","role":"textbox","name":"Customer name","bind":"f"},
      {"id":"fill","op":"fill","ref":"$f","value":"Alice"},
      {"id":"verify","op":"assert_value","ref":"$f","value":"Alice"}
    ]}
    """;

    private sealed class FakeModel(string variant = "ok") : IModelClient
    {
        public int Calls { get; private set; }
        public JsonObject? Request { get; private set; }

        public Task<JsonObject> CreateResponseAsync(JsonObject request, CancellationToken ct = default)
        {
            Calls++;
            Request = request;
            Check(request["store"]?.GetValue<bool>() == false, "no_response_storage");
            Check(request["tools"]?.AsArray().Count == 1, "single_plan_tool");
            Check(request["tool_choice"]?["name"]?.ToString() == "submit_plan", "forced_plan_tool");
            Check(request["input"]?.ToJsonString().Contains("example.test") == true, "observed_host_in_prompt");
            Check(!request.ToJsonString().Contains("Initial secret"), "input_value_omitted");
            var json = variant switch {
                "bad_binding" when Calls == 1 => Plan.Replace("$row", "$undefined"),
                "bad_binding" => Plan,
                "staged" when Calls == 1 => """
                  {"protocol":"of.plan.v1","expected_host":"example.test","nodes":[
                    {"id":"l","op":"locate","role":"link","name":"Customer 42","bind":"row"},
                    {"id":"c","op":"click","ref":"$row","effect":"navigation"},
                    {"id":"w","op":"wait_until","role":"link","name":"Customer 42","timeout_ms":100}
                  ]}
                  """,
                "staged" => """
                  {"protocol":"of.plan.v1","expected_host":"example.test","nodes":[
                    {"id":"l","op":"locate","role":"textbox","name":"Customer name","bind":"f"},
                    {"id":"f","op":"fill","ref":"$f","value":"Alice"},
                    {"id":"v","op":"assert_value","ref":"$f","value":"Alice"}
                  ]}
                  """,
                "invalid_effect" => Plan.Replace("\"navigation\"", "\"send\""),
                "wrong_host" => Plan.Replace("example.test", "elsewhere.test"),
                _ => Plan
            };
            var call = new JsonObject {
                ["type"] = "function_call",
                ["name"] = variant == "wrong_tool" ? "other_tool" : "submit_plan",
                ["call_id"] = "call_mock_1",
                ["arguments"] = variant == "malformed"
                    ? "{bad" : new JsonObject { ["plan_json"] = json }.ToJsonString()
            };
            var res = new JsonObject {
                ["output"] = new JsonArray(call),
                ["usage"] = new JsonObject { ["input_tokens"] = 123, ["output_tokens"] = 45 }
            };
            if (variant == "incomplete") res["status"] = "incomplete";
            return Task.FromResult(res);
        }
    }

    private sealed class FakeBrowser : ILocalPlanFormBrowser
    {
        public int Reads, Actions;
        public bool OnForm;
        public string Value = "Initial secret";
        private long _revision = 1;
        public Task<SemanticPlanSnapshot> ReadAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Reads++;
            var rows = OnForm
                ? new[] {new SemanticPlanElement("field","textbox","Customer name",true,Value)}
                : new[] {new SemanticPlanElement("row","link","Customer 42",true)};
            return Task.FromResult(new SemanticPlanSnapshot(_revision,"example.test",rows));
        }

        public Task<SemanticPlanClickResult> ClickNavigationAsync(
            string elementRef, long expectedRevision, CancellationToken ct)
        {
            Actions++;
            if (elementRef != "row" || expectedRevision != _revision)
                return Task.FromResult(new SemanticPlanClickResult("stale"));
            OnForm = true;
            _revision++;
            return Task.FromResult(new SemanticPlanClickResult("applied","local-page"));
        }

        public Task<SemanticPlanClickResult> FillTextAsync(
            string elementRef, long expectedRevision, string value, CancellationToken ct)
        {
            Actions++;
            if (elementRef != "field" || expectedRevision != _revision)
                return Task.FromResult(new SemanticPlanClickResult("stale"));
            Value = value;
            _revision++;
            return Task.FromResult(new SemanticPlanClickResult("applied","input-value"));
        }
    }

    public static async Task RunAsync()
    {
        var goodModel = new FakeModel();
        var browser = new FakeBrowser();
        var runner = new LocalPlanModelDevRunner();
        var result = await runner.ExecuteAsync(goodModel,"mock-model",
            "Open Customer 42 then set Customer name to Alice",browser,
            "operation14","task14","run14");
        Check(result.Status == "completed", "completed_" + result.Reason);
        Check(goodModel.Calls == 1 && result.ModelCalls == 1, "one_model_roundtrip");
        Check(result.InputTokens == 123 && result.OutputTokens == 45, "real_usage_fields");
        Check(result.PlanningSnapshotCalls == 1 && result.PlanningSnapshotSuccesses == 1,
            "first_dom_snapshot_measured");
        Check(result.ExecutorSnapshotCalls == 6, "executor_dom_reads");
        Check(result.NavigationAttempts == 1 && result.FillAttempts == 1, "two_actions");
        Check(result.ObservedConditions == 2, "two_verified_postconditions");
        Check(result.TotalDomReads == 7 && result.TotalBrowserActionAttempts == 2,
            "total_detailed_counts");
        Check(browser.Value == "Alice" && browser.Actions == 2, "actual_form_effect_once");
        Check(result.RequestChars > 300, "request_size_metered");
        Check(result.BusinessOperationId == "operation14" && result.RunId == "run14",
            "business_correlations");
        var stagedModel = new FakeModel("staged");
        var stagedBrowser = new FakeBrowser();
        var staged = await runner.ExecuteAsync(stagedModel, "mock-model",
            "Open Customer 42 then set Customer name to Alice",
            stagedBrowser, "operation14", "task14", "run14");
        Check(staged.Status == "completed", "replan_completed_" + staged.Reason);
        Check(staged.ModelCalls == 2 && stagedModel.Calls == 2, "replan_two_calls");
        Check(staged.NavigationAttempts == 1 && staged.FillAttempts == 1,
            "replan_no_duplicate_navigation");
        Check(staged.InputTokens == 246 && staged.OutputTokens == 90,
            "replan_usage_aggregated");
        Check(stagedBrowser.Value == "Alice" && stagedBrowser.Actions == 2,
            "replan_goal_verified");

        var correctedModel = new FakeModel("bad_binding");
        var correctedBrowser = new FakeBrowser();
        var corrected = await runner.ExecuteAsync(correctedModel, "mock-model",
            "Open Customer 42 then set Customer name to Alice",
            correctedBrowser, "operation14", "task14", "run14");
        Check(corrected.Status == "completed", "binding_correction_completed_" + corrected.Reason);
        Check(corrected.ModelCalls == 2 && correctedModel.Calls == 2,
            "binding_one_correction_call");
        Check(correctedBrowser.Actions == 2 &&
            corrected.NavigationAttempts == 1 && corrected.FillAttempts == 1,
            "binding_invalid_plan_had_zero_actions");

        foreach (var (variant, code) in new[] {
            ("wrong_tool","planner_wrong_function_calls"),
            ("invalid_effect","plan_side_effect_not_supported"),
            ("wrong_host","planner_host_mismatch"),
            ("malformed","planner_arguments_invalid_json"),
            ("incomplete","planner_response_incomplete")
        })
        {
            var model = new FakeModel(variant);
            var isolated = new FakeBrowser();
            var failed = await runner.ExecuteAsync(model,"mock-model","Open Customer 42",isolated,
                "op14","task14","run14");
            Check(failed.Reason == code, variant + "_cause_" + failed.Reason);
            Check(failed.ModelCalls == 1 && isolated.Actions == 0, variant + "_zero_actions");
        }
        Console.WriteLine("ARCHFLOW_PLANNER_" + _checks + "_CHECKS_OK");
    }
}
