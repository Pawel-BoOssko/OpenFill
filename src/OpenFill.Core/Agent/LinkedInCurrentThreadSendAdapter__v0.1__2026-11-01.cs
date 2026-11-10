// OF-IMP-20261031 D02: candidate current-thread LinkedIn UI action.
// Not exposed to stable or dev tools until real logged-in WebView2 acceptance.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenFill.Core.Browser;

namespace OpenFill.Core.Agent;

public sealed class LinkedInCurrentThreadSendAdapter(BrowserController browser, OperationalMemoryStore memory)
{
    private const string SnapshotScript = """
        (() => {
          const needle = __MESSAGE_JSON__;
          const visible = e => !!(e && e.getClientRects().length &&
            getComputedStyle(e).visibility !== 'hidden');
          const unique = xs => [...new Set(xs)];
          const editors = unique([...document.querySelectorAll(
            '.msg-form__contenteditable[contenteditable="true"], .msg-form [contenteditable="true"][role="textbox"]'
          )].filter(visible));
          const buttons = unique([...document.querySelectorAll(
            '.msg-form__send-button, button.msg-form__send-button'
          )].filter(visible));
          const of = window.__openfill;
          const assign = e => {
            if (!e || !of?.byId || typeof of.seq !== 'number') return null;
            let id = e.getAttribute('data-of-id');
            if (id && of.byId.get(id) === e) return id;
            id = 'of' + of.seq++;
            e.setAttribute('data-of-id', id);
            of.byId.set(id, e);
            return id;
          };
          const outgoing = unique([...document.querySelectorAll(
            '.msg-s-message-list__event--outbound .msg-s-event-listitem__body, .msg-s-event-listitem--outbound .msg-s-event-listitem__body'
          )]);
          return {
            url: location.href, observed_at: new Date().toISOString(),
            editor_count: editors.length, send_count: buttons.length,
            editor_id: editors.length === 1 ? assign(editors[0]) : null,
            send_id: buttons.length === 1 ? assign(buttons[0]) : null,
            draft: editors.length === 1 ? (editors[0].innerText || '').trim() : null,
            send_disabled: buttons.length === 1 ? !!buttons[0].disabled : true,
            outgoing_match_count: outgoing.filter(e => (e.innerText || '').trim() === needle).length,
            scope: 'currently_loaded_dom', complete: false
          };
        })()
        """;

    private async Task<JsonObject?> SnapshotAsync(string message, CancellationToken ct)
    {
        var script = SnapshotScript.Replace("__MESSAGE_JSON__", JsonSerializer.Serialize(message),
            StringComparison.Ordinal);
        var raw = await browser.RunJsAsync(script, ct);
        return raw?["ok"]?.GetValue<bool>() == true ? raw["value"] as JsonObject : null;
    }

    public static ProcedureActionResult AssessEvidence(
        JsonObject before, JsonObject? after, bool exactIdentityVerified, string? evidenceRef)
    {
        if (!exactIdentityVerified || after is null)
            return new("uncertain", Reason: "post_send_identity_or_observation_unverified");
        if (!string.Equals(before["url"]?.ToString(), after["url"]?.ToString(),
                StringComparison.OrdinalIgnoreCase))
            return new("uncertain", Reason: "thread_changed_after_send");
        var previous = before["outgoing_match_count"]?.GetValue<int>() ?? -1;
        var current = after["outgoing_match_count"]?.GetValue<int>() ?? -1;
        if (previous < 0 || current <= previous)
            return new("uncertain", Reason: "new_outgoing_message_not_observed");
        if (string.IsNullOrWhiteSpace(evidenceRef))
            return new("uncertain", Reason: "outgoing_evidence_not_persisted");
        return new("verified", evidenceRef, Reason: "outgoing_visible_in_thread_not_delivery");
    }

