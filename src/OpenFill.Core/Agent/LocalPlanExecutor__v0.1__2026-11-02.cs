// OF-ARCHFLOW-20261102: browser-agnostic DEV plan slice: navigation, wait, local textbox fill.
// Not connected to LIVE AgentRunner. Send/save/submit actions are forbidden.
using System.Text.Json.Nodes;

namespace OpenFill.Core.Agent;

public sealed record SemanticPlanElement(string Ref, string Role, string Name, bool Enabled, string? Value = null);
public sealed record SemanticPlanSnapshot(long Revision, string Host, IReadOnlyList<SemanticPlanElement> Elements);
public sealed record SemanticPlanLocator(string Role, string Name);
public sealed record SemanticPlanNode(
    string Id, string Op, SemanticPlanLocator? Locator, string? Bind, string? Reference, string? Effect,
    int TimeoutMs = 0, string? Value = null, JsonObject? FlowNode = null);
public sealed record ValidatedSemanticPlan(string Host, IReadOnlyList<SemanticPlanNode> Nodes);
public sealed record SemanticPlanResolution(string Status, string? Ref = null);
public sealed record SemanticPlanClickResult(string Status, string? EvidenceRef = null);
public sealed record SemanticPlanNodeResult(string NodeId, string Status, long Revision, string? EvidenceRef = null);
public sealed record SemanticPlanExecutionResult(
    string Status, string? Reason, IReadOnlyList<SemanticPlanNodeResult> Nodes);

public interface ILocalPlanBrowser
{
    Task<SemanticPlanSnapshot> ReadAsync(CancellationToken ct);
    // The adapter must compare expectedRevision BEFORE attempting any browser action.
    // If the action may have been attempted, return "uncertain", never "stale".
    Task<SemanticPlanClickResult> ClickNavigationAsync(
        string elementRef, long expectedRevision, CancellationToken ct);
}

// Fill is a distinct capability: existing fake/navigation browsers remain read-only.
public interface ILocalPlanFormBrowser : ILocalPlanBrowser
{
    Task<SemanticPlanClickResult> FillTextAsync(string elementRef,
        long expectedRevision, string value, CancellationToken ct);
}

