// OF-000012: bounded read-only if/foreach decision slice over live semantic snapshots.
// No browser actions. This is a DEV companion to LocalPlanExecutor, not a LIVE router.
using System.Text.Json.Nodes;

namespace OpenFill.Core.Agent;

public sealed record LocalFlowResult(
    string Status, string? Reason, int Reads, int Iterations, IReadOnlyList<string> Observed,
    long LastRevision = -1);

public sealed class LocalPlanControlFlow
{
    private readonly ILocalPlanBrowser _browser;
    private readonly string _host;
    private readonly List<string> _observed = new();
    private int _reads;
    private int _iterations;
    private long _revision = -1;

    public LocalPlanControlFlow(ILocalPlanBrowser browser, string expectedHost,
        long minimumRevision = -1)
    {
        _browser = browser;
        _host = expectedHost;
        _revision = minimumRevision;
    }

    // Grammar: a maximum of 20 read-only top-level if/foreach nodes.
    // Branch bodies only permit read-only assert_visible/assert_absent.
    // foreach uses one current semantic name per item; never an array-index reference.
    public async Task<LocalFlowResult> ExecuteAsync(JsonObject json, CancellationToken ct = default)
    {
        Validate(json);
        if (!string.Equals(json["expected_host"]!.GetValue<string>(), _host, StringComparison.OrdinalIgnoreCase))
            return Fail("flow_plan_host_mismatch");
        try
        {
            foreach (var node in json["nodes"]!.AsArray().Select(x => x!.AsObject()))
            {
                var op = node["op"]!.GetValue<string>();
                if (op == "if")
                {
                    var snapshot = await ReadCheckedAsync(ct);
                    var role = node["role"]!.GetValue<string>();
                    var name = node["name"]!.GetValue<string>();
                    var resolution = SemanticLocatorResolver.Resolve(snapshot, new(role, name), false);
                    if (resolution.Status == "ambiguous") return Fail("if_ambiguous");
                    var matched = resolution.Status == "unique";
                    _observed.Add(node["id"] + (matched ? ":then" : ":else"));
                    var branch = node[matched ? "then" : "else"]!.AsArray();
                    foreach (var assert in branch)
                    {
                        var result = await AssertAsync(assert!.AsObject(), null, ct);
                        if (result is not null) return Fail(result);
                    }
                }
                else
                {
                    var snapshot = await ReadCheckedAsync(ct);
                    var role = node["role"]!.GetValue<string>();
                    var prefix = node["name_prefix"]!.GetValue<string>();
                    var names = snapshot.Elements
                        .Where(e => string.Equals(e.Role, role, StringComparison.OrdinalIgnoreCase)
                            && e.Name.StartsWith(prefix, StringComparison.Ordinal))
                        .Select(e => e.Name).ToArray();
                    if (names.Distinct(StringComparer.Ordinal).Count() != names.Length)
                        return Fail("foreach_ambiguous");
                    var max = node["max_items"]!.GetValue<int>();
                    if (names.Length > max) return Fail("foreach_limit_exceeded");
                    foreach (var name in names)
                    {
                        _iterations++;
                        var exact = await ReadCheckedAsync(ct);
                        var resolved = SemanticLocatorResolver.Resolve(exact, new(role, name), false);
                        if (resolved.Status != "unique")
                            return Fail("foreach_item_" + resolved.Status);
                        foreach (var assert in node["body"]!.AsArray())
                        {
                            var result = await AssertAsync(assert!.AsObject(), name, ct);
                            if (result is not null) return Fail(result);
                        }
                    }
                    _observed.Add(node["id"] + ":items=" + names.Length);
                }
            }
            return new("completed", null, _reads, _iterations, _observed, _revision);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("flow_", StringComparison.Ordinal))
        { return Fail(ex.Message); }
        catch (Exception)
        { return Fail("flow_snapshot_unavailable"); }
    }

    private LocalFlowResult Fail(string reason)
        => new("needs_replan", reason, _reads, _iterations, _observed.ToArray(), _revision);

