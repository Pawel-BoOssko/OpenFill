// OpenFill - Metadata: wersja 0.1, data 2026-10-07 17:32
using OpenFill.Core.Agent;

namespace OpenFill.Core.Hosting;

/// <summary>Who holds a web domain: the task, where it came from, what it does, since when.</summary>
public sealed record LockHolder(string TaskKey, string Source, string What, string Domain, DateTime SinceUtc);

/// <summary>
/// One task at a time per web domain. While a task works on a domain (it is running, or waits for an answer), no other task
/// may touch that domain. There is no "go in anyway": the lock ends when the task ends or the person stops it.
/// Several tasks on different domains run side by side. All operations are under one lock.
/// </summary>
public sealed class DomainLocks
{
    private readonly object _lock = new();
    private readonly Dictionary<string, LockHolder> _byDomain = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Takes all the domains for the task, or none: returns the holder of the first domain that is taken by another task.</summary>
    public LockHolder? TryAcquire(string taskKey, string source, string what, IEnumerable<string> domains)
    {
        var wanted = domains.Where(d => !string.IsNullOrEmpty(d)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        lock (_lock)
        {
            foreach (var d in wanted)
                if (_byDomain.TryGetValue(d, out var h) && h.TaskKey != taskKey) return h;
            var now = DateTime.UtcNow;
            foreach (var d in wanted)
                if (!_byDomain.ContainsKey(d)) _byDomain[d] = new LockHolder(taskKey, source, what, d, now);
            return null;
        }
    }

    /// <summary>Same for one domain that the task reached while working (a link, a redirect, a navigate).</summary>
    public LockHolder? TryAcquireOne(string taskKey, string source, string what, string domain) =>
        TryAcquire(taskKey, source, what, new[] { domain });

    public void ReleaseAll(string taskKey)
    {
        lock (_lock)
        {
            foreach (var d in _byDomain.Where(kv => kv.Value.TaskKey == taskKey).Select(kv => kv.Key).ToList())
                _byDomain.Remove(d);
        }
    }

    public LockHolder? HolderOf(string domain)
    {
        lock (_lock) return _byDomain.TryGetValue(domain, out var h) ? h : null;
    }

    public IReadOnlyList<LockHolder> Held()
    {
        lock (_lock) return _byDomain.Values.OrderBy(h => h.SinceUtc).ToList();
    }

    /// <summary>Domains a task holds.</summary>
    public IReadOnlyList<string> DomainsOf(string taskKey)
    {
        lock (_lock) return _byDomain.Where(kv => kv.Value.TaskKey == taskKey).Select(kv => kv.Key).ToList();
    }

    /// <summary>The refusal text for the caller (a model) or the panel. It says who, what and since when, and that there is no way around it.</summary>
    public static string RefusalText(LockHolder h, bool midTask = false, DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        var head = midTask
            ? $"REFUSED: the site {h.Domain} is in use by another task, so this task may not go there."
            : $"REFUSED: the site {h.Domain} is in use. Your task was NOT started.";
        var mins = Math.Max(0, (int)(now - h.SinceUtc).TotalMinutes);
        var who = h.Source == "mcp" ? $"another chat, task {h.TaskKey}" : $"the user, typed in the OpenFill window (task {h.TaskKey})";
        var since = h.SinceUtc.ToLocalTime().ToString("HH:mm");
        return head + "\n" +
               $"In use by {who}, since {since} ({mins} min ago), doing: \"{Short(h.What, 160)}\".\n" +
               "That task may be in the middle of a job that takes several tasks (for example a sign-up), so while it runs nobody else may work on this site. There is no way around this.\n" +
               "You can try again later.\n" +
               $"Note: you may be duplicating the work of {who}.";
    }

    private static string Short(string s, int n)
    {
        s = System.Text.RegularExpressions.Regex.Replace(s ?? "", @"\s+", " ").Trim();
        return s.Length <= n ? s : s[..(n - 3)] + "...";
    }
}
