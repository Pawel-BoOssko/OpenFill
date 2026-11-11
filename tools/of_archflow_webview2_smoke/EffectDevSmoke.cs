// OF-000017: two distinct durable effects in one business operation,
// independently observed in a real embedded WebView2 localhost page.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using OpenFill.App;
using OpenFill.Core.Agent;

internal static class EffectDevSmoke
{
    private static int _checks;
    private static void Check(bool ok, string reason)
    {
        if (!ok) throw new InvalidOperationException("EFFECT_WEBVIEW2_" + reason);
        _checks++;
    }

    public static async Task RunAsync(WebView2 view, string origin, string profile)
    {
        var arrived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<CoreWebView2NavigationCompletedEventArgs>? onNavigation = null;
        onNavigation = (_, e) =>
        {
            view.CoreWebView2.NavigationCompleted -= onNavigation;
            arrived.TrySetResult(e.IsSuccess);
        };
        view.CoreWebView2.NavigationCompleted += onNavigation;
        view.CoreWebView2.Navigate(origin + "/effects");
        Check(await arrived.Task.WaitAsync(TimeSpan.FromSeconds(10)), "fixture_opened");

        var adapter = new LocalPlanWebView2EffectDevAdapter(view, new Uri(origin + "/"));
        var database = Path.Combine(profile, "effect-dev.db");
        var journal = new LocalPlanEffectJournal(new OperationalMemoryStore(database));
        var executor = new LocalPlanEffectExecutor(journal);
        LocalPlanEffectKey Key(string target) =>
            new("business-017","task-017","run-017","of-local-fixture","dev-session",
                target,"save",
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("value-" + target))),
                "save-" + target);

        var a = await executor.ExecuteAsync(Key("a"), adapter);
        Check(a.Status == "verified", "a_verified_" + a.Reason);
        Check(a.JournalState == "verified", "a_durable_state");
        Check(a.EvidenceRef == "local-effect:a:count=1", "a_observation_ref");

        var b = await executor.ExecuteAsync(Key("b"), adapter);
        Check(b.Status == "verified", "b_verified_" + b.Reason);
        Check(b.JournalState == "verified", "b_durable_state");
        Check(b.EvidenceRef == "local-effect:b:count=1", "b_observation_ref");
        Check(a.OperationId != b.OperationId, "same_business_distinct_effect_ids");

        var resumed = new LocalPlanEffectExecutor(new LocalPlanEffectJournal(
            new OperationalMemoryStore(database)));
        var resumedA = await resumed.ExecuteAsync(Key("a") with {
            TaskId="task-restart", RunId="run-restart"
        }, adapter);
        var resumedB = await resumed.ExecuteAsync(Key("b"), adapter);
        Check(resumedA.Status == "already_recorded", "a_restart_no_repeat");
        Check(resumedB.Status == "already_recorded", "b_restart_no_repeat");

        // Independent live DOM read: do not trust the journal's state to
        // prove the actual browser click count.
        var raw = await view.ExecuteScriptAsync("""
            (() => [document.getElementById('count-a')?.dataset.count,
                     document.getElementById('count-b')?.dataset.count,
                     document.getElementById('result-a')?.textContent,
                     document.getElementById('result-b')?.textContent])()
            """);
        var values = JsonSerializer.Deserialize<string[]>(raw);
        Check(values is {Length:4}, "dom_array_available");
        Check(values![0] == "1" && values[1] == "1", "actual_one_click_per_effect");
        Check(values[2] == "Saved a" && values[3] == "Saved b",
            "actual_success_text");

        try
        {
            journal.Prepare(Key("a") with {
                PayloadSha256 = new string('A',64)
            });
            throw new InvalidOperationException("EFFECT_WEBVIEW2_CHANGED_PAYLOAD_ACCEPTED");
        }
        catch (InvalidOperationException e) when (
            e.Message == "local_plan_business_operation_conflict") { _checks++; }

        Console.WriteLine("ARCHFLOW_EFFECT_WEBVIEW2_METRICS " +
            JsonSerializer.Serialize(new {
                businessOperationId="business-017",
                effectIds=new[]{"save-a","save-b"},
                operationIds=new[]{a.OperationId,b.OperationId},
                aState=a.JournalState,bState=b.JournalState,
                aEvidence=a.EvidenceRef,bEvidence=b.EvidenceRef,
                independentDomCounts=new[]{values[0],values[1]},
                modelCalls=0
            }));
        Console.WriteLine("ARCHFLOW_EFFECT_WEBVIEW2_" + _checks + "_CHECKS_OK");
    }
}
