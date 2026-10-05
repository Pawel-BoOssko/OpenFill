// OpenFill - Metadata: wersja 0.2, data 2026-10-05 19:15
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OpenFill.Core.Agent;

/// <summary>What one web domain has cost so far (cumulative, no daily reset).</summary>
public sealed class DomainCostEntry
{
    public double Total { get; set; }
    /// <summary>Part of Total that was already "forgiven" by the user when they lifted a block; the limit counts Total - Offset.</summary>
    public double Offset { get; set; }
    public bool Blocked { get; set; }
    public DateTime? BlockedUtc { get; set; }
}

/// <summary>Row for the panel.</summary>
public sealed record DomainCost(string Domain, double Spent, double Lifetime, bool Blocked, bool Locked = false);

/// <summary>Live numbers of the task that is running now (read by the MCP status and the panel).</summary>
public sealed class RunMeter
{
    public int Steps { get; set; }
    public double CostUsd { get; set; }
    public string Domain { get; set; } = "";
    public string? Warning { get; set; }
    public RunProgress Snapshot() => new(Steps, CostUsd, Warning);
}

/// <summary>Immutable copy of a <see cref="RunMeter"/>.</summary>
public sealed record RunProgress(int Steps, double CostUsd, string? Warning);

/// <summary>
/// Hard cost limit per web domain. Every model call is charged to the domain the browser is on; once a domain has cost the limit
/// (cumulative, it never resets by itself) it is blocked: no task may work on it again until the person at the computer lifts
/// the block in the OpenFill window. Nothing that arrives over MCP can lift it.
/// </summary>
public sealed class CostGuard
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };
    private static readonly HashSet<string> SecondLevel = new(StringComparer.OrdinalIgnoreCase) { "co", "com", "org", "net", "gov", "edu", "ac" };

    private readonly string? _file;
    private readonly object _lock = new();
    private readonly Dictionary<string, DomainCostEntry> _d = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Raised (on any thread) when a total or a block changed.</summary>
    public event Action? Changed;

    public CostGuard(string? file = null)
    {
        _file = file;
        try
        {
            if (file is not null && File.Exists(file))
            {
                var loaded = JsonSerializer.Deserialize<Dictionary<string, DomainCostEntry>>(File.ReadAllText(file));
                if (loaded is not null) foreach (var (k, v) in loaded) _d[k] = v;
            }
        }
        catch { /* a damaged file starts empty; it is rewritten on the next charge */ }
    }

    /// <summary>The domain a limit is counted on: host without "www.", reduced to the registrable part (stepstone.de, example.co.uk).</summary>
    public static string DomainOf(string? urlOrHost)
    {
        if (string.IsNullOrWhiteSpace(urlOrHost)) return "";
        var s = urlOrHost.Trim().ToLowerInvariant();
        if (s.StartsWith("about:") || s.StartsWith("data:") || s.StartsWith("chrome")) return "";
        var m = Regex.Match(s, @"^[a-z][a-z0-9+.\-]*://([^/?#\s]+)");
        var host = m.Success ? m.Groups[1].Value : s.Split('/', '?', '#')[0];
        var at = host.LastIndexOf('@');
        if (at >= 0) host = host[(at + 1)..];
        var colon = host.LastIndexOf(':');
        if (colon > 0 && !host.EndsWith("]")) host = host[..colon];
        host = host.Trim('.');
        if (host.Length == 0) return "";
        if (host == "localhost" || Regex.IsMatch(host, @"^[\d.]+$") || host.StartsWith("[")) return host;
        var labels = host.Split('.');
        if (labels.Length <= 2) return host;
        var last = labels[^1];
        if (last.Length == 2 && SecondLevel.Contains(labels[^2]) && labels.Length >= 3)
            return string.Join('.', labels[^3..]);
        return string.Join('.', labels[^2..]);
    }

    /// <summary>Adds a cost to a domain and returns what the domain has cost since the last time the block was lifted.</summary>
    public double Charge(string domain, double usd)
    {
        if (string.IsNullOrEmpty(domain)) return 0;
        double spent;
        lock (_lock)
        {
            if (!_d.TryGetValue(domain, out var e)) _d[domain] = e = new DomainCostEntry();
            if (usd > 0) e.Total += usd;
            spent = e.Total - e.Offset;
            Save();
        }
        Changed?.Invoke();
        return spent;
    }

    /// <summary>What the domain has cost since the last time its block was lifted.</summary>
    public double Spent(string domain)
    {
        lock (_lock) return _d.TryGetValue(domain, out var e) ? e.Total - e.Offset : 0;
    }

    /// <summary>Everything the domain has ever cost, whatever blocks were lifted.</summary>
    public double Lifetime(string domain)
    {
        lock (_lock) return _d.TryGetValue(domain, out var e) ? e.Total : 0;
    }

    public bool IsBlocked(string domain)
    {
        if (string.IsNullOrEmpty(domain)) return false;
        lock (_lock) return _d.TryGetValue(domain, out var e) && e.Blocked;
    }

    public void Block(string domain)
    {
        if (string.IsNullOrEmpty(domain)) return;
        lock (_lock)
        {
            if (!_d.TryGetValue(domain, out var e)) _d[domain] = e = new DomainCostEntry();
            if (e.Blocked) return;
            e.Blocked = true;
            e.BlockedUtc = DateTime.UtcNow;
            Save();
        }
        Changed?.Invoke();
    }

    /// <summary>Lifts a block and starts counting the domain from zero again. Only the panel calls this.</summary>
    public bool Unblock(string domain, double lifetimeLimit = double.MaxValue)
    {
        lock (_lock)
        {
            if (!_d.TryGetValue(domain ?? "", out var e) || !e.Blocked) return false;
            if (e.Total >= lifetimeLimit) return false; // the lifetime ceiling cannot be reset by the button
            e.Blocked = false;
            e.BlockedUtc = null;
            e.Offset = e.Total;
            Save();
        }
        Changed?.Invoke();
        return true;
    }

    public IReadOnlyList<DomainCost> List(double lifetimeLimit = double.MaxValue)
    {
        lock (_lock)
            return _d.Select(kv => new DomainCost(kv.Key, kv.Value.Total - kv.Value.Offset, kv.Value.Total, kv.Value.Blocked, kv.Value.Blocked && kv.Value.Total >= lifetimeLimit))
                     .OrderByDescending(x => x.Blocked).ThenByDescending(x => x.Spent).ToList();
    }

    /// <summary>The first blocked domain mentioned in a text (web addresses or bare domain names), or null.</summary>
    public string? BlockedIn(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        lock (_lock) { if (!_d.Values.Any(v => v.Blocked)) return null; }
        foreach (Match m in Regex.Matches(text, @"(?:https?://)?(?:[a-zA-Z0-9\-]+\.)+[a-zA-Z]{2,}(?::\d+)?", RegexOptions.None))
        {
            var d = DomainOf(m.Value);
            if (d.Length > 0 && IsBlocked(d)) return d;
        }
        return null;
    }

    private void Save()
    {
        if (_file is null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            File.WriteAllText(_file, JsonSerializer.Serialize(_d, JsonOpts));
        }
        catch { /* best effort */ }
    }
}
