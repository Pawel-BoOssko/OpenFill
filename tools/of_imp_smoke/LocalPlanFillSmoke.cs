using System.Text.Json.Nodes;
using OpenFill.Core.Agent;

internal static class LocalPlanFillSmoke
{
    private const string Valid = """
    {"protocol":"of.plan.v1","expected_host":"example.test","nodes":[
      {"id":"locate","op":"locate","role":"textbox","name":"Customer name","bind":"form"},
      {"id":"fill","op":"fill","ref":"$form","value":"Charlie"},
      {"id":"verify","op":"assert_value","ref":"$form","value":"Charlie"}
    ]}
    """;

    private static int _checks;
    private static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception("ARCHFLOW_FILL_" + label);
        _checks++;
    }

    private static JsonObject Source() => JsonNode.Parse(Valid)!.AsObject();

    private static void Reject(JsonObject source, string code)
    {
        try { PlanProtocolValidator.Parse(source); }
        catch (ArgumentException ex)
        {
            Check(ex.Message == code, "validator_" + code + "_" + ex.Message);
            return;
        }
        throw new Exception("ARCHFLOW_FILL accepted " + code);
    }

    private sealed class Fake(string mode = "ok") : ILocalPlanFormBrowser
    {
        public int Calls { get; private set; }
        private string _value = "Before";
        private long _revision = 1;

        public Task<SemanticPlanSnapshot> ReadAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(new SemanticPlanSnapshot(_revision, "example.test",
                new[] { new SemanticPlanElement("form", "textbox", "Customer name", true,
                    mode == "mismatch" && _revision > 1 ? "Wrong" : _value) }));
        }

        public Task<SemanticPlanClickResult> FillTextAsync(
            string elementRef, long expectedRevision, string value, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Calls++;
            if (elementRef != "form" || expectedRevision != _revision || mode == "stale")
                return Task.FromResult(new SemanticPlanClickResult("stale"));
            if (mode == "uncertain")
                return Task.FromResult(new SemanticPlanClickResult("uncertain"));
            _value = value;
            _revision++;
            return Task.FromResult(new SemanticPlanClickResult("applied", "fake-form"));
        }

        public Task<SemanticPlanClickResult> ClickNavigationAsync(string elementRef,
            long expectedRevision, CancellationToken ct)
            => throw new Exception("Unexpected navigation");
    }

    public static async Task RunAsync()
    {
        var plan = PlanProtocolValidator.Parse(Source());
        var runner = new LocalPlanExecutor();
        var success = new Fake();
        var result = await runner.ExecuteAsync(plan, success);
        Check(result.Status == "completed", "success");
        Check(success.Calls == 1, "exactly_one_fill");
        Check(result.Nodes[^1].Status == "observed", "value_confirmed");

        foreach (var (mode, expectedStatus, reason) in new [] {
            ("mismatch","needs_replan","value_mismatch"),
            ("stale","needs_replan","fill_stale"),
            ("uncertain","uncertain_effect","fill_uncertain")
        })
        {
            var browser = new Fake(mode);
            result = await runner.ExecuteAsync(plan, browser);
            Check(result.Status == expectedStatus && result.Reason == reason,
                mode + "_" + result.Status + "_" + result.Reason);
            Check(browser.Calls == 1, mode + "_no_retry");
        }

        var noAssert = Source();
        noAssert["nodes"]!.AsArray().RemoveAt(2);
        Reject(noAssert, "plan_missing_value_postcondition");
        var unsafeEffect = Source();
        unsafeEffect["nodes"]![1]!["effect"] = "send_message";
        Reject(unsafeEffect, "plan_invalid_fill_fields");
        var wrongBinding = Source();
        wrongBinding["nodes"]![1]!["ref"] = "$missing";
        Reject(wrongBinding, "plan_unknown_binding");
        var tooLarge = Source();
        tooLarge["nodes"]![1]!["value"] = new string('x',2001);
        Reject(tooLarge, "plan_invalid_value");

        Console.WriteLine($"ARCHFLOW_FILL_{_checks}_CHECKS_OK");
    }
}
