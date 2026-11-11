// OF-ARCHFLOW: persistent, at-most-once boundary for future local-plan effects.
// Deliberately NOT connected to the navigation-only executor or LIVE.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OpenFill.Core.Agent;

public sealed record LocalPlanEffectKey(
    string BusinessOperationId,
    string TaskId,
    string RunId,
    string Provider,
    string AccountId,
    string TargetId,
    string Action,
    string PayloadSha256,
    string EffectId = "default");

public sealed record LocalPlanEffectRecord(string OperationId, string State);

public sealed class LocalPlanEffectJournal
{
    private readonly OperationalMemoryStore _store;

    // Reuse OpenFill's SQLite WAL operation ledger rather than creating a second,
    // unsynchronized source of truth for external side effects.
    public LocalPlanEffectJournal(OperationalMemoryStore store) => _store = store;

    public LocalPlanEffectRecord Prepare(LocalPlanEffectKey key, string? payload = null)
    {
        Validate(key);
        if (payload is not null)
        {
            var actualHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
            if (!string.Equals(actualHash, key.PayloadSha256,
                StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("local_plan_effect_payload_mismatch");
        }
        // Legacy default keeps its exact stable request_id and fingerprint.
        // Explicit effect IDs get independent batches under the same business goal.
        // Hash the full typed scope to avoid delimiter collisions.
        var requestId = key.EffectId == "default" ? key.BusinessOperationId
            : "of-effect-v1:" + Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {
                    key.Provider, key.AccountId, key.BusinessOperationId, key.EffectId
                }))));
        var batch = _store.BeginBatch(
            key.Provider, key.AccountId, requestId,
            "local_plan_effect", "v1", key.TaskId, "local_plan", key.RunId);
        // Scope immutable business operation to account/target/action/exact payload.
        // TaskId and RunId are telemetry correlation, not deduplication keys.
        var identity = key.EffectId == "default"
            ? JsonSerializer.Serialize(new {
                key.Provider, key.AccountId, key.TargetId,
                key.Action, payload_sha256 = key.PayloadSha256.ToUpperInvariant()
            })
            : JsonSerializer.Serialize(new {
                key.Provider, key.AccountId, key.BusinessOperationId, key.EffectId,
                key.TargetId, key.Action,
                payload_sha256 = key.PayloadSha256.ToUpperInvariant()
            });
        var fingerprint = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        if (!_store.BindRequestFingerprint(batch, fingerprint))
            throw new InvalidOperationException("local_plan_business_operation_conflict");
        var operation = _store.PlanOperation(
            batch, key.TargetId, key.Action, key.PayloadSha256.ToUpperInvariant(),
            payload);
        return new(operation, _store.OperationState(operation) ?? "missing");
    }

    // Must be durably recorded BEFORE calling a browser adapter that can save/send.
    // A second call, including after process restart, may NEVER authorize retry.
    public bool TryBeginAttempt(string operationId)
    {
        RequireId(operationId);
        if (_store.OperationState(operationId) != "planned")
            return false;
        return _store.MarkAttempted(operationId); // conditional SQLite update
    }

    public bool Verify(string operationId, string evidenceRef, string? externalRef = null)
    {
        RequireId(operationId);
        if (string.IsNullOrWhiteSpace(evidenceRef))
            throw new ArgumentException("local_plan_verification_requires_evidence");
        if (_store.OperationState(operationId) != "attempted")
            return false;
        return _store.MarkOutcome(operationId, "verified", evidenceRef, externalRef, null);
    }

    // The action might already have been applied. Never turn it back to planned.
    public bool MarkUncertain(string operationId, string reason)
    {
        RequireId(operationId);
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("local_plan_uncertain_requires_reason");
        if (_store.OperationState(operationId) != "attempted")
            return false;
        return _store.MarkOutcome(operationId, "uncertain", null, null, reason);
    }

    public LocalPlanEffectRecord Read(string operationId)
    {
        RequireId(operationId);
        return new(operationId, _store.OperationState(operationId) ?? "missing");
    }

    private static void RequireId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("local_plan_operation_id_required");
    }

    private static void Validate(LocalPlanEffectKey key)
    {
        foreach (var part in new[] {
            key.BusinessOperationId, key.TaskId, key.RunId, key.Provider,
            key.AccountId, key.TargetId, key.Action
        })
            if (string.IsNullOrWhiteSpace(part) || part.Length > 256)
                throw new ArgumentException("local_plan_effect_identity_invalid");
        if (key.PayloadSha256.Length != 64 ||
            !key.PayloadSha256.All(Uri.IsHexDigit))
            throw new ArgumentException("local_plan_effect_payload_hash_invalid");
        if (string.IsNullOrWhiteSpace(key.EffectId) || key.EffectId.Length > 128 ||
            !string.Equals(key.EffectId, key.EffectId.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("local_plan_effect_id_invalid");
        if (key.Action is not ("send" or "save" or "submit"))
            throw new ArgumentException("local_plan_effect_action_unsupported");
    }
}
