using System.Text.Json.Nodes;
using OpenFill.Core.Agent;

internal static class ProcedureLearningSmoke
{
    public static void Run()
    {
        var dir = Path.Combine(Path.GetTempPath(), "of_procedure_learning_" +
            Guid.NewGuid().ToString("N"));
        var source = ProcedureCatalog.FromEmbedded().Get("whatsapp.send_batch")!;
        var store = new ProcedureLearningStore(dir);
        var count = 0;
        void Check(bool ok, string label)
        {
            if (!ok) throw new Exception("E02_" + label);
            count++;
        }
        JsonArray Complete(string proof) => new(new JsonObject {
            ["state"] = "verified", ["executed"] = true,
            ["evidence_ref"] = proof, ["target_id"] = "example"
        });
        JsonArray Incomplete() => new(new JsonObject {
            ["state"] = "uncertain", ["executed"] = true,
            ["evidence_ref"] = (string?)null
        });
        JsonArray Failed() => new(new JsonObject {
            ["state"] = "failed", ["executed"] = true
        });
        try
        {
            Check(store.Read(source.Id, source.Version) is null, "no_previous_evidence");
            var first = store.ObserveBatch(source, "batch-one", Complete("evidence-one"));
            Check(first.Status == "candidate" && first.VerifiedBatches == 1,
                "single_success_not_promoted");
            var duplicate = store.ObserveBatch(source, "batch-one", Complete("evidence-one"));
            Check(duplicate.VerifiedBatches == 1, "duplicate_batch_idempotent");
            var intermediate = store.ObserveBatch(source, "batch-partial", Incomplete());
            Check(intermediate.Status == "candidate" && intermediate.FailedBatches == 0, "unknown_not_failure");
            var second = store.ObserveBatch(source, "batch-two", Complete("evidence-two"));
            Check(second.Status == "verified" && second.VerifiedBatches == 2, "two_proven_batches_promote");
            var selected = ProcedureCatalog.FromEmbedded(dir).Get(source.Id)!;
            Check(selected.Status == "verified", "catalog_reads_verified_evidence");
            var text = File.ReadAllText(Directory.EnumerateFiles(dir, "*.json").Single());
            Check(!text.Contains("evidence-one") && !text.Contains("evidence-two"), "evidence_reference_not_stored");
            var failDir = Path.Combine(dir, "failure");
            var failStore = new ProcedureLearningStore(failDir);
            failStore.ObserveBatch(source, "failure-one", Failed());
            failStore.ObserveBatch(source, "failure-two", Failed());
            var suspended = failStore.ObserveBatch(source, "failure-three", Failed());
            Check(suspended.Status == "suspended" &&
                suspended.SuspendReason == "three_proven_failed_batches", "three_proven_failures_suspend");
            failStore.ObserveBatch(source, "late-success-a", Complete("evidence-late-a"));
            failStore.ObserveBatch(source, "late-success-b", Complete("evidence-late-b"));
            Check(failStore.Read(source.Id, source.Version)!.Status == "suspended",
                "suspension_not_auto_lifted");
            Check(ProcedureCatalog.FromEmbedded(failDir).Get(source.Id)!.Status ==
                "suspended", "catalog_refuses_suspended");
            Console.WriteLine("E02_PROCEDURE_LEARNING_" + count + "_CHECKS_OK");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}
