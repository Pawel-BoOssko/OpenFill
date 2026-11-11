using System.Text.RegularExpressions;

namespace OpenFill.Core.Agent;

/// <summary>Only omit unrelated provider-specific schemas when one explicit URL host is unambiguous.
/// Generic browser, file, authentication and task-control tools are always retained.</summary>
public static class SingleHostToolProfile
{
    public static Tool[]? Select(IReadOnlyList<Tool> tools, string task)
    {
        if (string.IsNullOrWhiteSpace(task)) return null;
        MatchCollection urls;
        try
        {
            urls = Regex.Matches(task, @"https?://[^\s<>'""\]\)]+",
                RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(50));
        }
        catch (RegexMatchTimeoutException) { return null; }
        if (urls.Count == 0 || urls.Count > 20) return null;

        string? onlyHost = null;
        foreach (Match url in urls)
        {
            if (!Uri.TryCreate(url.Value.TrimEnd('.', ',', ';'), UriKind.Absolute, out var uri)
                || (uri.Scheme != "https" && uri.Scheme != "http")) return null;
            var host = uri.Host;
            if (onlyHost is not null && !onlyHost.Equals(host, StringComparison.OrdinalIgnoreCase))
                return null;
            onlyHost = host;
        }
        if (onlyHost is null) return null;

        bool HasProvider(string domain, string label) =>
            onlyHost.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
            onlyHost.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase) ||
            task.Contains(label, StringComparison.OrdinalIgnoreCase);
        var airbnb = HasProvider("airbnb.com", "airbnb");
        var linkedin = HasProvider("linkedin.com", "linkedin");
        var whatsapp = HasProvider("whatsapp.com", "whatsapp");
        var meta = HasProvider("facebook.com", "facebook") ||
                   HasProvider("instagram.com", "instagram") ||
                   HasProvider("youtube.com", "youtube") ||
                   task.Contains("meta business", StringComparison.OrdinalIgnoreCase);

        return tools.Where(t =>
            (!t.Name.StartsWith("airbnb_", StringComparison.Ordinal) || airbnb) &&
            (!t.Name.StartsWith("linkedin_", StringComparison.Ordinal) || linkedin) &&
            (!t.Name.StartsWith("whatsapp_", StringComparison.Ordinal) || whatsapp) &&
            (t.Name != "google_meta_view_audit" || meta)).ToArray();
    }
}
