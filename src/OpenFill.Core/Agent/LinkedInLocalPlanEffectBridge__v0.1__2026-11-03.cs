// OF-000019: opt-in provider bridge for journaled LinkedIn SEND effects.
// Not registered in production AgentRunner/ToolRegistry/MCP. No automatic retry.
// Reuses the existing LinkedInCurrentThreadSendAdapter's exact-thread
// preflight, real browser actions and persisted before/after DOM evidence.
using System.Security.Cryptography;
using System.Text;
using OpenFill.Core.Browser;

namespace OpenFill.Core.Agent;

public sealed class LinkedInLocalPlanEffectBridge(
    BrowserController browser, OperationalMemoryStore memory, string evidenceDirectory)
{
    private readonly LocalPlanEffectJournal _journal = new(memory);

    public async Task<LocalPlanEffectExecutionResult> SendCurrentAsync(
        LocalPlanEffectKey key, string expectedSenderProfile,
        string expectedThreadUrl, string expectedTargetProfile,
        string message, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (key.Provider != "linkedin" || key.Action != "send" ||
            key.AccountId != expectedSenderProfile ||
            key.TargetId != expectedTargetProfile ||
            key.EffectId == "default" ||
            LinkedInSendBatchService.Validate(expectedSenderProfile,
                [new LinkedInSendTarget(expectedTargetProfile, expectedThreadUrl, message)]) is not null)
            throw new ArgumentException("linkedin_effect_scope_invalid");

        if (Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(message))) !=
            key.PayloadSha256.ToUpperInvariant())
            throw new ArgumentException("linkedin_effect_payload_mismatch");

        // Persist the exact plaintext payload for the existing provider adapter:
        // SendCurrentAsync validates OperationPayload against what it will type.
        var entry = _journal.Prepare(key, message);
        if (entry.State != "planned")
        {
            return new("already_recorded", entry.OperationId, entry.State,
                "linkedin_effect_previously_attempted", null);
        }

        // OF-000022: inspect the exact current authenticated thread BEFORE
        // consuming the at-most-once attempt. A missing/login/stale view may
        // be corrected without creating an uncertain send that never happened.
        // The provider adapter repeats this check after attempted, before click.
        try
        {
            var gate = await new LinkedInThreadPreflight(browser).ObserveAndCheckAsync(
                expectedSenderProfile, expectedThreadUrl, expectedTargetProfile, ct);
            if (gate["status"]?.ToString() != "ready")
                return new("blocked_preflight", entry.OperationId, "planned",
                    "linkedin_preflight_" + (gate["reason_code"]?.ToString() ?? "blocked"), null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            return new("blocked_preflight", entry.OperationId, "planned",
                "linkedin_preflight_" + ex.GetType().Name, null);
        }

        if (!_journal.TryBeginAttempt(entry.OperationId))
        {
            var old = _journal.Read(entry.OperationId);
            return new("already_recorded", old.OperationId, old.State,
                "linkedin_effect_previously_attempted", null);
        }

        ProcedureActionResult action;
        try
        {
            action = await new LinkedInCurrentThreadSendAdapter(browser, memory)
                .SendCurrentAsync(entry.OperationId, expectedSenderProfile,
                    expectedThreadUrl, expectedTargetProfile, message, evidenceDirectory, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _journal.MarkUncertain(entry.OperationId, "linkedin_effect_cancelled");
            throw;
        }
        catch (Exception e)
        {
            _journal.MarkUncertain(entry.OperationId,
                "linkedin_effect_exception_" + e.GetType().Name);
            return Current(entry.OperationId, "linkedin_effect_exception");
        }

        // LinkedInCurrentThreadSendAdapter reports verified ONLY after
        // observed outbound count increase, exact-thread recheck and atomic
        // evidence-file persistence. This proves UI outgoing, NOT delivery.
        if (action.Status == "verified" && !string.IsNullOrWhiteSpace(action.EvidenceRef)
            && _journal.Verify(entry.OperationId, action.EvidenceRef, action.ExternalRef))
            return new("verified", entry.OperationId, "verified", null, action.EvidenceRef);

        _journal.MarkUncertain(entry.OperationId,
            "linkedin_effect_" + (action.Reason ?? action.Status));
        return Current(entry.OperationId, "linkedin_effect_unverified");
    }

    private LocalPlanEffectExecutionResult Current(string id, string reason)
    {
        var state = _journal.Read(id);
        return new("uncertain_effect", id, state.State, reason, null);
    }
}
