// OpenFill - Metadata: version 0.2, 2026-10-29 19:05
// Mozilla PSL embedded snapshot + bounded HTTPS refresh to per-user cache.
// Exact-origin credential rules remain unchanged.
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;

namespace OpenFill.Core.Agent;

public static class PublicSuffixResolver
{
    private const string ResourceName = "OpenFill.Assets.public_suffix_list.dat";
    private const string Upstream = "https://publicsuffix.org/list/public_suffix_list.dat";
    private const int MaxDownloadBytes = 4_000_000;
    private sealed record Rules(HashSet<string> Exact, HashSet<string> Wildcards, HashSet<string> Exceptions);
    private static readonly IdnMapping Idn = new();
    private static readonly Lazy<Rules> Embedded = new(LoadEmbedded, LazyThreadSafetyMode.ExecutionAndPublication);
    private static Rules? _latest;
    private static string? _cache;
    private static int _updaterStarted;
    private static readonly HttpClient Http = new(new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromSeconds(5) })
    { Timeout = TimeSpan.FromSeconds(22) };
    public static string SnapshotSource { get; private set; } = "embedded";
    public static string? LastRefreshError { get; private set; }

    // Never blocks the application start; online failures fall back to last valid cache or embedded PSL.
    public static void EnableAutoRefresh(string directory)
    {
        if (Interlocked.CompareExchange(ref _updaterStarted, 1, 0) != 0) return;
        _cache = Path.Combine(directory, "public_suffix_list_current.dat");
        try
        {
            if (File.Exists(_cache))
            {
                using var reader = File.OpenText(_cache);
                var cacheRules = ReadRules(reader);
                Volatile.Write(ref _latest, cacheRules);
                SnapshotSource = "cached";
            }
        }
        catch (Exception e) { LastRefreshError = "cache:" + e.GetType().Name; }
        _ = Task.Run(async () =>
        {
            while (true)
            {
                try { await RefreshIfDueAsync().ConfigureAwait(false); }
                catch (Exception e) { LastRefreshError = "refresh:" + e.GetType().Name; }
                await Task.Delay(TimeSpan.FromHours(12)).ConfigureAwait(false);
            }
        });
    }

    public static async Task<bool> RefreshIfDueAsync(CancellationToken ct = default)
    {
        var cache = _cache;
        if (cache is null) return false;
        if (File.Exists(cache) && DateTime.UtcNow - File.GetLastWriteTimeUtc(cache) < TimeSpan.FromDays(1))
            return false;
        using var result = await Http.GetAsync(Upstream, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        result.EnsureSuccessStatusCode();
        if (result.Content.Headers.ContentLength is > MaxDownloadBytes)
            throw new InvalidDataException("PSL response exceeds 4 MB");
        await using var body = await result.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var memory = new MemoryStream();
        var buffer = new byte[16_384];
        int count;
        while ((count = await body.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            if (memory.Length + count > MaxDownloadBytes)
                throw new InvalidDataException("PSL exceeded maximum download size");
            memory.Write(buffer, 0, count);
        }
        var raw = Encoding.UTF8.GetString(memory.ToArray());
        if (!raw.Contains("BEGIN ICANN DOMAINS", StringComparison.Ordinal) ||
            !raw.Contains("BEGIN PRIVATE DOMAINS", StringComparison.Ordinal))
            throw new InvalidDataException("PSL official sections missing");
        using var reader = new StringReader(raw);
        var rules = ReadRules(reader); // no changes if invalid
        var parent = Path.GetDirectoryName(cache)!;
        Directory.CreateDirectory(parent);
        var temp = cache + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temp, raw, new UTF8Encoding(false), ct).ConfigureAwait(false);
            File.Move(temp, cache, true); // atomic replacement on same drive
        }
        finally { try { if (File.Exists(temp)) File.Delete(temp); } catch { } }
        Volatile.Write(ref _latest, rules);
        SnapshotSource = "refreshed";
        LastRefreshError = null;
        return true;
    }

    private static string Normalize(string host)
    {
        host = host.Trim().TrimEnd('.').ToLowerInvariant();
        if (IPAddress.TryParse(host, out var ip)) return ip.ToString().ToLowerInvariant();
        var labels = host.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (labels.Length == 0) return "";
        try { return string.Join(".", labels.Select(label => Idn.GetAscii(label).ToLowerInvariant())); }
        catch (ArgumentException) { return ""; }
    }

    private static Rules LoadEmbedded()
    {
        using var stream = typeof(PublicSuffixResolver).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("Embedded Public Suffix List missing");
        using var reader = new StreamReader(stream);
        return ReadRules(reader);
    }

    private static Rules ReadRules(TextReader reader)
    {
        var exact = new HashSet<string>(StringComparer.Ordinal);
        var wildcard = new HashSet<string>(StringComparer.Ordinal);
        var exceptions = new HashSet<string>(StringComparer.Ordinal);
        while (reader.ReadLine() is { } line)
        {
            line = line.Trim();
            if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal)) continue;
            var except = line.StartsWith('!');
            var star = line.StartsWith("*.", StringComparison.Ordinal);
            var rule = Normalize(line[(except ? 1 : star ? 2 : 0)..]);
            if (rule.Length == 0) continue;
            if (except) exceptions.Add(rule);
            else if (star) wildcard.Add(rule);
            else exact.Add(rule);
        }
        if (exact.Count < 5000 || wildcard.Count < 50 || exceptions.Count < 5)
            throw new InvalidDataException("Public Suffix List incomplete or corrupt");
        return new Rules(exact, wildcard, exceptions);
    }

    public static string RegistrableDomain(string host)
    {
        var normalized = Normalize(host);
        if (normalized.Length == 0) return "";
        if (IPAddress.TryParse(normalized, out _) || normalized == "localhost") return normalized;
        var labels = normalized.Split('.');
        if (labels.Length == 1) return normalized;
        var rules = Volatile.Read(ref _latest) ?? Embedded.Value;
        var suffixCount = 1;
        var exceptionCount = 0;
        for (var i = 0; i < labels.Length; i++)
        {
            var suffix = string.Join(".", labels[i..]);
            var count = labels.Length - i;
            if (rules.Exceptions.Contains(suffix))
                exceptionCount = Math.Max(exceptionCount, count);
            if (rules.Exact.Contains(suffix))
                suffixCount = Math.Max(suffixCount, count);
            if (i > 0 && rules.Wildcards.Contains(suffix))
                suffixCount = Math.Max(suffixCount, count + 1);
        }
        if (exceptionCount > 0) suffixCount = exceptionCount - 1;
        var countLabels = Math.Min(labels.Length, suffixCount + 1);
        return string.Join(".", labels[^countLabels..]);
    }
}
