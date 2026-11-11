// OF-IMP-20261031 D03: verify the initial daily prices before creating a durable change plan.
// Candidate: requires a separately persisted, complete WebView2 capture per listing/day.
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace OpenFill.Core.Agent;

public sealed record AirbnbPriceCaptureReference(DateOnly Date, string Path, string Sha256);

public sealed class AirbnbPriceSourceEvidenceGate(OperationalMemoryStore memory)
{
    private static JsonObject Block(string code) => new()
    {
        ["status"] = "blocked", ["reason_code"] = code
    };

    // Airbnb locale hosts use the same hosting calendar. Reject lookalike hosts and unrelated routes.
    public static bool IsTrustedCalendarUrl(string? raw)
    {
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !(uri.Host.Equals("airbnb.com", StringComparison.OrdinalIgnoreCase) ||
              uri.Host.EndsWith(".airbnb.com", StringComparison.OrdinalIgnoreCase)))
            return false;
        var path = uri.AbsolutePath;
        return path.StartsWith("/hosting/", StringComparison.OrdinalIgnoreCase) ||
               path.Equals("/multicalendar", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("/multicalendar/", StringComparison.OrdinalIgnoreCase);
    }

    public (JsonObject Result, IReadOnlyList<AirbnbPriceObservation> Observations) Validate(
        string accountId, string listingId, string currency, string ratePlan,
        DateOnly firstDay, DateOnly lastDay,
        IReadOnlyList<AirbnbPriceCaptureReference> references)
    {
        var empty = Array.Empty<AirbnbPriceObservation>();
        if (new[] { accountId, listingId, currency }.Any(string.IsNullOrWhiteSpace))
            return (Block("source_identity_missing"), empty);
        var days = lastDay.DayNumber - firstDay.DayNumber + 1;
        if (days is < 1 or > 366 || references.Count != days)
            return (Block("source_date_coverage_incomplete"), empty);
        if (references.Select(x => x.Date).Distinct().Count() != days ||
            references.Any(x => x.Date < firstDay || x.Date > lastDay))
            return (Block("source_date_coverage_incomplete"), empty);
        var dbDir = Path.GetDirectoryName(memory.DatabasePath)!;
        var instanceRoot = Path.GetFileName(dbDir).Equals("memory", StringComparison.OrdinalIgnoreCase)
            ? Directory.GetParent(dbDir)!.FullName : dbDir;
        var capturesRoot = Path.GetFullPath(Path.Combine(instanceRoot, "captures")) +
                           Path.DirectorySeparatorChar;
        var result = new List<AirbnbPriceObservation>();
        foreach (var reference in references.OrderBy(x => x.Date))
        {
            if (string.IsNullOrWhiteSpace(reference.Path) ||
                !Path.IsPathFullyQualified(reference.Path))
                return (Block("source_path_invalid"), empty);
            var path = Path.GetFullPath(reference.Path);
            if (!path.StartsWith(capturesRoot, StringComparison.OrdinalIgnoreCase))
                return (Block("source_outside_captures"), empty);
            if (!File.Exists(path)) return (Block("source_file_missing"), empty);
            var info = new FileInfo(path);
            if (info.Length is < 10 or > 1048576)
                return (Block("source_size_invalid"), empty);
            if (string.IsNullOrWhiteSpace(reference.Sha256) || reference.Sha256.Length != 64 ||
                reference.Sha256.Any(ch => !Uri.IsHexDigit(ch)))
                return (Block("source_hash_invalid"), empty);
            JsonObject proof;
            string hash;
            try
            {
                var bytes = File.ReadAllBytes(path);
                hash = Convert.ToHexString(SHA256.HashData(bytes));
                if (!hash.Equals(reference.Sha256, StringComparison.OrdinalIgnoreCase))
                    return (Block("source_hash_mismatch"), empty);
                proof = JsonNode.Parse(bytes) as JsonObject ?? throw new FormatException();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                return (Block("source_unreadable:" + ex.GetType().Name), empty);
            }
            if (proof["provider"]?.ToString() != "airbnb" ||
                proof["source_kind"]?.ToString() != "webview2_dom" ||
                proof["scope"]?.ToString() != "single_listing_day" ||
                !bool.TryParse(proof["complete"]?.ToString(), out var complete) || !complete)
                return (Block("source_scope_incomplete"), empty);
            if (!IsTrustedCalendarUrl(proof["url"]?.ToString()))
                return (Block("source_url_unverified"), empty);
            foreach (var (field, expected) in new[] {
                ("account_id", accountId), ("listing_id", listingId),
                ("currency", currency), ("rate_plan", ratePlan),
                ("date", reference.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            })
                if (!string.Equals(proof[field]?.ToString(), expected, StringComparison.Ordinal))
                    return (Block("source_identity_mismatch:" + field), empty);
            if (!DateTimeOffset.TryParse(proof["observed_at_utc"]?.ToString(),
                    CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var observed) ||
                observed.Offset != TimeSpan.Zero ||
                observed > DateTimeOffset.UtcNow.AddMinutes(2) ||
                observed < DateTimeOffset.UtcNow.AddMinutes(-15))
                return (Block("source_stale_or_invalid_time"), empty);
            if (!decimal.TryParse(proof["price"]?.ToString(), NumberStyles.Number,
                    CultureInfo.InvariantCulture, out var price) ||
                price <= 0 || price > 100000000)
                return (Block("source_price_invalid"), empty);
            result.Add(new AirbnbPriceObservation(reference.Date, price,
                path + "#sha256=" + hash));
        }
        return (new JsonObject { ["status"] = "ready", ["days"] = result.Count }, result);
    }
}
