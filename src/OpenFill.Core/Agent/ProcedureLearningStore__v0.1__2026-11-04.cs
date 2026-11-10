// E02 2026-11-04: durable, evidence-gated procedure lifecycle.
// Data only: never execute a candidate, never store messages, prompts or DOM.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OpenFill.Core.Agent;

public sealed class ProcedureLearningRecord
{
    public string ProcedureId { get; set; } = "";
    public string Version { get; set; } = "";
    public string Status { get; set; } = "candidate";
    public int VerifiedBatches { get; set; }
    public int FailedBatches { get; set; }
    public string? SuspendReason { get; set; }
    public string LastProofSha256 { get; set; } = "";
    public DateTime UpdatedUtc { get; set; }
    public List<string> CountedBatchIds { get; set; } = [];
}

public sealed class ProcedureLearningStore(string directory)
{
    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions Json = new()
    { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    private string FileFor(string id, string version)
    {
        var key = id + "@" + version;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        return Path.Combine(directory, hash + ".json");
    }

    public ProcedureLearningRecord? Read(string id, string version)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(version))
            return null;
        lock (Gate)
        {
            var file = FileFor(id, version);
            if (!File.Exists(file)) return null;
            try
            {
                var record = JsonSerializer.Deserialize<ProcedureLearningRecord>(
                    File.ReadAllText(file), Json);
                return record is not null && record.ProcedureId == id &&
                    record.Version == version && record.Status is
                    ("candidate" or "verified" or "suspended") ? record : null;
            }
            catch (JsonException) { return null; }
            catch (IOException) { return null; }
        }
    }

    // Only a deterministic executor may record this. "verified" requires an
    // independently observed per-item proof for EVERY item in TWO distinct batches.
    // A partial/unknown result can NEVER upgrade a candidate.
    public ProcedureLearningRecord ObserveBatch(ProcedureDefinition procedure,
        string batchId, JsonArray results)
    {
        if (string.IsNullOrWhiteSpace(procedure.Id) ||
            string.IsNullOrWhiteSpace(procedure.Version) ||
            string.IsNullOrWhiteSpace(batchId) || batchId.Length > 250 ||
            results.Count is < 1 or > 500)
            throw new ArgumentException("procedure_learning_invalid_batch");
        var verified = results.All(n => n is JsonObject o &&
            o["state"]?.ToString() == "verified" &&
            o["executed"]?.ToString() == "true" &&
            !string.IsNullOrWhiteSpace(o["evidence_ref"]?.ToString()));
        var failed = results.All(n => n is JsonObject o &&
            o["state"]?.ToString() == "failed" &&
            o["executed"]?.ToString() == "true");
        lock (Gate)
        {
            var file = FileFor(procedure.Id, procedure.Version);
            ProcedureLearningRecord record;
            try
            {
                record = File.Exists(file)
                    ? JsonSerializer.Deserialize<ProcedureLearningRecord>(
                        File.ReadAllText(file), Json) ?? new()
                    : new();
            }
            catch (JsonException) { record = new(); }
            if (record.ProcedureId != procedure.Id || record.Version != procedure.Version)
                record = new()
                {
                    ProcedureId = procedure.Id, Version = procedure.Version,
                    Status = procedure.Status
                };
            if (record.CountedBatchIds.Contains(batchId)) return record;
            if (record.CountedBatchIds.Count >= 1000)
                record.CountedBatchIds.RemoveAt(0);
            record.CountedBatchIds.Add(batchId);
            if (verified)
            {
                record.VerifiedBatches++;
                // Suspension is not cleared automatically by a later ambiguous run.
                if (record.Status != "suspended" && record.VerifiedBatches >= 2)
                    record.Status = "verified";
                var refs = string.Join("|", results.OfType<JsonObject>()
                    .Select(x => x["evidence_ref"]?.ToString()));
                record.LastProofSha256 = Convert.ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(refs)));
            }
            else if (failed)
            {
                record.FailedBatches++;
                if (record.FailedBatches >= 3)
                {
                    record.Status = "suspended";
                    record.SuspendReason = "three_proven_failed_batches";
                }
            }
            record.UpdatedUtc = DateTime.UtcNow;
            Directory.CreateDirectory(directory);
            var temp = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(record, Json));
            File.Move(temp, file, true);
            return record;
        }
    }

    // Explicit new version is needed to re-enable a suspended executable recipe.
    // No caller-provided text can become an executable adapter at runtime.
}
