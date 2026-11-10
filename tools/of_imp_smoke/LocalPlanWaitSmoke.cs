using System.Text.Json.Nodes;
using OpenFill.Core.Agent;

internal static class LocalPlanWaitSmoke
{
    private static int _checks;
    private static void Eq<T>(T expected, T actual, string label)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"ARCHFLOW_WAIT {label}: expected={expected}, actual={actual}");
        _checks++;
    }

    private static JsonObject Json(int timeoutMs = 350, bool withClick = true)
    {
        var nodes = new JsonArray();
        if (withClick)
        {
            nodes.Add(new JsonObject { ["id"]="locate",["op"]="locate",
                ["role"]="link",["name"]="Customer 42",["bind"]="row" });
            nodes.Add(new JsonObject { ["id"]="click",["op"]="click",
                ["ref"]="$row",["effect"]="navigation" });
        }
        nodes.Add(new JsonObject { ["id"]="wait",["op"]="wait_until",
            ["role"]="textbox",["name"]="Customer name",["timeout_ms"]=timeoutMs });
        return new JsonObject {
            ["protocol"]="of.plan.v1",["expected_host"]="example.test",["nodes"]=nodes
        };
    }

    private sealed class Fake : ILocalPlanBrowser
    {
        public string Mode { get; init; } = "delayed";
        public int ReadCount { get; private set; }
        public int ClickCount { get; private set; }
        private bool _clicked;

        public Task<SemanticPlanSnapshot> ReadAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            ReadCount++;
            var afterClickReads = ReadCount - 2; // locate, click, then polling
            var show = Mode switch {
                "initial_visible" => true,
                "never" => false,
                _ => _clicked && afterClickReads >= 3
            };
            var rows = new List<SemanticPlanElement>();
            if (!_clicked && Mode != "initial_visible")
                rows.Add(new("row", "link", "Customer 42", true));
            if (show)
            {
                rows.Add(new("name", "textbox", "Customer name", true));
                if (Mode == "ambiguous")
                    rows.Add(new("name2", "textbox", "Customer name", true));
            }
            var host = Mode == "host_changes" && _clicked ? "other.test" : "example.test";
            return Task.FromResult(new SemanticPlanSnapshot(show ? 2 : 1, host, rows));
        }

        public Task<SemanticPlanClickResult> ClickNavigationAsync(
            string elementRef, long expectedRevision, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            ClickCount++;
            if (elementRef != "row" || expectedRevision != 1)
                return Task.FromResult(new SemanticPlanClickResult("stale"));
            _clicked = true;
            return Task.FromResult(new SemanticPlanClickResult("applied", "fixture"));
        }
    }

    private static void Reject(JsonObject plan, string expected)
    {
        try { PlanProtocolValidator.Parse(plan); }
        catch (ArgumentException ex)
        {
            Eq(expected, ex.Message, "validator");
            return;
        }
        throw new Exception("ARCHFLOW_WAIT accepted invalid plan " + expected);
    }

    public static async Task RunAsync()
    {
        var runner = new LocalPlanExecutor();
        var delayed = new Fake();
        var success = await runner.ExecuteAsync(PlanProtocolValidator.Parse(Json()), delayed);
        Eq("completed", success.Status, "delayed status");
        Eq(1, delayed.ClickCount, "exactly one navigation");
        Eq("observed", success.Nodes[^1].Status, "delayed postcondition");
        Eq(true, delayed.ReadCount >= 5, "local polling occurred");

        var never = new Fake { Mode="never" };
        var timeout = await runner.ExecuteAsync(PlanProtocolValidator.Parse(Json(150)), never);
        Eq("needs_replan", timeout.Status, "timeout status");
        Eq("wait_timeout", timeout.Reason, "timeout reason");
        Eq(1, never.ClickCount, "timeout no replay");

        var ambiguous = new Fake { Mode="ambiguous" };
        var result = await runner.ExecuteAsync(PlanProtocolValidator.Parse(Json()), ambiguous);
        Eq("needs_replan", result.Status, "ambiguous status");
        Eq("wait_ambiguous", result.Reason, "ambiguous reason");
        Eq(1, ambiguous.ClickCount, "ambiguous no replay");

        var changed = new Fake { Mode="host_changes" };
        result = await runner.ExecuteAsync(PlanProtocolValidator.Parse(Json()), changed);
        Eq("host_changed", result.Reason, "changed host");
        Eq(1, changed.ClickCount, "changed host no replay");

        var alreadyVisible = new Fake { Mode="initial_visible" };
        result = await runner.ExecuteAsync(
            PlanProtocolValidator.Parse(Json(withClick:false)), alreadyVisible);
        Eq("completed", result.Status, "visible immediate");
        Eq(0, alreadyVisible.ClickCount, "read-only wait");

        Reject(Json(0), "plan_invalid_timeout_ms");
        Reject(Json(99), "plan_invalid_timeout_ms");
        Reject(Json(5001), "plan_invalid_timeout_ms");
        var stringTimeout = Json();
        stringTimeout["nodes"]![2]!["timeout_ms"] = "200";
        Reject(stringTimeout, "plan_invalid_timeout_ms");
        var extra = Json();
        extra["nodes"]![2]!["javascript"] = "alert(1)";
        Reject(extra, "plan_unknown_field_javascript");

        var noPost = Json();
        noPost["nodes"]!.AsArray().RemoveAt(2);
        Reject(noPost, "plan_missing_postcondition");
        Console.WriteLine($"ARCHFLOW_WAIT_{_checks}_CHECKS_OK");
    }
}
