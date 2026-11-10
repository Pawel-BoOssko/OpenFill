// OF-000018: explicit DEV plan interpreter for journaled fixture effects.
// Regular of.plan.v1 validator deliberately rejects effect nodes in LIVE.
// This standalone DEBUG entry point only permits two localhost save buttons.
#if DEBUG
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace OpenFill.Core.Agent;

public sealed record DevEffectPlanResult(
    string Status, string? Reason, int EffectAttempts, int EffectVerified,
    int DomReads, IReadOnlyList<LocalPlanEffectExecutionResult> Effects);

public sealed class LocalPlanEffectPlanDevRunner
{
    private sealed record Step(ValidatedSemanticPlan? Local, LocalPlanEffectKey? Effect,
        string? ExpectedEvidence);
    private sealed class EvidenceMatchAdapter(ILocalPlanEffectAdapter inner, string expected)
        : ILocalPlanEffectAdapter
    {
        public Task<LocalPlanEffectAttemptResult> AttemptAsync(LocalPlanEffectKey key,
            CancellationToken ct) => inner.AttemptAsync(key, ct);
        public async Task<string?> ObserveAsync(LocalPlanEffectKey key, CancellationToken ct)
        {
            var observed = await inner.ObserveAsync(key, ct);
            return string.Equals(observed, expected, StringComparison.Ordinal) ? observed : null;
        }
    }

    private static string Text(JsonObject node, string name, int max = 180)
    {
        if (node[name] is not JsonValue v) throw new ArgumentException("dev_effect_missing_" + name);
        string? s;
        try { s = v.GetValue<string>(); }
        catch (Exception e) when (e is InvalidOperationException or FormatException)
        { throw new ArgumentException("dev_effect_invalid_" + name, e); }
        if (string.IsNullOrWhiteSpace(s) || s.Length > max)
            throw new ArgumentException("dev_effect_invalid_" + name);
        return s;
    }

    private static void Only(JsonObject node, params string[] fields)
    {
        var valid = new HashSet<string>(fields, StringComparer.Ordinal);
        if (node.Any(kv => !valid.Contains(kv.Key)))
            throw new ArgumentException("dev_effect_unknown_field");
    }

