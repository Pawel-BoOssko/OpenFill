using System.Security.Cryptography;
using System.Text;
using OpenFill.Core.Agent;

internal static class LocalPlanEffectExecutorSmoke
{
    private static int _checks;
    private static void Eq<T>(T expected, T actual, string label)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"EFFECT_EXECUTOR {label}: expected {expected}, got {actual}");
        _checks++;
    }

    private sealed class Fake(string behavior) : ILocalPlanEffectAdapter
    {
        public int Attempts, Observations;
        public Task<LocalPlanEffectAttemptResult> AttemptAsync(
            LocalPlanEffectKey key, CancellationToken ct)
        {
            Attempts++;
            if (behavior == "throw") throw new IOException("broken fixture");
            return Task.FromResult(new LocalPlanEffectAttemptResult(
                behavior == "rejected" ? "failed_before_action" : "applied"));
        }
        public Task<string?> ObserveAsync(LocalPlanEffectKey key, CancellationToken ct)
        {
            Observations++;
            if (behavior == "missing") return Task.FromResult<string?>(null);
            return Task.FromResult<string?>("fixture:postcondition:" + key.TargetId);
        }
    }

    public static async Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(),"of_effect_executor_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var db = Path.Combine(root, "operations.db");
            var key = new LocalPlanEffectKey("bo-1","task-1","run-1",
                "fixture","account","target-1","save",
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("hello"))));
            var journal = new LocalPlanEffectJournal(new OperationalMemoryStore(db));
            var runner = new LocalPlanEffectExecutor(journal);
            var good = new Fake("ok");
            var verified = await runner.ExecuteAsync(key, good);
            Eq("verified", verified.Status, "verified");
            Eq("verified", verified.JournalState, "state");
            Eq("fixture:postcondition:target-1", verified.EvidenceRef, "real evidence");
            Eq(1, good.Attempts, "exactly one attempt");
            Eq(1, good.Observations, "independent observation");

            var repeated = await runner.ExecuteAsync(key with {TaskId="task-2",RunId="run-2"},good);
            Eq("already_recorded", repeated.Status, "same process retry denied");
            Eq(1, good.Attempts, "no duplicate action");

            // The state is on disk: even after store reconstruction, no attempt.
            var restarted = new LocalPlanEffectExecutor(
                new LocalPlanEffectJournal(new OperationalMemoryStore(db)));
            repeated = await restarted.ExecuteAsync(key, good);
            Eq("already_recorded", repeated.Status, "restart retry denied");
            Eq(1, good.Attempts, "restart no duplicate action");

            var missing = new Fake("missing");
            var uncertain = await runner.ExecuteAsync(
                key with {BusinessOperationId="bo-2"},missing);
            Eq("uncertain_effect", uncertain.Status, "postcondition missing");
            Eq("uncertain", uncertain.JournalState, "uncertain persisted");
            Eq(1, missing.Attempts, "only one uncertain attempt");
            repeated = await restarted.ExecuteAsync(key with {BusinessOperationId="bo-2"},missing);
            Eq("already_recorded", repeated.Status, "uncertain retry denied");
            Eq(1, missing.Attempts, "uncertain no replay");

            var throwing = new Fake("throw");
            uncertain = await runner.ExecuteAsync(
                key with {BusinessOperationId="bo-3"},throwing);
            Eq("uncertain_effect", uncertain.Status, "exception marked uncertain");
            Eq("uncertain", uncertain.JournalState, "exception persisted");
            Eq(0, throwing.Observations, "exception no false proof");

            var rejected = new Fake("rejected");
            uncertain = await runner.ExecuteAsync(
                key with {BusinessOperationId="bo-4"},rejected);
            Eq("uncertain_effect", uncertain.Status, "rejected no retry");
            Eq(0, rejected.Observations, "rejected no observation");

            // Two distinct immutable effects under ONE business operation.
            var firstKey = key with {
                BusinessOperationId="business-multi", EffectId="node-save-a",
                TargetId="target-a"
            };
            var secondKey = key with {
                BusinessOperationId="business-multi", EffectId="node-save-b",
                TargetId="target-b"
            };
            var a = new Fake("ok");
            var b = new Fake("ok");
            var first = await runner.ExecuteAsync(firstKey, a);
            var second = await runner.ExecuteAsync(secondKey, b);
            Eq("verified", first.Status, "multi_first_verified");
            Eq("verified", second.Status, "multi_second_verified");
            Eq(false, first.OperationId == second.OperationId, "multi_distinct_operations");
            Eq(1, a.Attempts, "multi_first_once");
            Eq(1, b.Attempts, "multi_second_once");
            var resumedA = await restarted.ExecuteAsync(firstKey, a);
            var resumedB = await restarted.ExecuteAsync(secondKey, b);
            Eq("already_recorded", resumedA.Status, "multi_first_no_repeat_after_restart");
            Eq("already_recorded", resumedB.Status, "multi_second_no_repeat_after_restart");
            Eq(1, a.Attempts, "multi_first_still_once");
            Eq(1, b.Attempts, "multi_second_still_once");
            try
            {
                journal.Prepare(firstKey with {TargetId="different-target"});
                throw new Exception("EFFECT_EXECUTOR changed payload under effect id accepted");
            }
            catch (InvalidOperationException ex) when (
                ex.Message == "local_plan_business_operation_conflict") { _checks++; }
            try
            {
                journal.Prepare(firstKey with {EffectId=" "});
                throw new Exception("EFFECT_EXECUTOR whitespace effect id accepted");
            }
            catch (ArgumentException ex) when (
                ex.Message == "local_plan_effect_id_invalid") { _checks++; }

            Console.WriteLine($"ARCHFLOW_EFFECT_EXECUTOR_{_checks}_CHECKS_OK");
        }
        finally
        {
            try { Directory.Delete(root,recursive:true); } catch (IOException) {}
        }
    }
}
