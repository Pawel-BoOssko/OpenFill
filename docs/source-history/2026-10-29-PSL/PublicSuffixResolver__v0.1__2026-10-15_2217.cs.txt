// OpenFill - Metadata: version 0.1, 2026-10-15 22:17
// Offline Mozilla Public Suffix List snapshot, including the PRIVATE section.
// Only used to classify site families. Credential filling remains exact-origin.
using System.Globalization;
using System.Net;
using System.Reflection;

namespace OpenFill.Core.Agent;

public static class PublicSuffixResolver
{
    private const string ResourceName = "OpenFill.Assets.public_suffix_list.dat";
    private static readonly Lazy<Rules> Snapshot = new(Load, LazyThreadSafetyMode.ExecutionAndPublication);
    private sealed record Rules(HashSet<string> Exact, HashSet<string> Wildcards, HashSet<string> Exceptions);
    private static readonly IdnMapping Idn = new();

    private static string Normalize(string host)
    {
        host = host.Trim().TrimEnd('.').ToLowerInvariant();
        if (IPAddress.TryParse(host, out var ip)) return ip.ToString().ToLowerInvariant();
        var labels = host.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (labels.Length == 0) return "";
        try { return string.Join(".", labels.Select(label => Idn.GetAscii(label).ToLowerInvariant())); }
        catch (ArgumentException) { return ""; }
    }

    private static Rules Load()
    {
        using var stream = typeof(PublicSuffixResolver).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("Embedded Public Suffix List is missing");
        using var reader = new StreamReader(stream);
        var exact = new HashSet<string>(StringComparer.Ordinal);
        var wildcard = new HashSet<string>(StringComparer.Ordinal);
        var exceptions = new HashSet<string>(StringComparer.Ordinal);
        while (reader.ReadLine() is { } line)
        {
            line = line.Trim();
            if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal)) continue;
            var exception = line.StartsWith('!');
            var star = line.StartsWith("*.", StringComparison.Ordinal);
            var rule = Normalize(line[(exception ? 1 : star ? 2 : 0)..]);
            if (rule.Length == 0) continue;
            if (exception) exceptions.Add(rule);
            else if (star) wildcard.Add(rule);
            else exact.Add(rule);
        }
        if (exact.Count < 5000 || wildcard.Count < 50 || exceptions.Count < 5)
            throw new InvalidOperationException("Embedded Public Suffix List is incomplete");
        return new Rules(exact, wildcard, exceptions);
    }

    public static string RegistrableDomain(string host)
    {
        var normalized = Normalize(host);
        if (normalized.Length == 0) return "";
        if (IPAddress.TryParse(normalized, out _) || normalized == "localhost") return normalized;
        var labels = normalized.Split('.');
        if (labels.Length == 1) return normalized;
        var rules = Snapshot.Value;
        var suffixCount = 1; // PSL prevailing wildcard rule '*'.
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