    public async Task<ProcedureActionResult> SendCurrentAsync(
        string operationId, string expectedSenderProfile, string expectedThreadUrl,
        string expectedTargetProfile, string message, string evidenceDirectory,
        CancellationToken ct = default)
    {
        if (memory.OperationState(operationId) != "attempted")
            return new("blocked", Reason: "durable_attempt_not_recorded");
        if (!string.Equals(memory.OperationPayload(operationId), message, StringComparison.Ordinal))
            return new("blocked", Reason: "durable_payload_mismatch");
        if (string.IsNullOrWhiteSpace(message) || message.Length > 5000)
            return new("blocked", Reason: "invalid_message_length");
        var gate = new LinkedInThreadPreflight(browser);
        var initial = await gate.ObserveAndCheckAsync(
            expectedSenderProfile, expectedThreadUrl, expectedTargetProfile, ct);
        if (initial["status"]?.ToString() != "ready")
            return new("blocked", Reason: initial["reason_code"]?.ToString() ?? "preflight_failed");

        // The extractor creates the element-ID map used by real CDP input actions.
        await browser.ExtractAsync(maxFields: 150, ct: ct);
        var before = await SnapshotAsync(message, ct);
        if (before is null || before["editor_count"]?.GetValue<int>() != 1 ||
            before["send_count"]?.GetValue<int>() != 1 ||
            string.IsNullOrEmpty(before["editor_id"]?.ToString()) ||
            string.IsNullOrEmpty(before["send_id"]?.ToString()))
            return new("blocked", Reason: "composer_or_send_button_ambiguous");

        var typed = await browser.RealTypeAsync(before["editor_id"]!.ToString(), message, ct: ct);
        if (typed["ok"]?.GetValue<bool>() != true)
            return new("blocked", Reason: "composer_typing_failed");
        var secondGate = await gate.ObserveAndCheckAsync(
            expectedSenderProfile, expectedThreadUrl, expectedTargetProfile, ct);
        var draft = await SnapshotAsync(message, ct);
        if (secondGate["status"]?.ToString() != "ready" || draft is null ||
            draft["draft"]?.ToString() != message ||
            draft["send_id"]?.ToString() != before["send_id"]?.ToString() ||
            draft["send_disabled"]?.GetValue<bool>() != false ||
            draft["outgoing_match_count"]?.GetValue<int>() != before["outgoing_match_count"]?.GetValue<int>())
            return new("blocked", Reason: "stale_view_or_draft_mismatch_before_click");

        // The caller must already have written attempted to the durable ledger.
        // After this point ANY failure or missing proof must remain uncertain.
        var clicked = await browser.RealClickAsync(before["send_id"]!.ToString(), ct);
        if (clicked["ok"]?.GetValue<bool>() != true)
            return new("uncertain", Reason: "send_click_unconfirmed");
        await Task.Delay(500, ct);
        var after = await SnapshotAsync(message, ct);
        var postGate = await gate.ObserveAndCheckAsync(
            expectedSenderProfile, expectedThreadUrl, expectedTargetProfile, ct);
        var identityOk = postGate["status"]?.ToString() == "ready";
        var preliminary = AssessEvidence(before, after, identityOk, "pending_evidence");
        if (preliminary.Status != "verified") return preliminary;

        try
        {
            Directory.CreateDirectory(evidenceDirectory);
            var evidence = new JsonObject
            {
                ["provider"] = "linkedin", ["scope"] = "ui_outgoing_not_delivery",
                ["sender_profile_url"] = expectedSenderProfile,
                ["thread_url"] = expectedThreadUrl, ["target_profile_url"] = expectedTargetProfile,
                ["message_sha256"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(message))),
                ["before"] = before.DeepClone(), ["after"] = after!.DeepClone()
            };
            var json = evidence.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            var name = "linkedin_outgoing_" + Guid.NewGuid().ToString("N") + ".json";
            var finalPath = Path.Combine(evidenceDirectory, name);
            var tempPath = finalPath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json, Encoding.UTF8, ct);
            File.Move(tempPath, finalPath);
            var hash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(finalPath, ct)));
            return AssessEvidence(before, after, true, finalPath + "#sha256=" + hash);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new("uncertain", Reason: "evidence_persist_failed:" + ex.GetType().Name);
        }
    }
}
