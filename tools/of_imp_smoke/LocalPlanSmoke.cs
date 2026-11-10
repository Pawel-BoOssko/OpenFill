using System.Text.Json.Nodes;
using OpenFill.Core.Agent;

internal static class LocalPlanSmoke
{
    private const string ValidJson = """
    {
      "protocol": "of.plan.v1",
      "expected_host": "example.test",
      "nodes": [
        {"id":"n1","op":"locate","role":"link","name":"Customer 42","bind":"row"},
        {"id":"n2","op":"click","ref":"$row","effect":"navigation"},
        {"id":"n3","op":"assert_visible","role":"textbox","name":"Customer name"}
      ]
    }
    """;

    private static JsonObject PlanJson() => JsonNode.Parse(ValidJson)!.AsObject();
    private static ValidatedSemanticPlan Plan() => PlanProtocolValidator.Parse(PlanJson());

    private static void Eq<T>(T expected, T actual, string label)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"ARCHFLOW_PLAN {label}: expected {expected}, actual {actual}");
    }

    private static void Reject(JsonObject json, string reason)
    {
        try { PlanProtocolValidator.Parse(json); }
        catch (ArgumentException ex)
        {
            Eq(reason, ex.Message, "validation reason");
            return;
        }
        throw new Exception("ARCHFLOW_PLAN accepted invalid plan: " + reason);
    }

    private sealed class FakeBrowser : ILocalPlanBrowser
    {
        public string Mode { get; init; } = "ok";
        public int Calls { get; private set; }
        public int Effects { get; private set; }
        private bool _form;
        private long _revision = 1;
        private int _reads;

        public Task<SemanticPlanSnapshot> ReadAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _reads++;
            if (Mode == "read_exception") throw new InvalidOperationException("test read");
            var host = Mode == "host_mismatch" ||
                (Mode == "host_changes_after_click" && _form) ? "other.test" : "example.test";
            var elements = new List<SemanticPlanElement>();
            if (!_form)
            {
                if (Mode != "missing")
                    elements.Add(new("row42", "link", "Customer 42", Mode != "disabled"));
                if (Mode == "ambiguous")
                    elements.Add(new("row42duplicate", "link", "Customer 42", true));
            }
            else if (Mode != "postcondition_missing")
            {
                elements.Add(new("name42", "textbox", "Customer name", true));
                if (Mode == "postcondition_ambiguous")
                    elements.Add(new("name42duplicate", "textbox", "Customer name", true));
            }
            return Task.FromResult(new SemanticPlanSnapshot(
                Mode == "invalid_revision" ? -1 :
                Mode == "regressed_revision" && _reads == 2 ? 0 : _revision, host, elements));
        }

        public Task<SemanticPlanClickResult> ClickNavigationAsync(
            string elementRef, long expectedRevision, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Calls++;
            Eq("row42", elementRef, "clicked ref");
            if (Mode == "action_exception") throw new InvalidOperationException("test action");
            if (Mode == "stale" || expectedRevision != _revision)
                return Task.FromResult(new SemanticPlanClickResult("stale"));
            if (Mode == "uncertain")
                return Task.FromResult(new SemanticPlanClickResult("uncertain"));
            if (Mode == "failed_before_action")
                return Task.FromResult(new SemanticPlanClickResult("failed_before_action"));
            Effects++;
            _form = true;
            if (Mode != "unchanged_revision") _revision++;
            return Task.FromResult(new SemanticPlanClickResult("applied", "fake-nav-evidence"));
        }
    }

    public static async Task RunAsync()
    {
        var executor = new LocalPlanExecutor();
        int checks = 0;

        var okBrowser = new FakeBrowser();
        var ok = await executor.ExecuteAsync(Plan(), okBrowser);
        Eq("completed", ok.Status, "success status");
        Eq(3, ok.Nodes.Count, "node outcomes");
        Eq("applied_unverified", ok.Nodes[1].Status, "action is not proof");
        Eq("observed", ok.Nodes[2].Status, "postcondition");
        Eq(1, okBrowser.Calls, "success click calls");
        Eq(1, okBrowser.Effects, "success effects");
        checks++;

        foreach (var (mode, reason) in new[] {
            ("ambiguous", "locator_ambiguous"), ("missing", "locator_missing"),
            ("disabled", "locator_disabled"), ("host_mismatch", "host_changed"),
            ("invalid_revision", "invalid_revision"), ("read_exception", "snapshot_unavailable"),
            ("regressed_revision", "stale_snapshot")
        })
        {
            var fake = new FakeBrowser { Mode = mode };
            var outcome = await executor.ExecuteAsync(Plan(), fake);
            Eq("needs_replan", outcome.Status, mode + " status");
            Eq(reason, outcome.Reason, mode + " reason");
            Eq(0, fake.Calls, mode + " zero clicks");
            checks++;
        }

        foreach (var (mode, status, reason) in new[] {
            ("stale", "needs_replan", "action_stale"),
            ("failed_before_action", "needs_replan", "action_failed_before_action"),
            ("uncertain", "uncertain_effect", "action_uncertain"),
            ("action_exception", "uncertain_effect", "browser_action_exception"),
            ("postcondition_missing", "needs_replan", "postcondition_missing"),
            ("postcondition_ambiguous", "needs_replan", "postcondition_ambiguous"),
            ("host_changes_after_click", "needs_replan", "host_changed"),
            ("unchanged_revision", "needs_replan", "postcondition_revision_unchanged")
        })
        {
            var fake = new FakeBrowser { Mode = mode };
            var outcome = await executor.ExecuteAsync(Plan(), fake);
            Eq(status, outcome.Status, mode + " status");
            Eq(reason, outcome.Reason, mode + " reason");
            Eq(1, fake.Calls, mode + " no retry");
            Eq(mode.StartsWith("postcondition_") || mode == "host_changes_after_click" ||
                mode == "unchanged_revision" ? 1 : 0,
                fake.Effects, mode + " effects");
            checks++;
        }

        var duplicate = PlanJson();
        duplicate["nodes"]![1]!["id"] = "n1";
        Reject(duplicate, "plan_duplicate_node_id");
        checks++;

        var unsafeEffect = PlanJson();
        unsafeEffect["nodes"]![1]!["effect"] = "send_message";
        Reject(unsafeEffect, "plan_side_effect_not_supported");
        checks++;

        var unknownBinding = PlanJson();
        unknownBinding["nodes"]![1]!["ref"] = "$other";
        Reject(unknownBinding, "plan_unknown_binding");
        checks++;

        var unsafeNavigation = PlanJson();
        unsafeNavigation["nodes"]![0]!["role"] = "button";
        Reject(unsafeNavigation, "plan_non_navigation_locator");
        checks++;

        var unsupported = PlanJson();
        unsupported["nodes"]![1]!["op"] = "run_js";
        Reject(unsupported, "plan_unknown_op");
        checks++;

        var missingPostcondition = PlanJson();
        missingPostcondition["nodes"]!.AsArray().RemoveAt(2);
        Reject(missingPostcondition, "plan_missing_postcondition");
        checks++;

        var unknownField = PlanJson();
        unknownField["nodes"]![0]!["javascript"] = "alert(1)";
        Reject(unknownField, "plan_unknown_field_javascript");
        checks++;

        var wrongProtocol = PlanJson();
        wrongProtocol["protocol"] = "of.plan.v2";
        Reject(wrongProtocol, "plan_protocol_unsupported");
        checks++;

        var tooMany = PlanJson();
        var arr = tooMany["nodes"]!.AsArray();
        for (int i = 0; i < 29; i++)
            arr.Add(new JsonObject {
                ["id"] = "extra" + i, ["op"] = "assert_visible",
                ["role"] = "textbox", ["name"] = "Customer name"
            });
        Reject(tooMany, "plan_invalid_node_count");
        checks++;

        var missingHost = PlanJson();
        missingHost["expected_host"] = "https://example.test";
        Reject(missingHost, "plan_invalid_host");
        checks++;

        Console.WriteLine($"ARCHFLOW_LOCAL_PLAN_{checks}_CHECKS_OK");
    }
}