    public async Task<DevEffectPlanResult> ExecuteAsync(
        JsonObject plan, ILocalPlanBrowser browser, ILocalPlanEffectAdapter adapter,
        LocalPlanEffectJournal journal, string businessOperationId, string taskId,
        string runId, CancellationToken ct = default)
    {
        // Validate every node BEFORE the first action. No effect exists in the
        // production PlanProtocolValidator and no standard agent route calls here.
        Only(plan, "protocol", "expected_host", "nodes");
        if (Text(plan, "protocol") != "of.plan.v1")
            throw new ArgumentException("dev_effect_invalid_protocol");
        var host = Text(plan, "expected_host");
        if (host != "127.0.0.1")
            throw new ArgumentException("dev_effect_localhost_only");
        if (plan["nodes"] is not JsonArray nodes || nodes.Count is < 1 or > 12)
            throw new ArgumentException("dev_effect_invalid_node_count");
        foreach (var id in new[] { businessOperationId, taskId, runId })
            if (string.IsNullOrWhiteSpace(id) || id.Length > 128)
                throw new ArgumentException("dev_effect_invalid_correlation");

        var steps = new List<Step>();
        var pure = new JsonArray();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var seenEffects = new HashSet<string>(StringComparer.Ordinal);
        var effectCount = 0;
        var totalNodeIds = 0;

        void FlushPure()
        {
            if (pure.Count == 0) return;
            var ordinary = new JsonObject {
                ["protocol"] = "of.plan.v1", ["expected_host"] = host,
                ["nodes"] = pure.DeepClone()
            };
            // Full pre-validation ensures no incomplete navigation/fill is
            // separated by an effect boundary.
            steps.Add(new(PlanProtocolValidator.Parse(ordinary), null, null));
            pure = new JsonArray();
        }

        foreach (var item in nodes)
        {
            if (item is not JsonObject n)
                throw new ArgumentException("dev_effect_invalid_node");
            var id = Text(n, "id");
            if (!seenIds.Add(id))
                throw new ArgumentException("dev_effect_duplicate_node_id");
            totalNodeIds++;
            if (Text(n, "op") != "effect")
            {
                pure.Add(n.DeepClone());
                continue;
            }

            FlushPure();
            if (++effectCount > 2)
                throw new ArgumentException("dev_effect_too_many_effects");
            Only(n, "id", "op", "effect_id", "target_id", "action",
                "payload_sha256", "postcondition");
            var effectId = Text(n, "effect_id", 128);
            var target = Text(n, "target_id");
            var action = Text(n, "action");
            var hash = Text(n, "payload_sha256");
            if (!seenEffects.Add(effectId))
                throw new ArgumentException("dev_effect_duplicate_effect_id");
            if (target is not ("a" or "b") || action != "save" ||
                effectId != "save-" + target)
                throw new ArgumentException("dev_effect_fixture_identity_invalid");
            var correctHash = Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes("value-" + target)));
            if (hash != correctHash)
                throw new ArgumentException("dev_effect_fixture_payload_invalid");
            if (n["postcondition"] is not JsonObject proof)
                throw new ArgumentException("dev_effect_postcondition_missing");
            Only(proof, "type", "expected");
            if (Text(proof, "type") != "adapter_evidence")
                throw new ArgumentException("dev_effect_postcondition_type");
            var expected = Text(proof, "expected");
            if (expected != "local-effect:" + target + ":count=1")
                throw new ArgumentException("dev_effect_postcondition_invalid");
            steps.Add(new(null, new LocalPlanEffectKey(
                businessOperationId, taskId, runId, "of-local-fixture", "dev-session",
                target, action, hash, effectId), expected));
        }
        FlushPure();

        // Branch IDs must not collide with other nodes or effect IDs.
        foreach (var step in steps.Where(s => s.Local is not null))
        {
            foreach (var node in step.Local!.Nodes)
            {
                if (node.Op is "if" or "foreach")
                {
                    var branches = node.Op == "if" ? new[] { "then", "else" }
                        : new[] { "body" };
                    foreach (var branch in branches)
                        foreach (var nested in node.FlowNode![branch]!.AsArray())
                        {
                            var nestedId = Text(nested!.AsObject(), "id");
                            if (!seenIds.Add(nestedId))
                                throw new ArgumentException("dev_effect_duplicate_node_id");
                            totalNodeIds++;
                        }
                }
            }
        }
        if (totalNodeIds > 20)
            throw new ArgumentException("dev_effect_total_node_limit");

        var effects = new List<LocalPlanEffectExecutionResult>();
        int domReads = 0, attempts = 0, verified = 0;
        foreach (var step in steps)
        {
            ct.ThrowIfCancellationRequested();
            if (step.Local is not null)
            {
                var res = await new LocalPlanExecutor().ExecuteAsync(step.Local, browser, ct);
                if (res.Status != "completed")
                    return new(res.Status, res.Reason, attempts, verified,
                        domReads, effects);
                continue;
            }
            // Verify the current host before invoking the effect boundary.
            // Actual adapter also checks localhost origin, port and path.
            domReads++;
            SemanticPlanSnapshot snapshot;
            try { snapshot = await browser.ReadAsync(ct); }
            catch (Exception) when (!ct.IsCancellationRequested)
            { return new("needs_replan","dev_effect_snapshot_unavailable",
                attempts,verified,domReads,effects); }
            if (snapshot.Host != host || snapshot.Revision < 0)
                return new("needs_replan","dev_effect_host_changed",
                    attempts,verified,domReads,effects);

            // Already recorded effects do not issue a second browser action.
            var effect = await new LocalPlanEffectExecutor(journal).ExecuteAsync(
                step.Effect!, new EvidenceMatchAdapter(adapter, step.ExpectedEvidence!), ct);
            effects.Add(effect);
            if (effect.Status == "verified")
            {
                attempts++;
                verified++;
            }
            else if (effect.Status == "already_recorded")
            {
                // A previously completed effect must not be mistaken for a
                // newly applied one. Stop and leave the following effects alone.
                return new("already_recorded", effect.Reason, attempts,
                    verified, domReads, effects);
            }
            else
            {
                attempts++;
                return new(effect.Status, effect.Reason, attempts,
                    verified, domReads, effects);
            }
        }
        return new("completed", null, attempts, verified, domReads, effects);
    }
}
#endif
