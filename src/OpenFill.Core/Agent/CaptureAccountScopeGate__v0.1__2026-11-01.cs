using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace OpenFill.Core.Agent;

// A05: account identity is accepted only from the signed-in account menu,
// not from arbitrary profile links, a login marker, or task-provided text.
public static class CaptureAccountScopeGate
{
    private static readonly Regex ProfileSlug = new(
        "^[A-Za-z0-9._-]{2,100}$", RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    public static string? Resolve(JsonObject? observation)
    {
        if (observation is null ||
            !Uri.TryCreate(observation["url"]?.ToString(), UriKind.Absolute, out var current) ||
            current.Scheme != Uri.UriSchemeHttps ||
            !current.Host.Equals("www.linkedin.com", StringComparison.OrdinalIgnoreCase) ||
            observation["authenticatedMarker"]?.ToString() != "true" ||
            observation["loginForm"]?.ToString() == "true" ||
            observation["accountSelfLinks"] is not JsonArray links ||
            links.Count is < 1 or > 5) return null;

        var distinct = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var link in links)
        {
            if (!Uri.TryCreate(link?.ToString(), UriKind.Absolute, out var profile) ||
                profile.Scheme != Uri.UriSchemeHttps ||
                !profile.Host.Equals("www.linkedin.com", StringComparison.OrdinalIgnoreCase) ||
                !profile.AbsolutePath.StartsWith("/in/", StringComparison.OrdinalIgnoreCase))
                return null;
            var slug = Uri.UnescapeDataString(profile.AbsolutePath[4..].TrimEnd('/'));
            if (!ProfileSlug.IsMatch(slug)) return null;
            distinct.Add("linkedin:in/" + slug.ToLowerInvariant());
        }
        return distinct.Count == 1 ? distinct.Single() : null;
    }
}
