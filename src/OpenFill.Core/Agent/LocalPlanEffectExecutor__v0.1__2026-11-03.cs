// OF-000016: journaled DEV effect boundary. No production browser adapter.
namespace OpenFill.Core.Agent;

public sealed record LocalPlanEffectAttemptResult(string Status, string? Detail = null);

public interface ILocalPlanEffectAdapter
{
    // Must attempt the external change at most once for a given operation.
    // "applied" is NOT evidence that the external state has the target value.
    Task<LocalPlanEffectAttemptResult> AttemptAsync(LocalPlanEffectKey key, CancellationToken ct);

    // Read independent postcondition evidence. Null means not verified.
    Task<string?> ObserveAsync(LocalPlanEffectKey key, CancellationToken ct);
}

public sealed record LocalPlanEffectExecutionResult(
    string Status, string OperationId, string JournalState, string? Reason,
    string? EvidenceRef);

// Deliberately separated from navigation-only LocalPlanExecutor and LIVE.
// Only single-effect business operations; multi-effect scope requires a node key.
public sealed class LocalPlanEffectExecutor
{
    private readonly LocalPlanEffectJournal _journal;
    public LocalPlanEffectExecutor(LocalPlanEffectJournal journal) => _journal = journal;

    public async Task<LocalPlanEffectExecutionResult> ExecuteAsync(
        LocalPlanEffectKey key, ILocalPlanEffectAdapter adapter, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var prepared = _journal.Prepare(key);
        if (prepared.State != "planned" || !_journal.TryBeginAttempt(prepared.OperationId))
        {
            var current = _journal.Read(prepared.OperationId);
            return new("already_recorded", current.OperationId, current.State,
                "effect_previously_attempted", null);
        }

        // "attempted" was durably committed before any adapter invocation.
        try
        {
            var result = await adapter.AttemptAsync(key, ct);
            if (result.Status != "applied")
            {
                _journal.MarkUncertain(prepared.OperationId,
                    "effect_adapter_" + result.Status);
                return Current("uncertain_effect", prepared.OperationId,
                    "effect_adapter_" + result.Status);
            }

            string? evidence;
            try { evidence = await adapter.ObserveAsync(key, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _journal.MarkUncertain(prepared.OperationId,
                    "effect_observation_" + ex.GetType().Name);
                return Current("uncertain_effect", prepared.OperationId,
                    "effect_observation_failed");
            }

            if (string.IsNullOrWhiteSpace(evidence))
            {
                _journal.MarkUncertain(prepared.OperationId, "effect_not_observed");
                return Current("uncertain_effect", prepared.OperationId, "effect_not_observed");
            }
            if (!_journal.Verify(prepared.OperationId, evidence))
                return Current("uncertain_effect", prepared.OperationId, "journal_verify_failed");
            return new("verified", prepared.OperationId, "verified", null, evidence);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _journal.MarkUncertain(prepared.OperationId, "effect_cancelled_after_attempt");
            throw;
        }
        catch (Exception ex)
        {
            _journal.MarkUncertain(prepared.OperationId,
                "effect_exception_" + ex.GetType().Name);
            return Current("uncertain_effect", prepared.OperationId,
                "effect_adapter_exception");
        }
    }

    private LocalPlanEffectExecutionResult Current(string status, string id, string reason)
    {
        var current = _journal.Read(id);
        return new(status, id, current.State, reason, null);
    }
}
