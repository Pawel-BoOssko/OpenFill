// OpenFill - Metadata: wersja 0.3, data 2026-10-07 17:32
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OpenFill.Core.Agent;

/// <summary>One minute of spending on a domain (many model calls inside the same minute are added together).</summary>
public sealed class CostBucket
{
    /// <summary>Unix minute (seconds since 1970 / 60).</summary>
    public long Minute { get; set; }
    public double Usd { get; set; }
}

/// <summary>What one web domain has cost lately (a sliding window, 7 days by default; older costs are archived).</summary>
public sealed class DomainCostEntry
{
    /// <summary>Old format (before the sliding window): everything ever spent. Read once and converted to buckets.</summary>
    public double Total { get; set; }
    /// <summary>Old format: part of Total that was forgiven by an unblock.</summary>
    public double Offset { get; set; }
    public bool Blocked { get; set; }
    public DateTime? BlockedUtc { get; set; }
    /// <summary>When the block was last lifted by the user; "Spent" counts from then on.</summary>
    public DateTime? UnblockedUtc { get; set; }
    public List<CostBucket> Buckets { get; set; } = new();
    /// <summary>Sum of everything that aged out of the window (kept for information; it no longer counts).</summary>
    public double ArchivedTotal { get; set; }
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
/// Hard cost limit per web domain. Every model call is charged to the domain the browser is on. A domain that has cost the limit
/// is blocked: no task may work on it again until the person at the computer lifts the block in the OpenFill window.
/// Nothing that arrives over MCP can lift it. The "lifetime" ceiling is not eternal: only the last WindowDays days count
/// (7 by default); older costs are moved to costs_archive.ndjson and stop counting.
/// Several tasks may charge the same domain at once: every operation is under one lock.
/// </summary>
public sealed class CostGuard
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };
    private static readonly HashSet<string> SecondLevel = new(StringComparer.OrdinalIgnoreCase) { "co", "com", "org", "net", "gov", "edu", "ac" };

    private readonly string? _file;
    private readonly Func<DateTime> _clock;
    private readonly object _lock = new();
    private readonly Dictionary<string, DomainCostEntry> _d = new(StringComparer.OrdinalIgnoreCase);
    private int _windowDays;

    /// <summary>Raised (on any thread) when a total or a block changed.</summary>
    public event Action? Changed;

    /// <summary>How many days back costs still count (1..365).</summary>
    public int WindowDays
    {
        get { lock (_lock) return _windowDays; }
        set { lock (_lock) { _windowDays = Math.Clamp(value, 1, 365); } }
    }

    public CostGuard(string? file = null, int windowDays = 7, Func<DateTime>? clock = null)
    {
        _file = file;
        _clock = clock ?? (() => DateTime.UtcNow);
        _windowDays = Math.Clamp(windowDays, 1, 365);
        try
        {
            if (file is not null && File.Exists(file))
            {
                var loaded = JsonSerializer.Deserialize<Dictionary<string, DomainCostEntry>>(File.ReadAllText(file));
                if (loaded is not null) foreach (var (k, v) in loaded) _d[k] = v;
            }
        }
        catch { /* a damaged file starts empty; it is rewritten on the next charge */ }
        lock (_lock)
        {
            foreach (var e in _d.Values) Migrate(e);
            PruneLocked();
        }
    }

    private static long MinuteOf(DateTime utc) => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeSeconds() / 60;

    /// <summary>Old files kept Total and Offset only. They become two buckets stamped "now": they expire in a window from today.</summary>
    private void Migrate(DomainCostEntry e)
    {
        if (e.Buckets.Count > 0 || e.Total <= 0) { e.Total = 0; e.Offset = 0; return; }
        var now = _clock();
        var forgiven = Math.Clamp(e.Offset, 0, e.Total);
        var m = MinuteOf(now);
        if (forgiven > 0) e.Buckets.Add(new CostBucket { Minute = m - 2, Usd = forgiven });
        if (e.Total - forgiven > 0) e.Buckets.Add(new CostBucket { Minute = m, Usd = e.Total - forgiven });
        if (forgiven > 0) e.UnblockedUtc = now.AddMinutes(-1);
        e.Total = 0; e.Offset = 0;
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

    /// <summary>All web domains mentioned in a text (addresses or bare domain names), without duplicates.</summary>
    public static IReadOnlyList<string> DomainsIn(string? text)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return list;
        foreach (Match m in Regex.Matches(text, @"(?:https?://)?(?:[a-zA-Z0-9\-]+\.)+[a-zA-Z]{2,}(?::\d+)?", RegexOptions.None))
        {
            var d = DomainOf(m.Value);
            if (d.Length > 0 && !list.Contains(d, StringComparer.OrdinalIgnoreCase)) list.Add(d);
        }
        return list;
    }

    // ------------------------------------------------------------------ sums (call under the lock)

    private long WindowStartMinute() => MinuteOf(_clock().AddDays(-_windowDays));

    private static double WindowSumLocked(DomainCostEntry e, long fromMinute)
    {
        double sum = 0;
        foreach (var b in e.Buckets) if (b.Minute >= fromMinute) sum += b.Usd;
        return sum;
    }

    private double LifetimeLocked(DomainCostEntry e) => WindowSumLocked(e, WindowStartMinute());

    private double SpentLocked(DomainCostEntry e)
    {
        var from = WindowStartMinute();
        if (e.UnblockedUtc is { } u) from = Math.Max(from, MinuteOf(u) + 1);
        return WindowSumLocked(e, from);
    }

    /// <summary>Moves costs older than the window to the archive file and forgets them.</summary>
    private bool PruneLocked()
    {
        var start = WindowStartMinute();
        List<string>? archive = null;
        foreach (var (domain, e) in _d)
        {
            var old = e.Buckets.Where(b => b.Minute < start).ToList();
            if (old.Count == 0) continue;
            e.Buckets.RemoveAll(b => b.Minute < start);
            var sum = old.Sum(b => b.Usd);
            e.ArchivedTotal += sum;
            archive ??= new List<string>();
            archive.Add(JsonSerializer.Serialize(new
            {
                archivedUtc = _clock().ToString("o"),
                domain,
                from = DateTimeOffset.FromUnixTimeSeconds(old.Min(b => b.Minute) * 60).UtcDateTime.ToString("o"),
                to = DateTimeOffset.FromUnixTimeSeconds(old.Max(b => b.Minute) * 60).UtcDateTime.ToString("o"),
                usd = Math.Round(sum, 6)
            }));
        }
        if (archive is null) return false;
        if (_file is null) return true;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            File.AppendAllLines(Path.Combine(Path.GetDirectoryName(_file)!, "costs_archive.ndjson"), archive);
        }
        catch { /* the archive is best effort */ }
        return true;
    }

    // ------------------------------------------------------------------ public API

    /// <summary>Adds a cost to a domain and returns what the domain has cost since the last time the block was lifted (inside the window).</summary>
    public double Charge(string domain, double usd)
    {
        if (string.IsNullOrEmpty(domain)) return 0;
        double spent;
        lock (_lock)
        {
            if (!_d.TryGetValue(domain, out var e)) _d[domain] = e = new DomainCostEntry();
            if (usd > 0)
            {
                var m = MinuteOf(_clock());
                var last = e.Buckets.Count > 0 ? e.Buckets[^1] : null;
                if (last is not null && last.Minute == m) last.Usd += usd;
                else e.Buckets.Add(new CostBucket { Minute = m, Usd = usd });
            }
            PruneLocked();
            spent = SpentLocked(e);
            Save();
        }
        Changed?.Invoke();
        return spent;
    }

    /// <summary>What the domain has cost since the last time its block was lifted (inside the window).</summary>
    public double Spent(string domain)
    {
        lock (_lock)
        {
            if (PruneLocked()) Save();
            return _d.TryGetValue(domain, out var e) ? SpentLocked(e) : 0;
        }
    }

    /// <summary>Everything the domain has cost inside the window, whatever blocks were lifted.</summary>
    public double Lifetime(string domain)
    {
        lock (_lock)
        {
            if (PruneLocked()) Save();
            return _d.TryGetValue(domain, out var e) ? LifetimeLocked(e) : 0;
        }
    }

    /// <summary>What aged out of the window (archived) for the domain.</summary>
    public double Archived(string domain)
    {
        lock (_lock)
        {
            if (PruneLocked()) Save();
            return _d.TryGetValue(domain, out var e) ? e.ArchivedTotal : 0;
        }
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
            e.BlockedUtc = _clock();
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
            if (LifetimeLocked(e) >= lifetimeLimit) return false; // the ceiling cannot be reset by the button (it expires with the window)
            e.Blocked = false;
            e.BlockedUtc = null;
            e.UnblockedUtc = _clock();
            Save();
        }
        Changed?.Invoke();
        return true;
    }

    public IReadOnlyList<DomainCost> List(double lifetimeLimit = double.MaxValue)
    {
        lock (_lock)
        {
            if (PruneLocked()) Save();
            return _d.Select(kv => new { kv, life = LifetimeLocked(kv.Value), spent = SpentLocked(kv.Value) })
                     .Where(x => x.kv.Value.Blocked || x.life > 0)
                     .Select(x => new DomainCost(x.kv.Key, x.spent, x.life, x.kv.Value.Blocked, x.kv.Value.Blocked && x.life >= lifetimeLimit))
                     .OrderByDescending(x => x.Blocked).ThenByDescending(x => x.Spent).ToList();
        }
    }

    /// <summary>The first blocked domain mentioned in a text (web addresses or bare domain names), or null.</summary>
    public string? BlockedIn(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        lock (_lock) { if (!_d.Values.Any(v => v.Blocked)) return null; }
        foreach (var d in DomainsIn(text))
            if (IsBlocked(d)) return d;
        return null;
    }

    private void Save()
    {
        if (_file is null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            var tmp = _file + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(_d, JsonOpts));
            File.Move(tmp, _file, overwrite: true);
        }
        catch { /* best effort */ }
    }
}
