// OF-000025: exercise the real WhatsAppBatchService with a deterministic
// browser port, SQLite ledger and durable UI evidence. No external send.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using OpenFill.Core.Agent;

internal static class WhatsAppBatchEffectSmoke
{
    private static int _checks;
    private static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception("OF025_WHATSAPP_BATCH_" + message);
        _checks++;
    }

    private sealed class Port : IWhatsAppBatchSendPort
    {
        public HashSet<string> Ready { get; } = new(StringComparer.Ordinal);
        public HashSet<string> Unconfirmed { get; } = new(StringComparer.Ordinal);
        public Dictionary<string,int> Sends { get; } = new(StringComparer.Ordinal);

        public Task<JsonNode?> OpenAsync(string target, CancellationToken ct)
        {
            var available = Ready.Contains(target);
            return Task.FromResult<JsonNode?>(new JsonObject
            {
                ["ok"] = available,
                ["error"] = available ? null : "conversation_not_ready_after_navigation",
                ["view"] = new JsonObject {
                    ["conversation_open"] = available, ["composer_visible"] = available
                }
            });
        }

        public Task<JsonNode?> SendAsync(string target, string message, CancellationToken ct)
        {
            Sends[target] = Sends.GetValueOrDefault(target) + 1;
            var confirmed = !Unconfirmed.Contains(target);
            return Task.FromResult<JsonNode?>(new JsonObject
            {
                ["status"] = confirmed ? "sent_ui" : "unconfirmed",
                ["confirmation"] = confirmed
                    ? "new_outgoing_bubble_visible" : "outgoing_bubble_not_confirmed",
                ["view"] = new JsonObject
                {
                    ["messages"] = new JsonArray(new JsonObject
                    {
                        ["id"] = "new-" + target + "-" + Sends[target],
                        ["direction"] = "out", ["text"] = message
                    })
                }
            });
        }
    }

    public static async Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "of025_wa_batch_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            const string sender = "254712345678";
            const string blocked = "254700000001";
            const string ready = "254700000002";
            const string uncertainTarget = "254700000003";
            const string message = "Explicit batch fixture";
            var db = Path.Combine(root, "operations.db");
            var port = new Port();
            port.Ready.Add(ready);
            var service = new WhatsAppBatchService(port, new OperationalMemoryStore(db));
            var targets = new[] {
                new ProcedureTarget(blocked, message),
                new ProcedureTarget(ready, message)
            };
            static Task<bool> Auth(string account, CancellationToken ct) =>
                Task.FromResult(account == "254712345678");

            var first = await service.SendBatchAsync(sender, "batch-025",
                targets, Auth, "task-025", "smoke", "test-chat",
                CancellationToken.None, candidateStagingOnly:true);
            Check(first["status"]?.ToString() == "completed_with_item_status", "batch_completed");
            var rows = first["items"]!.AsArray();
            Check(rows.Count == 2, "two_targets");
            Check(rows[0]?["state"]?.ToString() == "planned" &&
                  rows[0]?["preflight_status"]?.ToString() == "blocked" &&
                  rows[0]?["executed"]?.GetValue<bool>() == false,
                  "blocked_preflight_keeps_planned");
            Check(!port.Sends.ContainsKey(blocked), "blocked_target_never_sent");
            Check(rows[1]?["state"]?.ToString() == "verified", "second_target_verified");
            // OF-000038: timings are measured per operation and aggregated
            // without changing which target is attempted or resumed.
            Check(rows[0]?["preflight_elapsed_ms"] is not null &&
                  rows[0]?["action_elapsed_ms"] is null, "blocked_preflight_timing");
            Check(rows[1]?["preflight_elapsed_ms"] is not null &&
                  rows[1]?["action_elapsed_ms"] is not null, "verified_action_timing");
            Check(first["metrics"]?["executed_this_run"]?.GetValue<int>() == 1 &&
                  first["metrics"]?["items_returned"]?.GetValue<int>() == 2,
                  "batch_timing_counts");
            Check(first["metrics"]?["preflight_elapsed_ms"] is not null &&
                  first["metrics"]?["action_elapsed_ms"] is not null &&
                  first["metrics"]?["batch_elapsed_ms"] is not null,
                  "batch_timing_fields");
            Check(port.Sends.GetValueOrDefault(ready) == 1, "second_target_one_send");
            var evidenceFile = rows[1]?["evidence_ref"]?.ToString() ?? "";
            Check(File.Exists(evidenceFile), "evidence_exists");
            var evidence = JsonNode.Parse(await File.ReadAllTextAsync(evidenceFile))!;
            Check(evidence["operation_id"]?.ToString() ==
                  rows[1]?["operation_id"]?.ToString(), "evidence_operation_id");
            Check(evidence["account_id"]?.ToString() == sender &&
                  evidence["target_id"]?.ToString() == ready, "evidence_scope");
            Check(evidence["payload_sha256"]?.ToString() ==
                  Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(message))),
                  "evidence_hash");

            port.Ready.Add(blocked);
            var resumed = await new WhatsAppBatchService(
                port, new OperationalMemoryStore(db)).SendBatchAsync(
                sender, "batch-025", targets, Auth, "task-025-new", "smoke", "test-chat",
                CancellationToken.None, candidateStagingOnly:true);
            var resumedRows = resumed["items"]!.AsArray();
            Check(resumedRows[0]?["state"]?.ToString() == "verified",
                  "resume_preflight_then_send");
            Check(resumedRows[1]?["executed"]?.GetValue<bool>() == false,
                  "verified_target_skipped");
            Check(port.Sends.GetValueOrDefault(blocked) == 1 &&
                  port.Sends.GetValueOrDefault(ready) == 1, "no_repeat_after_restart");

            port.Ready.Add(uncertainTarget);
            port.Unconfirmed.Add(uncertainTarget);
            var uncertainArgs = new[] { new ProcedureTarget(uncertainTarget, message) };
            var uncertain = await service.SendBatchAsync(sender, "batch-025-uncertain",
                uncertainArgs, Auth, "task-025", "smoke", "test-chat",
                CancellationToken.None, candidateStagingOnly:true);
            Check(uncertain["items"]![0]?["state"]?.ToString() == "uncertain",
                  "unconfirmed_is_uncertain");
            var unreplayed = await new WhatsAppBatchService(
                port, new OperationalMemoryStore(db)).SendBatchAsync(
                sender, "batch-025-uncertain", uncertainArgs, Auth,
                "task-025-new", "smoke", "test-chat",
                CancellationToken.None, candidateStagingOnly:true);
            Check(unreplayed["items"]![0]?["executed"]?.GetValue<bool>() == false &&
                  port.Sends.GetValueOrDefault(uncertainTarget) == 1,
                  "uncertain_never_replayed");
            Console.WriteLine($"OF025_WHATSAPP_BATCH_{_checks}_CHECKS_OK");
        }
        finally
        {
            try { Directory.Delete(root, recursive:true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