public static class PlanProtocolValidator
{
    private static string Required(JsonObject obj, string key)
    {
        if (obj[key] is not JsonValue value)
            throw new ArgumentException("plan_missing_" + key);
        string text;
        try { text = value.GetValue<string>(); }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException)
        { throw new ArgumentException("plan_invalid_" + key, ex); }
        if (string.IsNullOrWhiteSpace(text) || text.Length > 180)
            throw new ArgumentException("plan_invalid_" + key);
        return text;
    }

    private static string? Optional(JsonObject obj, string key)
        => obj.ContainsKey(key) ? Required(obj, key) : null;

    private static string TextValue(JsonObject obj)
    {
        if (obj["value"] is not JsonValue v)
            throw new ArgumentException("plan_missing_value");
        try
        {
            var text = v.GetValue<string>();
            if (text is null || text.Length > 2000)
                throw new ArgumentException("plan_invalid_value");
            return text;
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException)
        { throw new ArgumentException("plan_invalid_value", ex); }
    }

    private static void Only(JsonObject obj, params string[] keys)
    {
        var allowed = new HashSet<string>(keys, StringComparer.Ordinal);
        foreach (var key in obj.Select(x => x.Key))
            if (!allowed.Contains(key)) throw new ArgumentException("plan_unknown_field_" + key);
    }

    public static ValidatedSemanticPlan Parse(JsonObject source)
    {
        Only(source, "protocol", "expected_host", "nodes");
        if (Required(source, "protocol") != "of.plan.v1")
            throw new ArgumentException("plan_protocol_unsupported");
        var host = Required(source, "expected_host");
        if (Uri.CheckHostName(host) == UriHostNameType.Unknown || host.Contains(':') ||
            host.Contains('/') || host != host.Trim())
            throw new ArgumentException("plan_invalid_host");
        if (source["nodes"] is not JsonArray items || items.Count is < 1 or > 30)
            throw new ArgumentException("plan_invalid_node_count");

        var nodes = new List<SemanticPlanNode>(items.Count);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var bindings = new HashSet<string>(StringComparer.Ordinal);
        bool pendingPostcondition = false;
        bool pendingValuePostcondition = false;
        int clicks = 0, fills = 0, flowNodes = 0;
        foreach (var item in items)
        {
            if (item is not JsonObject obj)
                throw new ArgumentException("plan_invalid_node");
            Only(obj, "id", "op", "role", "name", "bind", "ref", "effect", "timeout_ms", "value",
                "name_prefix", "max_items", "then", "else", "body");
            var id = Required(obj, "id");
            if (!ids.Add(id)) throw new ArgumentException("plan_duplicate_node_id");
            var op = Required(obj, "op");
            if (op is not ("if" or "foreach") &&
                new[] { "name_prefix", "max_items", "then", "else", "body" }
                    .Any(obj.ContainsKey))
                throw new ArgumentException("plan_invalid_flow_fields");
            if (pendingPostcondition && op is not ("assert_visible" or "wait_until"))
                throw new ArgumentException("plan_missing_postcondition");
            if (pendingValuePostcondition && op != "assert_value")
                throw new ArgumentException("plan_missing_value_postcondition");

            SemanticPlanNode node;
            switch (op)
            {
                case "locate":
                {
                    var role = Required(obj, "role");
                    var name = Required(obj, "name");
                    var bind = Required(obj, "bind");
                    if (obj.ContainsKey("ref") || obj.ContainsKey("effect") || obj.ContainsKey("timeout_ms") || obj.ContainsKey("value") || !bindings.Add(bind))
                        throw new ArgumentException("plan_invalid_binding");
                    node = new(id, op, new(role, name), bind, null, null);
                    break;
                }
                case "click":
                {
                    if (obj.ContainsKey("role") || obj.ContainsKey("name") || obj.ContainsKey("bind") || obj.ContainsKey("timeout_ms") || obj.ContainsKey("value"))
                        throw new ArgumentException("plan_invalid_click_fields");
                    var reference = Required(obj, "ref");
                    if (!reference.StartsWith('$') || !bindings.Contains(reference[1..]))
                        throw new ArgumentException("plan_unknown_binding");
                    if (Required(obj, "effect") != "navigation")
                        throw new ArgumentException("plan_side_effect_not_supported");
                    var bound = nodes.First(n => n.Bind == reference[1..]).Locator!;
                    if (!string.Equals(bound.Role, "link", StringComparison.OrdinalIgnoreCase))
                        throw new ArgumentException("plan_non_navigation_locator");
                    if (++clicks > 12) throw new ArgumentException("plan_too_many_actions");
                    pendingPostcondition = true;
                    node = new(id, op, null, null, reference[1..], "navigation");
                    break;
                }
                case "assert_visible":
                {
                    if (obj.ContainsKey("ref") || obj.ContainsKey("effect") || obj.ContainsKey("bind") || obj.ContainsKey("timeout_ms") || obj.ContainsKey("value"))
                        throw new ArgumentException("plan_invalid_assert_fields");
                    node = new(id, op, new(Required(obj, "role"), Required(obj, "name")), null, null, null);
                    pendingPostcondition = false;
                    break;
                }
                case "wait_until":
                {
                    if (obj.ContainsKey("ref") || obj.ContainsKey("effect") || obj.ContainsKey("bind") || obj.ContainsKey("value"))
                        throw new ArgumentException("plan_invalid_wait_fields");
                    int ms;
                    try { ms = (obj["timeout_ms"] as JsonValue)?.GetValue<int>() ?? 0; }
                    catch (Exception ex) when (ex is InvalidOperationException or FormatException)
                    { throw new ArgumentException("plan_invalid_timeout_ms", ex); }
                    if (ms is < 100 or > 5000)
                        throw new ArgumentException("plan_invalid_timeout_ms");
                    node = new(id, op, new(Required(obj, "role"), Required(obj, "name")), null, null, null, ms);
                    pendingPostcondition = false;
                    break;
                }
                case "fill":
                {
                    if (obj.ContainsKey("role") || obj.ContainsKey("name") ||
                        obj.ContainsKey("bind") || obj.ContainsKey("effect") ||
                        obj.ContainsKey("timeout_ms"))
                        throw new ArgumentException("plan_invalid_fill_fields");
                    var reference = Required(obj, "ref");
                    if (!reference.StartsWith('$') || !bindings.Contains(reference[1..]))
                        throw new ArgumentException("plan_unknown_binding");
                    var bound = nodes.First(n => n.Bind == reference[1..]).Locator!;
                    if (!string.Equals(bound.Role, "textbox", StringComparison.OrdinalIgnoreCase))
                        throw new ArgumentException("plan_non_textbox_locator");
                    if (++fills > 12) throw new ArgumentException("plan_too_many_actions");
                    node = new(id, op, null, null, reference[1..], null, 0, TextValue(obj));
                    pendingValuePostcondition = true;
                    break;
                }
                case "assert_value":
                {
                    if (obj.ContainsKey("role") || obj.ContainsKey("name") ||
                        obj.ContainsKey("bind") || obj.ContainsKey("effect") ||
                        obj.ContainsKey("timeout_ms"))
                        throw new ArgumentException("plan_invalid_assert_fields");
                    var reference = Required(obj, "ref");
                    if (!reference.StartsWith('$') || !bindings.Contains(reference[1..]))
                        throw new ArgumentException("plan_unknown_binding");
                    var bound = nodes.First(n => n.Bind == reference[1..]).Locator!;
                    if (!string.Equals(bound.Role, "textbox", StringComparison.OrdinalIgnoreCase))
                        throw new ArgumentException("plan_non_textbox_locator");
                    node = new(id, op, null, null, reference[1..], null, 0, TextValue(obj));
                    pendingValuePostcondition = false;
                    break;
                }
                case "if":
                case "foreach":
                {
                    // Share the existing read-only flow grammar. Nested branches
                    // cannot perform clicks, fills, sends, saves or submits.
                    var flow = new JsonObject {
                        ["protocol"] = "of.flow.v1",
                        ["expected_host"] = host,
                        ["nodes"] = new JsonArray(obj.DeepClone())
                    };
                    LocalPlanControlFlow.Validate(flow);
                    if (++flowNodes > 4)
                        throw new ArgumentException("plan_too_many_flow_nodes");
                    if (op == "foreach" && (obj["max_items"]!.GetValue<int>() > 5 ||
                        obj["body"]!.AsArray().Count > 4))
                        throw new ArgumentException("plan_flow_budget_exceeded");
                    var inner = new List<JsonObject>();
                    foreach (var key in op == "if" ? new[] { "then", "else" } : new[] { "body" })
                        inner.AddRange(obj[key]!.AsArray().Select(n => n!.AsObject()));
                    if (ids.Count + inner.Count > 30)
                        throw new ArgumentException("plan_invalid_node_count");
                    foreach (var nested in inner)
                        if (!ids.Add(Required(nested, "id")))
                            throw new ArgumentException("plan_duplicate_node_id");
                    node = new(id, op, null, null, null, null, 0, null, obj.DeepClone().AsObject());
                    break;
                }
                default:
                    throw new ArgumentException("plan_unknown_op");
            }
            nodes.Add(node);
            if (ids.Count > 30) throw new ArgumentException("plan_invalid_node_count");
        }
        if (pendingPostcondition) throw new ArgumentException("plan_missing_postcondition");
        if (pendingValuePostcondition) throw new ArgumentException("plan_missing_value_postcondition");
        return new(host, nodes);
    }
}

