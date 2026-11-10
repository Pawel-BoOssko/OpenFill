// Production LinkedInSendBatchService against an injected deterministic UI port.
// Tests real ProcedureExecutor + SQLite ledger + evidence path, never a real send.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using OpenFill.Core.Agent;

internal static class LinkedInBatchEffectSmoke
{
    private static int _checks;
    private static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception("D02_LINKEDIN_BATCH_" + label);
        _checks++;
    }

    private sealed class Port(OperationalMemoryStore store) : ILinkedInBatchPort
    {
        public bool AuthAllowed;
        public HashSet<string> Ready = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Unconfirmed = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string,int> Attempts = new(StringComparer.OrdinalIgnoreCase);

        public Task<bool> VerifySenderAsync(string sender, LinkedInSendTarget first, CancellationToken ct) =>
            Task.FromResult(AuthAllowed && sender == "https://www.linkedin.com/in/test-sender" &&
                first.ThreadUrl.StartsWith("https://www.linkedin.com/messaging/thread/", StringComparison.Ordinal));

        public Task<ProcedureActionResult> PreflightAsync(string sender, LinkedInSendTarget target, CancellationToken ct) =>
            Task.FromResult(Ready.Contains(target.ProfileUrl)
                ? new ProcedureActionResult("ready")
                : new ProcedureActionResult("blocked", Reason:"thread_not_ready"));

        public async Task<ProcedureActionResult> SendAsync(string operationId, string sender,
            LinkedInSendTarget item, string evidenceDirectory, CancellationToken ct)
        {
            if (store.OperationState(operationId) != "attempted" ||
                store.OperationPayload(operationId) != item.Message)
                return new ProcedureActionResult("blocked", Reason:"missing_attempt_or_wrong_payload");
            Attempts[item.ProfileUrl] = Attempts.GetValueOrDefault(item.ProfileUrl) + 1;
            if (Unconfirmed.Contains(item.ProfileUrl))
                return new ProcedureActionResult("uncertain", Reason:"outgoing_not_observed");
            Directory.CreateDirectory(evidenceDirectory);
            var evidencePath = Path.Combine(evidenceDirectory, operationId + ".json");
            var evidence = new JsonObject {
                ["operation_id"] = operationId,
                ["target_profile"] = item.ProfileUrl,
                ["payload_sha256"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(item.Message))),
                ["proof"] = "fixture_outgoing_visible_not_delivery"
            };
            await File.WriteAllTextAsync(evidencePath, evidence.ToJsonString(), ct);
            return new ProcedureActionResult("verified", evidencePath,
                item.ThreadUrl, "fixture_outgoing_visible_not_delivery");
        }
    }

    public static async Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "of_d02_batch_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var db = Path.Combine(root,"operational.db");
            var evidenceDir = Path.Combine(root, "evidence");
            var store = new OperationalMemoryStore(db);
            var port = new Port(store);
            var service = new LinkedInSendBatchService(port, store, evidenceDir);
            const string sender = "https://www.linkedin.com/in/test-sender";
            var a = new LinkedInSendTarget("https://www.linkedin.com/in/test-a",
                "https://www.linkedin.com/messaging/thread/test-a", "Test message A");
            var b = new LinkedInSendTarget("https://www.linkedin.com/in/test-b",
                "https://www.linkedin.com/messaging/thread/test-b", "Test message B");
            var items = new[] { a, b };
            Check(LinkedInSendBatchService.Validate(sender, items) is null, "valid_request");
            Check(LinkedInSendBatchService.Validate(sender, new[] { a, a }) == "duplicate_target",
                "duplicate_rejected");
            Check(LinkedInSendBatchService.Validate(sender, new[] {
                a with { ThreadUrl = "https://www.linkedin.com/messaging/thread/" } }) ==
                "invalid_thread_url", "incomplete_thread_rejected");
            var noOptIn = await service.SendBatchAsync(sender, "req-no-optin", items,
                "test", "batch", stagingOnly:false);
            Check(noOptIn["status"]?.ToString() == "staging_only", "requires_explicit_optin");
            var notAuthenticated = await service.SendBatchAsync(sender, "req-d02", items,
                "test", "batch", stagingOnly:true);
            Check(notAuthenticated["status"]?.ToString() == "authentication_unverified",
                "sender_auth_gate");
            Check(port.Attempts.Count == 0, "no_send_without_sender");

            port.AuthAllowed = true;
            port.Ready.Add(b.ProfileUrl);
            var first = await service.SendBatchAsync(sender, "req-d02", items,
                "test", "batch", stagingOnly:true);
            Check(first["status"]?.ToString() == "completed_with_item_status", "batch_result");
            var rows = first["items"]!.AsArray();
            Check(rows.Count == 2, "two_rows");
            Check(rows[0]?["state"]?.ToString() == "planned" &&
                rows[0]?["preflight_status"]?.ToString() == "blocked", "preflight_keeps_planned");
            Check(!port.Attempts.ContainsKey(a.ProfileUrl), "blocked_not_sent");
            Check(rows[1]?["state"]?.ToString() == "verified", "ready_sent_and_verified");
            Check(port.Attempts.GetValueOrDefault(b.ProfileUrl) == 1, "sent_once");
            var file = rows[1]?["evidence_ref"]?.ToString() ?? "";
            Check(File.Exists(file), "evidence_file_exists");
            var proof = JsonNode.Parse(await File.ReadAllTextAsync(file))!;
            Check(proof["operation_id"]?.ToString() == rows[1]?["operation_id"]?.ToString(),
                "evidence_operation_id");
            Check(proof["payload_sha256"]?.ToString() ==
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(b.Message))),
                "evidence_message_hash");

            port.Ready.Add(a.ProfileUrl);
            var resumed = await new LinkedInSendBatchService(port, new OperationalMemoryStore(db),
                evidenceDir).SendBatchAsync(sender, "req-d02", items, "test", "batch",
                stagingOnly:true);
            Check(resumed["items"]![0]?["state"]?.ToString() == "verified",
                "resume_completes_preflight");
            Check(resumed["items"]![1]?["executed"]?.GetValue<bool>() == false,
                "verified_not_replayed");
            Check(port.Attempts.GetValueOrDefault(a.ProfileUrl) == 1 &&
                port.Attempts.GetValueOrDefault(b.ProfileUrl) == 1,
                "one_send_per_target_after_restart");

            var collision = await service.SendBatchAsync(sender, "req-d02",
                new[] { a with { Message = "CHANGED" }, b }, "test", "batch", stagingOnly:true);
            Check(collision["status"]?.ToString() == "idempotency_conflict",
                "request_payload_conflict");
            var c = new LinkedInSendTarget("https://www.linkedin.com/in/test-c",
                "https://www.linkedin.com/messaging/thread/test-c", "Test message C");
            port.Ready.Add(c.ProfileUrl);
            port.Unconfirmed.Add(c.ProfileUrl);
            var uncertain = await service.SendBatchAsync(sender, "req-uncertain",
                new[] { c }, "test", "batch", stagingOnly:true);
            Check(uncertain["items"]![0]?["state"]?.ToString() == "uncertain",
                "no_post_send_proof_uncertain");
            var retried = await service.SendBatchAsync(sender, "req-uncertain",
                new[] { c }, "test", "batch", stagingOnly:true);
            Check(retried["items"]![0]?["executed"]?.GetValue<bool>() == false &&
                port.Attempts.GetValueOrDefault(c.ProfileUrl) == 1,
                "uncertain_not_replayed");
            Console.WriteLine($"D02_LINKEDIN_BATCH_{_checks}_CHECKS_OK");
        }
        finally
        {
            try { Directory.Delete(root, recursive:true); } catch (IOException) {}
        }
    }
}