    private async Task<SemanticPlanSnapshot> ReadCheckedAsync(CancellationToken ct)
    {
        if (_reads >= 80) throw new InvalidOperationException("flow_read_budget_exceeded");
        var current = await _browser.ReadAsync(ct);
        _reads++;
        if (!string.Equals(current.Host, _host, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("flow_host_changed");
        if (current.Revision < 0 || current.Revision < _revision)
            throw new InvalidOperationException("flow_stale_revision");
        _revision = current.Revision;
        return current;
    }

    private async Task<string?> AssertAsync(JsonObject node, string? itemName, CancellationToken ct)
    {
        var name = node["name"]!.GetValue<string>();
        if (name == "$item" && itemName is null)
            return "flow_unbound_item";
        if (name == "$item") name = itemName!;
        var snapshot = await ReadCheckedAsync(ct);
        var resolved = SemanticLocatorResolver.Resolve(snapshot,
            new(node["role"]!.GetValue<string>(), name), false);
        if (resolved.Status == "ambiguous") return "flow_assert_ambiguous";
        var expected = node["op"]!.GetValue<string>() == "assert_visible" ? "unique" : "missing";
        if (resolved.Status != expected) return "flow_assert_failed";
        _observed.Add(node["id"]!.GetValue<string>() + ":" + name + ":" + resolved.Status);
        return null;
    }

    private static string Required(JsonObject o, string key)
    {
        if (o[key] is not JsonValue v) throw new ArgumentException("flow_missing_" + key);
        try
        {
            var s = v.GetValue<string>();
            if (string.IsNullOrWhiteSpace(s) || s.Length > 180)
                throw new ArgumentException("flow_invalid_" + key);
            return s;
        }
        catch (Exception e) when (e is InvalidOperationException or FormatException)
        { throw new ArgumentException("flow_invalid_" + key, e); }
    }

    private static void Only(JsonObject o, params string[] fields)
    {
        var set = new HashSet<string>(fields, StringComparer.Ordinal);
        if (o.Any(x => !set.Contains(x.Key)))
            throw new ArgumentException("flow_unknown_field");
    }

    public static void Validate(JsonObject json)
    {
        Only(json, "protocol", "expected_host", "nodes");
        if (Required(json, "protocol") != "of.flow.v1")
            throw new ArgumentException("flow_unsupported_protocol");
        var host = Required(json, "expected_host");
        if (Uri.CheckHostName(host) == UriHostNameType.Unknown || host.Contains(':'))
            throw new ArgumentException("flow_invalid_host");
        if (json["nodes"] is not JsonArray nodes || nodes.Count is < 1 or > 20)
            throw new ArgumentException("flow_invalid_count");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            if (node is not JsonObject o) throw new ArgumentException("flow_invalid_node");
            var op = Required(o, "op");
            if (!ids.Add(Required(o, "id")))
                throw new ArgumentException("flow_duplicate_node");
            if (op == "if")
            {
                Only(o, "id", "op", "role", "name", "then", "else");
                Required(o, "role");
                Required(o, "name");
                ValidateAssertions(o["then"], ids);
                ValidateAssertions(o["else"], ids);
            }
            else if (op == "foreach")
            {
                Only(o, "id", "op", "role", "name_prefix", "max_items", "body");
                Required(o, "role");
                Required(o, "name_prefix");
                int max;
                try { max = o["max_items"]!.GetValue<int>(); }
                catch { throw new ArgumentException("flow_invalid_max_items"); }
                if (max is < 1 or > 20) throw new ArgumentException("flow_invalid_max_items");
                ValidateAssertions(o["body"], ids);
            }
            else throw new ArgumentException("flow_unknown_op");
        }
    }

    private static void ValidateAssertions(JsonNode? node, HashSet<string> ids)
    {
        if (node is not JsonArray arr || arr.Count is < 1 or > 10)
            throw new ArgumentException("flow_invalid_body");
        foreach (var child in arr)
        {
            if (child is not JsonObject o) throw new ArgumentException("flow_invalid_assert");
            Only(o, "id", "op", "role", "name");
            if (!ids.Add(Required(o, "id")))
                throw new ArgumentException("flow_duplicate_node");
            if (Required(o, "op") is not ("assert_visible" or "assert_absent"))
                throw new ArgumentException("flow_write_not_supported");
            Required(o, "role");
            Required(o, "name");
        }
    }
}