public static class SemanticLocatorResolver
{
    public static SemanticPlanResolution Resolve(
        SemanticPlanSnapshot snapshot, SemanticPlanLocator locator, bool requireEnabled)
    {
        var matches = snapshot.Elements.Where(e =>
            string.Equals(e.Role, locator.Role, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(e.Name, locator.Name, StringComparison.Ordinal)).Take(2).ToArray();
        if (matches.Length == 0) return new("missing");
        if (matches.Length > 1) return new("ambiguous");
        if (string.IsNullOrWhiteSpace(matches[0].Ref)) return new("missing");
        if (requireEnabled && !matches[0].Enabled) return new("disabled");
        return new("unique", matches[0].Ref);
    }
}

public sealed class LocalPlanExecutor
{
    public async Task<SemanticPlanExecutionResult> ExecuteAsync(
        ValidatedSemanticPlan plan, ILocalPlanBrowser browser, CancellationToken ct = default)
    {
        var outcomes = new List<SemanticPlanNodeResult>();
        var bindings = new Dictionary<string, SemanticPlanLocator>(StringComparer.Ordinal);
        long? previousRevision = null;
        long? pendingClickRevision = null;
        long? pendingFillRevision = null;
        foreach (var node in plan.Nodes)
        {
            ct.ThrowIfCancellationRequested();
            SemanticPlanSnapshot snapshot;
            try { snapshot = await browser.ReadAsync(ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception)
            { return new("needs_replan", "snapshot_unavailable", outcomes); }
            if (!string.Equals(snapshot.Host, plan.Host, StringComparison.OrdinalIgnoreCase))
                return new("needs_replan", "host_changed", outcomes);
            if (snapshot.Revision < 0)
                return new("needs_replan", "invalid_revision", outcomes);
            if (previousRevision.HasValue && snapshot.Revision < previousRevision.Value)
                return new("needs_replan", "stale_snapshot", outcomes);
            previousRevision = snapshot.Revision;

            if (node.Op is "if" or "foreach")
            {
                // Execute bounded read-only branches against the same live browser.
                // A new flow must never accept a DOM revision older than the one
                // already seen by the surrounding sequential plan.
                var flowJson = new JsonObject {
                    ["protocol"] = "of.flow.v1",
                    ["expected_host"] = plan.Host,
                    ["nodes"] = new JsonArray(node.FlowNode!.DeepClone())
                };
                var flow = await new LocalPlanControlFlow(browser, plan.Host,
                    snapshot.Revision).ExecuteAsync(flowJson, ct);
                var flowRevision = Math.Max(snapshot.Revision, flow.LastRevision);
                if (flow.Status != "completed")
                {
                    outcomes.Add(new(node.Id, "failed", flowRevision));
                    return new("needs_replan", flow.Reason, outcomes);
                }
                // Count actual assertions, not mere control-flow branch selections.
                foreach (var record in flow.Observed)
                {
                    // Accessible names may themselves contain ':'. Read the
                    // outcome suffix rather than splitting at every colon.
                    if (record.EndsWith(":unique", StringComparison.Ordinal) ||
                        record.EndsWith(":missing", StringComparison.Ordinal))
                    {
                        var separator = record.IndexOf(':');
                        if (separator > 0)
                            outcomes.Add(new(node.Id + "/" + record[..separator],
                                "observed", flowRevision));
                    }
                }
                outcomes.Add(new(node.Id, "flow_completed", flowRevision));
                previousRevision = flowRevision;
            }
            else if (node.Op == "locate")
            {
                var resolved = SemanticLocatorResolver.Resolve(snapshot, node.Locator!, true);
                if (resolved.Status != "unique")
                {
                    outcomes.Add(new(node.Id, resolved.Status, snapshot.Revision));
                    return new("needs_replan", "locator_" + resolved.Status, outcomes);
                }
                bindings.Add(node.Bind!, node.Locator!); // bind semantics, not a stale DOM element ID
                outcomes.Add(new(node.Id, "located", snapshot.Revision));
            }
            else if (node.Op == "assert_visible")
            {
                if (pendingClickRevision.HasValue && snapshot.Revision <= pendingClickRevision.Value)
                {
                    outcomes.Add(new(node.Id, "revision_unchanged", snapshot.Revision));
                    return new("needs_replan", "postcondition_revision_unchanged", outcomes);
                }
                var resolved = SemanticLocatorResolver.Resolve(snapshot, node.Locator!, false);
                if (resolved.Status != "unique")
                {
                    outcomes.Add(new(node.Id, resolved.Status, snapshot.Revision));
                    return new("needs_replan", "postcondition_" + resolved.Status, outcomes);
                }
                outcomes.Add(new(node.Id, "observed", snapshot.Revision));
                pendingClickRevision = null;
            }
            else if (node.Op == "wait_until")
            {
                // All polling stays local, not a model-call round trip.
                // This is observation only: never replay an action when a condition fails.
                var elapsed = System.Diagnostics.Stopwatch.StartNew();
                while (true)
                {
                    var resolved = SemanticLocatorResolver.Resolve(snapshot, node.Locator!, false);
                    if (resolved.Status == "ambiguous")
                    {
                        outcomes.Add(new(node.Id, "ambiguous", snapshot.Revision));
                        return new("needs_replan", "wait_ambiguous", outcomes);
                    }
                    if (resolved.Status == "unique" &&
                        (!pendingClickRevision.HasValue || snapshot.Revision > pendingClickRevision.Value))
                    {
                        outcomes.Add(new(node.Id, "observed", snapshot.Revision));
                        pendingClickRevision = null;
                        break;
                    }
                    var remaining = node.TimeoutMs - (int)elapsed.ElapsedMilliseconds;
                    if (remaining <= 0)
                    {
                        outcomes.Add(new(node.Id, "timeout", snapshot.Revision));
                        return new("needs_replan", "wait_timeout", outcomes);
                    }
                    await Task.Delay(Math.Min(100, remaining), ct);
                    try { snapshot = await browser.ReadAsync(ct); }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                    catch (Exception)
                    { return new("needs_replan", "snapshot_unavailable", outcomes); }
                    if (!string.Equals(snapshot.Host, plan.Host, StringComparison.OrdinalIgnoreCase))
                        return new("needs_replan", "host_changed", outcomes);
                    if (snapshot.Revision < 0)
                        return new("needs_replan", "invalid_revision", outcomes);
                    if (previousRevision.HasValue && snapshot.Revision < previousRevision.Value)
                        return new("needs_replan", "stale_snapshot", outcomes);
                    previousRevision = snapshot.Revision;
                }
            }
            else if (node.Op == "assert_value")
            {
                if (!bindings.TryGetValue(node.Reference!, out var locator))
                    return new("needs_replan", "binding_missing", outcomes);
                if (pendingFillRevision.HasValue && snapshot.Revision <= pendingFillRevision.Value)
                {
                    outcomes.Add(new(node.Id, "revision_unchanged", snapshot.Revision));
                    return new("needs_replan", "value_revision_unchanged", outcomes);
                }
                var resolved = SemanticLocatorResolver.Resolve(snapshot, locator, false);
                if (resolved.Status != "unique")
                {
                    outcomes.Add(new(node.Id, resolved.Status, snapshot.Revision));
                    return new("needs_replan", "value_locator_" + resolved.Status, outcomes);
                }
                var element = snapshot.Elements.FirstOrDefault(e => e.Ref == resolved.Ref);
                if (element?.Value is null || !string.Equals(element.Value, node.Value, StringComparison.Ordinal))
                {
                    outcomes.Add(new(node.Id, "mismatch", snapshot.Revision));
                    return new("needs_replan", "value_mismatch", outcomes);
                }
                outcomes.Add(new(node.Id, "observed", snapshot.Revision));
                pendingFillRevision = null;
            }
            else if (node.Op == "fill")
            {
                if (browser is not ILocalPlanFormBrowser formBrowser)
                    return new("needs_replan", "form_browser_unavailable", outcomes);
                if (!bindings.TryGetValue(node.Reference!, out var locator))
                    return new("needs_replan", "binding_missing", outcomes);
                var resolved = SemanticLocatorResolver.Resolve(snapshot, locator, true);
                if (resolved.Status != "unique")
                {
                    outcomes.Add(new(node.Id, resolved.Status, snapshot.Revision));
                    return new("needs_replan", "locator_" + resolved.Status, outcomes);
                }
                SemanticPlanClickResult action;
                try { action = await formBrowser.FillTextAsync(resolved.Ref!, snapshot.Revision, node.Value!, ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception)
                {
                    outcomes.Add(new(node.Id, "uncertain", snapshot.Revision));
                    return new("uncertain_effect", "browser_fill_exception", outcomes);
                }
                if (action.Status != "applied")
                {
                    outcomes.Add(new(node.Id, action.Status, snapshot.Revision, action.EvidenceRef));
                    return action.Status is "stale" or "failed_before_action"
                        ? new("needs_replan", "fill_" + action.Status, outcomes)
                        : new("uncertain_effect", "fill_" + action.Status, outcomes);
                }
                outcomes.Add(new(node.Id, "applied_unverified", snapshot.Revision, action.EvidenceRef));
                pendingFillRevision = snapshot.Revision;
            }
            else if (node.Op == "click")
            {
                if (!bindings.TryGetValue(node.Reference!, out var locator))
                    return new("needs_replan", "binding_missing", outcomes);
                var resolved = SemanticLocatorResolver.Resolve(snapshot, locator, true);
                if (resolved.Status != "unique")
                {
                    outcomes.Add(new(node.Id, resolved.Status, snapshot.Revision));
                    return new("needs_replan", "locator_" + resolved.Status, outcomes);
                }
                SemanticPlanClickResult action;
                try { action = await browser.ClickNavigationAsync(resolved.Ref!, snapshot.Revision, ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception)
                {
                    outcomes.Add(new(node.Id, "uncertain", snapshot.Revision));
                    return new("uncertain_effect", "browser_action_exception", outcomes);
                }
                if (action.Status != "applied")
                {
                    outcomes.Add(new(node.Id, action.Status, snapshot.Revision, action.EvidenceRef));
                    return action.Status == "stale" || action.Status == "failed_before_action"
                        ? new("needs_replan", "action_" + action.Status, outcomes)
                        : new("uncertain_effect", "action_" + action.Status, outcomes);
                }
                // Applied is not proof that navigation succeeded; the next assert must observe it.
                outcomes.Add(new(node.Id, "applied_unverified", snapshot.Revision, action.EvidenceRef));
                pendingClickRevision = snapshot.Revision;
            }
        }
        return new("completed", null, outcomes);
    }
}
