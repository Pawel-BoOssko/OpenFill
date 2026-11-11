// D04 2026-11-04: Google/Meta compact scoped read-only view adapter.
// URL-scope evidence is NOT a logged-in user proof and NEVER authorizes a write.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using OpenFill.Core.Browser;

namespace OpenFill.Core.Agent;

public static class DomainReadAuditService
{
    private static readonly Dictionary<string, HashSet<string>> Intents = new(StringComparer.Ordinal)
    {
        ["youtube_studio"] = ["audit_channel_readonly", "upload_schedule_short"],
        ["meta_business"] = ["audit_page_readonly", "reel_editor_workflow"]
    };

    private static JsonObject Error(string code) =>
        new() { ["status"] = "blocked", ["reason_code"] = code,
                ["read_only"] = true, ["authenticated"] = false };

    public static JsonObject Inspect(string provider, string intent,
        string expectedScopeId, string scopeKey, PageModel page)
    {
        if (!Intents.TryGetValue(provider, out var allowed) || !allowed.Contains(intent))
            return Error("unsupported_provider_intent");
        if (string.IsNullOrWhiteSpace(expectedScopeId) ||
            expectedScopeId.Length > 128 || expectedScopeId.Any(ch =>
                !(char.IsLetterOrDigit(ch) || ch is '_' or '-')))
            return Error("invalid_expected_scope");
        if (!Uri.TryCreate(page.Url, UriKind.Absolute, out var uri) ||
            uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Fragment))
            return Error("invalid_current_url");

        string? observed = null;
        if (provider == "youtube_studio")
        {
            if (uri.Host != "studio.youtube.com" ||
                scopeKey != "channel_id" ||
                !uri.AbsolutePath.StartsWith("/channel/", StringComparison.OrdinalIgnoreCase))
                return Error("youtube_channel_route_required");
            observed = uri.AbsolutePath["/channel/".Length..].Split('/')[0];
        }
        else
        {
            if (uri.Host != "business.facebook.com" || scopeKey is not
                ("business_id" or "asset_id") ||
                !uri.AbsolutePath.StartsWith("/latest/", StringComparison.OrdinalIgnoreCase))
                return Error("meta_business_route_required");
            var matches = uri.Query.TrimStart('?').Split('&',
                StringSplitOptions.RemoveEmptyEntries).Select(x => x.Split('=', 2))
                .Where(x => x.Length == 2 &&
                    x[0].Equals(scopeKey, StringComparison.OrdinalIgnoreCase))
                .Select(x => Uri.UnescapeDataString(x[1])).Distinct().ToArray();
            if (matches.Length != 1) return Error("scope_query_missing_or_ambiguous");
            observed = matches[0];
        }
        if (!string.Equals(observed, expectedScopeId, StringComparison.Ordinal))
            return Error("account_scope_mismatch");

        var headings = (page.Raw["headings"] as JsonArray)?.Take(12)
            .Select(x => (x?.ToString() ?? "")[..Math.Min(160, x?.ToString().Length ?? 0)])
            .Where(x => x.Length > 0).ToArray() ?? [];
        var controls = new List<string>();
        void Add(JsonArray? arr, string key)
        {
            if (arr is null) return;
            foreach (var item in arr.OfType<JsonObject>())
            {
                var value = item[key]?.ToString() ?? "";
                if (value.Length > 0)
                    controls.Add(value.Length <= 120 ? value : value[..120]);
                if (controls.Count >= 50) break;
            }
        }
        Add(page.Raw["buttons"] as JsonArray, "text");
        Add(page.Raw["looseFields"] as JsonArray, "label");
        if (page.Raw["forms"] is JsonArray forms)
            foreach (var form in forms.OfType<JsonObject>())
            {
                Add(form["fields"] as JsonArray, "label");
                Add(form["buttons"] as JsonArray, "text");
            }
        controls = controls.Take(50).ToList();
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            provider + "|" + intent + "|" + scopeKey + "|" + observed + "|" +
            uri.AbsolutePath + "|" + string.Join("|", headings) + "|" +
            string.Join("|", controls))));
        return new JsonObject
        {
            ["status"] = "observed_readonly",
            ["provider"] = provider, ["intent"] = intent,
            ["scope_status"] = "url_scope_match_only",
            ["authenticated"] = false,
            ["read_only"] = true,
            ["url"] = uri.GetLeftPart(UriPartial.Path),
            ["scope_key"] = scopeKey, ["scope_id"] = observed,
            ["completeness"] = page.Raw["truncated"]?.ToString() == "true"
                ? "partial_visible" : "visible_only",
            ["view_signature"] = fingerprint,
            ["headings"] = new JsonArray(headings.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()),
            ["controls"] = new JsonArray(controls.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()),
            ["action_attempted"] = false
        };
    }
}
