using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace OpenFill.Core.Agent;

// A05: fail-closed selection of a technical recipe and independent validation of cached data.
public sealed record CaptureRecipeScope(
    string Host, string Route, string Operation = "capture",
    string? AccountScope = null, string? ViewSignature = null,
    string? ContentFingerprint = null, bool Authenticated = false);

public static class CaptureRecipeScopeGate
{
    public static bool IsSensitiveHost(string host) =>
        host.Equals("web.whatsapp.com", StringComparison.OrdinalIgnoreCase) ||
        host.Equals("gmail.com", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".google.com", StringComparison.OrdinalIgnoreCase) ||
        host.Equals("linkedin.com", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".linkedin.com", StringComparison.OrdinalIgnoreCase) ||
        host.Equals("airbnb.com", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".airbnb.com", StringComparison.OrdinalIgnoreCase);

    public static CaptureRecipe? Select(IEnumerable<CaptureRecipe> recipes, CaptureRecipeScope request)
    {
        if (string.IsNullOrWhiteSpace(request.Host) || string.IsNullOrWhiteSpace(request.Route))
            return null;
        // An account-bearing service cannot use an unscoped historical recipe.
        if ((request.Authenticated || IsSensitiveHost(request.Host)) &&
            (string.IsNullOrWhiteSpace(request.AccountScope) ||
             string.IsNullOrWhiteSpace(request.ViewSignature))) return null;
        var scoped = recipes.Where(r =>
            string.Equals(r.Host, request.Host, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(r.Path, request.Route, StringComparison.Ordinal) &&
            string.Equals(r.Operation, request.Operation, StringComparison.Ordinal) &&
            string.Equals(r.Confidence, "verified", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(r.AccountScope ?? "", request.AccountScope ?? "", StringComparison.Ordinal) &&
            (string.IsNullOrWhiteSpace(r.ViewSignature)
                ? !request.Authenticated && !IsSensitiveHost(request.Host)
                : string.Equals(r.ViewSignature, request.ViewSignature, StringComparison.OrdinalIgnoreCase)) &&
            // Do not reuse an account-specific method without an exact view signature.
            (!(request.Authenticated || IsSensitiveHost(request.Host)) ||
                !string.IsNullOrWhiteSpace(r.ViewSignature)));
        return scoped.OrderByDescending(r => !string.IsNullOrWhiteSpace(r.ViewSignature))
            .ThenByDescending(r => r.TaskSuccesses)
            .ThenBy(r => r.TaskFailures)
            .ThenBy(r => r.LastElapsedMs).FirstOrDefault();
    }

    // Cache reuse is distinct from selector reuse: data must still be current and complete.
    public static string CheckCachedCapture(JsonObject manifest, CaptureRecipeScope request,
        string capturesRoot, TimeSpan maxAge)
    {
        if (maxAge <= TimeSpan.Zero) return "cache_age_policy_missing";
        if (!Uri.TryCreate(manifest["source_url"]?.ToString(), UriKind.Absolute, out var uri) ||
            !uri.Host.Equals(request.Host, StringComparison.OrdinalIgnoreCase) ||
            !uri.AbsolutePath.Equals(request.Route, StringComparison.Ordinal))
            return "cache_route_mismatch";
        if (string.IsNullOrWhiteSpace(request.ViewSignature) ||
            !string.Equals(manifest["view_signature"]?.ToString(), request.ViewSignature,
                StringComparison.OrdinalIgnoreCase))
            return "cache_view_mismatch";
        if (string.IsNullOrWhiteSpace(request.ContentFingerprint) ||
            !string.Equals(manifest["content_fingerprint"]?.ToString(), request.ContentFingerprint,
                StringComparison.OrdinalIgnoreCase))
            return "cache_content_changed_or_unverified";
        if (request.Authenticated || IsSensitiveHost(request.Host) || !string.IsNullOrWhiteSpace(request.AccountScope))
        {
            if (string.IsNullOrWhiteSpace(request.AccountScope) ||
                !string.Equals(manifest["account_scope"]?.ToString(), request.AccountScope,
                    StringComparison.Ordinal))
                return "cache_account_unverified_or_changed";
        }
        if (manifest["completeness"]?.ToString() != "complete" ||
            manifest["all_pages_complete"]?.ToString() != "true" ||
            manifest["virtualized"]?.ToString() != "false" ||
            manifest["lazy_not_loaded"]?.ToString() != "false")
            return "cache_scope_incomplete";
        if (!DateTimeOffset.TryParse(manifest["observed_at_utc"]?.ToString(), out var observed) ||
            observed > DateTimeOffset.UtcNow.AddSeconds(30) ||
            DateTimeOffset.UtcNow - observed > maxAge)
            return "cache_stale_or_invalid_time";
        if (manifest["files"] is not JsonArray files || files.Count == 0)
            return "cache_files_missing";
        var root = Path.GetFullPath(capturesRoot).TrimEnd(Path.DirectorySeparatorChar) +
                   Path.DirectorySeparatorChar;
        foreach (var entry in files)
        {
            if (entry is not JsonObject f || string.IsNullOrWhiteSpace(f["path"]?.ToString()) ||
                !Path.IsPathFullyQualified(f["path"]!.ToString()))
                return "cache_file_path_invalid";
            var path = Path.GetFullPath(f["path"]!.ToString());
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return "cache_file_outside_root";
            if (!File.Exists(path)) return "cache_file_missing";
            var hash = f["sha256"]?.ToString();
            try
            {
                if (string.IsNullOrWhiteSpace(hash) ||
                    !Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))
                        .Equals(hash, StringComparison.OrdinalIgnoreCase))
                    return "cache_file_hash_mismatch";
            }
            catch (IOException) { return "cache_file_unreadable"; }
            catch (UnauthorizedAccessException) { return "cache_file_unreadable"; }
        }
        return "ready";
    }
}
