// OF-000018: real WebView2 DEV plan with read-only branch and two
// journaled effects. All browser actions are on localhost fixture.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using OpenFill.App;
using OpenFill.Core.Agent;

internal static class EffectPlanDevSmoke
{
    private static int _checks;
    private static void Check(bool v, string name)
    {
        if (!v) throw new InvalidOperationException("EFFECT_PLAN_DEV_" + name);
        _checks++;
    }

    private sealed class MissingEvidenceAdapter(ILocalPlanEffectAdapter original)
        : ILocalPlanEffectAdapter
    {
        public int Attempts { get; private set; }
        public async Task<LocalPlanEffectAttemptResult> AttemptAsync(
            LocalPlanEffectKey key, CancellationToken ct)
        {
            Attempts++;
            return await original.AttemptAsync(key, ct);
        }
        public Task<string?> ObserveAsync(LocalPlanEffectKey key, CancellationToken ct)
            => Task.FromResult<string?>(null);
    }

    private static JsonObject Plan(bool includeBoth = true)
    {
        static string Hash(string target) => Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes("value-" + target)));
        JsonObject Node(string target) => new() {
            ["id"] = "apply-" + target,
            ["op"] = "effect",
            ["effect_id"] = "save-" + target,
            ["target_id"] = target,
            ["action"] = "save",
            ["payload_sha256"] = Hash(target),
            ["postcondition"] = new JsonObject {
                ["type"] = "adapter_evidence",
                ["expected"] = "local-effect:" + target + ":count=1"
            }
        };
        var nodes = new JsonArray(
            JsonNode.Parse("""
              {"id":"check-page","op":"if","role":"button","name":"Save a",
               "then":[{"id":"visible","op":"assert_visible","role":"button","name":"Save a"}],
               "else":[{"id":"missing","op":"assert_absent","role":"button","name":"Save a"}]}
              """),
            Node("a"));
        if (includeBoth) nodes.Add(Node("b"));
        return new JsonObject {
            ["protocol"] = "of.plan.v1",
            ["expected_host"] = "127.0.0.1",
            ["nodes"] = nodes
        };
    }

    private static async Task OpenFixtureAsync(WebView2 view, string origin)
    {
        var loaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<CoreWebView2NavigationCompletedEventArgs>? h = null;
        h = (_, e) => {
            view.CoreWebView2.NavigationCompleted -= h;
            loaded.TrySetResult(e.IsSuccess);
        };
        view.CoreWebView2.NavigationCompleted += h;
        view.CoreWebView2.Navigate(origin + "/effects");
        Check(await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10)), "fixture_loaded");
    }

    private static async Task<string[]> Counts(WebView2 view)
    {
        var js = await view.ExecuteScriptAsync("""
          (() => [document.getElementById('count-a')?.dataset.count,
                   document.getElementById('count-b')?.dataset.count])()
          """);
        return JsonSerializer.Deserialize<string[]>(js) ?? Array.Empty<string>();
    }

    public static async Task RunAsync(WebView2 view, string origin, string profile,
        ILocalPlanBrowser browser, AgentRunner agent)
    {
        await OpenFixtureAsync(view, origin);
        var adapter = new LocalPlanWebView2EffectDevAdapter(view, new Uri(origin + "/"));
        var db = Path.Combine(profile, "effect-plan-dev.db");
        var journal = new LocalPlanEffectJournal(new OperationalMemoryStore(db));
        var runner = new LocalPlanEffectPlanDevRunner();
        var plan = Plan();
        var invalid = plan.DeepClone().AsObject();
        invalid["nodes"]![2]!["payload_sha256"] = new string('F', 64);
        try
        {
            await runner.ExecuteAsync(invalid, browser, adapter, journal,
                "business-018", "task-018", "run-018");
            throw new InvalidOperationException("EFFECT_PLAN_DEV_BAD_HASH_ACCEPTED");
        }
        catch (ArgumentException e) when (
            e.Message == "dev_effect_fixture_payload_invalid") { _checks++; }
        var counts = await Counts(view);
        Check(counts.SequenceEqual(new[]{"0","0"}), "invalid_plan_no_effects");

        invalid = plan.DeepClone().AsObject();
        invalid["nodes"]![0]!["then"]![0]!["op"] = "save";
        try
        {
            await runner.ExecuteAsync(invalid, browser, adapter, journal,
                "business-018", "task-018", "run-018");
            throw new InvalidOperationException("EFFECT_PLAN_DEV_BRANCH_SIDE_EFFECT_ACCEPTED");
        }
        catch (ArgumentException e) when (
            e.Message == "flow_write_not_supported") { _checks++; }
        counts = await Counts(view);
        Check(counts.SequenceEqual(new[]{"0","0"}), "invalid_branch_no_effects");

        var result = await agent.RunEffectPlanDevAsync(plan, browser, adapter, journal,
            "business-018", "task-018", "run-018");
        Check(result.Status == "completed", "complete_" + result.Reason);
        Check(result.EffectAttempts == 2 && result.EffectVerified == 2,
            "both_effects_verified");
        Check(result.Effects.Count == 2 &&
              result.Effects.All(e => e.Status == "verified"),
              "journal_records_verified");
        Check(result.Effects[0].OperationId != result.Effects[1].OperationId,
              "distinct_operation_ids");
        Check(result.Effects[0].EvidenceRef == "local-effect:a:count=1" &&
              result.Effects[1].EvidenceRef == "local-effect:b:count=1",
              "evidence_matches_declared_postconditions");
        counts = await Counts(view);
        Check(counts.SequenceEqual(new[]{"1","1"}), "independent_dom_one_each");

        var resumed = new LocalPlanEffectPlanDevRunner();
        var resumedJournal = new LocalPlanEffectJournal(new OperationalMemoryStore(db));
        var repeated = await resumed.ExecuteAsync(plan, browser, adapter, resumedJournal,
            "business-018", "task-018-restart", "run-018-restart");
        Check(repeated.Status == "already_recorded" &&
            repeated.EffectAttempts == 0, "restart_cannot_replay");
        counts = await Counts(view);
        Check(counts.SequenceEqual(new[]{"1","1"}), "restart_no_second_click");

        // Missing postcondition evidence after a real click is UNCERTAIN, not
        // a reason to click again. A later effect must not start.
        await OpenFixtureAsync(view, origin);
        var uncertainAdapter = new MissingEvidenceAdapter(adapter);
        var uncertainPlan = Plan();
        var uncertain = await agent.RunEffectPlanDevAsync(uncertainPlan, browser,
            uncertainAdapter, journal, "business-018-uncertain", "task-u", "run-u");
        Check(uncertain.Status == "uncertain_effect" &&
            uncertain.Effects[0].JournalState == "uncertain",
            "missing_evidence_persisted_uncertain");
        Check(uncertainAdapter.Attempts == 1 && uncertain.Effects.Count == 1,
            "stop_before_second_effect");
        counts = await Counts(view);
        Check(counts.SequenceEqual(new[]{"1","0"}), "uncertain_real_click_only_first");
        repeated = await resumed.ExecuteAsync(uncertainPlan, browser,
            uncertainAdapter, new LocalPlanEffectJournal(new OperationalMemoryStore(db)),
            "business-018-uncertain", "task-u-restart", "run-u-restart");
        Check(repeated.Status == "already_recorded" &&
            uncertainAdapter.Attempts == 1, "uncertain_no_auto_retry");
        counts = await Counts(view);
        Check(counts.SequenceEqual(new[]{"1","0"}), "uncertain_dom_not_replayed");

        Console.WriteLine("ARCHFLOW_EFFECT_PLAN_DEV_METRICS " + JsonSerializer.Serialize(new {
            normalStatus=result.Status, normalEffectAttempts=result.EffectAttempts,
            normalEffectVerified=result.EffectVerified,
            normalOperationIds=result.Effects.Select(e=>e.OperationId).ToArray(),
            normalObservedProofs=result.Effects.Select(e=>e.EvidenceRef).ToArray(),
            repeatStatus="already_recorded", uncertainStatus=uncertain.Status,
            uncertainAttempts=uncertainAdapter.Attempts, modelCalls=0
        }));
        Console.WriteLine("ARCHFLOW_EFFECT_PLAN_DEV_" + _checks + "_CHECKS_OK");
    }
}
