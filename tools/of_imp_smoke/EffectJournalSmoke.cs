using OpenFill.Core.Agent;
using System.Security.Cryptography;
using System.Text;

internal static class EffectJournalSmoke
{
    private static void Eq<T>(T expected, T actual, string label)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"EFFECT_JOURNAL {label}: expected={expected}, actual={actual}");
    }

    public static void Run()
    {
        var folder = Path.Combine(Path.GetTempPath(), "of_effect_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, "ledger.db");
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("hello")));
            var key = new LocalPlanEffectKey(
                "business-001", "task-001", "run-001", "example", "account-1",
                "target-42", "send", hash);
            var ledger = new OperationalMemoryStore(path);
            var journal = new LocalPlanEffectJournal(ledger);
            var planned = journal.Prepare(key);
            Eq("planned", planned.State, "initial planned");
            Eq(planned.OperationId, journal.Prepare(key).OperationId, "same business operation");
            try
            {
                journal.Prepare(key with { PayloadSha256 = new string('A', 64) });
                throw new Exception("EFFECT_JOURNAL accepted modified payload");
            }
            catch (InvalidOperationException ex) when (ex.Message == "local_plan_business_operation_conflict") { }

            Eq(true, journal.TryBeginAttempt(planned.OperationId), "first durable attempt");
            Eq(false, journal.TryBeginAttempt(planned.OperationId), "same process repeat denied");

            // Recreate the store: no auto-retry after process restart.
            var resumed = new LocalPlanEffectJournal(new OperationalMemoryStore(path));
            Eq("attempted", resumed.Prepare(key with { TaskId = "task-002", RunId = "run-002" }).State,
                "same business operation across tasks");
            Eq(false, resumed.TryBeginAttempt(planned.OperationId), "restarted process retry denied");
            Eq(true, resumed.MarkUncertain(planned.OperationId, "browser lost connection"), "uncertain");
            Eq(false, resumed.TryBeginAttempt(planned.OperationId), "uncertain retry denied");
            Eq(false, resumed.Verify(planned.OperationId, "unexpected-proof"), "uncertain not auto-verified");

            var second = resumed.Prepare(key with { BusinessOperationId = "business-002" });
            Eq(true, resumed.TryBeginAttempt(second.OperationId), "independent operation attempt");
            Eq(true, resumed.Verify(second.OperationId, "observed-message-id", "provider-123"),
                "verification with evidence");
            Eq("verified", resumed.Read(second.OperationId).State, "verified persisted");
            Eq(false, resumed.TryBeginAttempt(second.OperationId), "verified retry denied");

            var linkedinText = "LinkedIn effect journal exact payload";
            var linkedinHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(linkedinText)));
            var linkedin = key with {
                Provider = "linkedin", AccountId = "https://www.linkedin.com/in/sender",
                BusinessOperationId = "business-linkedin", EffectId = "send-thread-1",
                TargetId = "https://www.linkedin.com/in/target",
                PayloadSha256 = linkedinHash
            };
            try
            {
                resumed.Prepare(linkedin, "wrong payload");
                throw new Exception("EFFECT_JOURNAL mismatched raw payload accepted");
            }
            catch (ArgumentException e) when (
                e.Message == "local_plan_effect_payload_mismatch") { }

            var preparedLinkedIn = resumed.Prepare(linkedin, linkedinText);
            Eq("planned", preparedLinkedIn.State, "provider payload planned");
            Eq(linkedinText, ledger.OperationPayload(preparedLinkedIn.OperationId),
                "provider original payload persisted");
            Eq(true, resumed.TryBeginAttempt(preparedLinkedIn.OperationId),
                "provider durable attempt before adapter");
            var openedAgain = new LocalPlanEffectJournal(new OperationalMemoryStore(path));
            Eq("attempted", openedAgain.Prepare(linkedin, linkedinText).State,
                "provider attempt persisted after restart");
            Eq(false, openedAgain.TryBeginAttempt(preparedLinkedIn.OperationId),
                "provider no second attempt");
            Eq(true, openedAgain.MarkUncertain(preparedLinkedIn.OperationId,
                "provider postcondition not observed"), "provider uncertain persisted");

            var events = ledger.OperationEvents(planned.OperationId);
            Eq(3, events.Count, "planned attempted uncertain audit trail");
            Eq("uncertain", events[2]!["state"]!.ToString(), "final event");
            Console.WriteLine("ARCHFLOW_EFFECT_JOURNAL_22_CHECKS_OK");
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch (IOException) { }
        }
    }
}
