// OF-IMP-20261031 D02: resolve and persist LinkedIn batch, without a model loop.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace OpenFill.Core.Agent;

public sealed class LinkedInBatchPlanner(OperationalMemoryStore memory)
{
    public JsonObject Prepare(string accountId, string requestId, IReadOnlyList<ProcedureTarget> targets,
        string requester, string chat)
    {
        if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(requestId) ||
            string.IsNullOrWhiteSpace(requester) || string.IsNullOrWhiteSpace(chat))
            return Error("missing_identity", "account_id, request_id, requester and chat required.");
        if (targets.Count is < 1 or > 200)
            return Error("invalid_count", "1 to 200 exact targets required.");
        if (targets.Any(t => string.IsNullOrWhiteSpace(t.TargetId) || string.IsNullOrWhiteSpace(t.Payload)))
            return Error("invalid_target", "Every target requires an exact identifier and nonempty message.");
        if (targets.GroupBy(t => t.TargetId, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() != 1))
            return Error("duplicate_target", "One target must occur only once per batch.");

        var ids = targets.Select(t => t.TargetId.Trim()).ToArray();
        var resolved = memory.ResolveLinkedInBatch(accountId, ids);
        if (resolved.Count != targets.Count)
            return Error("resolution_mismatch", "Not every requested target produced exactly one resolution row.");

        // Preserve a stable request fingerprint so changed payloads never silently become a retry.
        static void Append(StringBuilder sb, string value) => sb.Append(value.Length).Append(':').Append(value);
        var sb = new StringBuilder();
        Append(sb, "linkedin.send_batch");
        Append(sb, "1.0");
        foreach (var item in targets) { Append(sb, item.TargetId.Trim()); Append(sb, item.Payload); }
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
        var batchId = memory.BeginBatch("linkedin", accountId,
            requester + "|" + chat + "|" + requestId, "linkedin.send_batch", "1.0", null, requester, chat);
        if (!memory.BindRequestFingerprint(batchId, fingerprint))
            return Error("idempotency_conflict", "Same request_id used for a different target or payload.");

        var result = new JsonArray();
        for (var i = 0; i < targets.Count; i++)
        {
            var target = targets[i];
            var known = resolved[i] as JsonObject ?? throw new InvalidOperationException("Missing resolution row.");
            var id = target.TargetId.Trim();
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(target.Payload)));
            var operation = memory.PlanOperation(batchId, id, "send_message_batch", hash, target.Payload);
            var knownThread = known["thread_url"]?.ToString();
            if (knownThread is null && Uri.TryCreate(id, UriKind.Absolute, out var supplied) &&
                supplied.Scheme == "https" && supplied.Host.Equals("www.linkedin.com", StringComparison.OrdinalIgnoreCase) &&
                supplied.AbsolutePath.StartsWith("/messaging/", StringComparison.OrdinalIgnoreCase))
                knownThread = supplied.GetLeftPart(UriPartial.Path);
            var stage = knownThread is not null ? "thread_ready" :
                known["status"]?.ToString() == "resolved_profile" ? "resolve_thread" : "resolve_profile";
            result.Add(new JsonObject
            {
                ["target_id"] = id,
                ["operation_id"] = operation,
                ["state"] = memory.OperationState(operation),
                ["stage"] = stage,
                ["thread_url"] = knownThread,
                ["profile_url"] = known["profile_url"]?.ToString(),
                ["source_ref"] = known["source_ref"]?.ToString()
            });
        }
        var threadReady = result.OfType<JsonObject>().Count(x => x["stage"]?.ToString() == "thread_ready");
        return new JsonObject
        {
            ["status"] = "planned",
            ["batch_id"] = batchId,
            ["requested"] = targets.Count,
            ["thread_ready"] = threadReady,
            ["unresolved"] = targets.Count - threadReady,
            ["counts"] = memory.BatchStatus(batchId),
            ["items"] = result
        };
    }
    private static JsonObject Error(string status, string reason) => new()
    { ["status"] = status, ["reason"] = reason };
}
