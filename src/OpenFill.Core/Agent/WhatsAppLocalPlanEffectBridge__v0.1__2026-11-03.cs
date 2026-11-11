// OF-000020: opt-in WhatsApp effect bridge, not routed by production AgentRunner.
// A verified result means a new, exact-text outgoing DOM message, not delivery.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using OpenFill.Core.Browser;

namespace OpenFill.Core.Agent;

public interface IWhatsAppEffectPort
{
    Task<bool> AccountMatchesAsync(string expectedAccount, CancellationToken ct);
    Task<JsonNode?> SendAsync(string target, string message, CancellationToken ct);
}

public sealed class WhatsAppBrowserEffectPort(WhatsAppBrowser browser) : IWhatsAppEffectPort
{
    public Task<bool> AccountMatchesAsync(string account, CancellationToken ct) =>
        browser.AccountMatchesAsync(account, ct);

    public Task<JsonNode?> SendAsync(string target, string message, CancellationToken ct) =>
        browser.SendAsync(target, message, ct);
}

public sealed class WhatsAppLocalPlanEffectBridge(
    IWhatsAppEffectPort port, OperationalMemoryStore memory, string evidenceDirectory)
{
    private readonly LocalPlanEffectJournal _journal = new(memory);

    public async Task<LocalPlanEffectExecutionResult> SendAsync(
        LocalPlanEffectKey key, string message, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (key.Provider != "whatsapp" || key.Action != "send" ||
            key.EffectId == "default" || string.IsNullOrWhiteSpace(evidenceDirectory) ||
            string.IsNullOrWhiteSpace(key.AccountId) ||
            key.AccountId.Where(char.IsAsciiDigit).Count() is < 8 or > 20 ||
            WhatsAppBrowser.DirectUrl(key.TargetId) is not { } route ||
            !string.Equals(route.Split("phone=", StringSplitOptions.None)[^1],
                key.TargetId, StringComparison.Ordinal))
            throw new ArgumentException("whatsapp_effect_scope_invalid");

        var sha = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(message)));
        if (message.Length == 0 ||
            !string.Equals(sha, key.PayloadSha256, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("whatsapp_effect_payload_mismatch");

        // Fail before the durable attempt if the logged-in self phone cannot be read.
        // This preflight MUST NOT inspect the recipient's name as a substitute.
        bool accountVerified;
        try { accountVerified = await port.AccountMatchesAsync(key.AccountId, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            return new("blocked_preflight", "", "not_planned",
                "whatsapp_sender_check_" + ex.GetType().Name, null);
        }
        if (!accountVerified)
            return new("blocked_preflight", "", "not_planned",
                "whatsapp_sender_unverified", null);

        var entry = _journal.Prepare(key, message);
        if (entry.State != "planned" || !_journal.TryBeginAttempt(entry.OperationId))
        {
            var previous = _journal.Read(entry.OperationId);
            return new("already_recorded", entry.OperationId, previous.State,
                "whatsapp_effect_previously_attempted", null);
        }

        // The SQLite attempted transition is committed BEFORE any browser send.
        try
        {
            var result = await port.SendAsync(key.TargetId, message, ct);
            if (result?["status"]?.ToString() != "sent_ui" ||
                result["confirmation"]?.ToString() != "new_outgoing_bubble_visible" ||
                result["view"]?["messages"] is not JsonArray)
            {
                _journal.MarkUncertain(entry.OperationId, "whatsapp_outgoing_unconfirmed");
                return Current(entry.OperationId, "whatsapp_outgoing_unconfirmed");
            }

            // Evidence is a durable on-disk UI observation. It is NOT a delivery receipt.
            Directory.CreateDirectory(evidenceDirectory);
            var file = Path.Combine(evidenceDirectory,
                "whatsapp_effect_" + Guid.NewGuid().ToString("N") + ".json");
            var temporary = file + ".tmp";
            var evidence = new JsonObject {
                ["operation_id"] = entry.OperationId,
                ["provider"] = "whatsapp",
                ["account_id"] = key.AccountId,
                ["target_id"] = key.TargetId,
                ["payload_sha256"] = sha,
                ["observed_utc"] = DateTimeOffset.UtcNow.ToString("O"),
                ["evidence_type"] = "new_outgoing_bubble_visible_not_delivery",
                ["ui_result"] = result.DeepClone()
            };
            await File.WriteAllTextAsync(temporary, evidence.ToJsonString(), ct);
            File.Move(temporary, file);
            if (_journal.Verify(entry.OperationId, file, route))
                return new("verified", entry.OperationId, "verified", null, file);
            return Current(entry.OperationId, "whatsapp_journal_verify_failed");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _journal.MarkUncertain(entry.OperationId, "whatsapp_effect_cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _journal.MarkUncertain(entry.OperationId,
                "whatsapp_effect_exception_" + ex.GetType().Name);
            return Current(entry.OperationId, "whatsapp_effect_exception");
        }
    }

    private LocalPlanEffectExecutionResult Current(string id, string reason)
    {
        var state = _journal.Read(id);
        return new("uncertain_effect", id, state.State, reason, null);
    }
}
