// OpenFill - Metadata: wersja 0.1, data 2026-10-23 11:41
using System.Text.Json;

namespace OpenFill.Core.Hosting;

/// <summary>
/// Local address-bar suggestions. Only HTTP(S) addresses are stored; credentials,
/// query strings and fragments are never persisted. Bounded most-recent-first list.
/// </summary>
public sealed class AddressHistory
{
    private readonly string _file;
    private readonly List<string> _urls = new();
    private const int MaxEntries = 200;

    public AddressHistory(string file)
    {
        _file = file;
        try
        {
            if (File.Exists(file))
            {
                var saved = JsonSerializer.Deserialize<string[]>(File.ReadAllText(file)) ?? [];
                foreach (var s in saved)
                {
                    var sanitized = SafeUrl(s);
                    if (sanitized is not null && !_urls.Contains(sanitized, StringComparer.Ordinal))
                        _urls.Add(sanitized);
                    if (_urls.Count >= MaxEntries) break;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _urls.Clear(); // Corrupt/unreadable history must not prevent browser startup.
        }
    }

    public IReadOnlyList<string> Suggestions => _urls.Select(Display).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public string Resolve(string typed)
    {
        for (var i = 0; i < _urls.Count; i++)
            if (string.Equals(Display(_urls[i]), typed.Trim(), StringComparison.OrdinalIgnoreCase))
                return _urls[i];
        return typed.Trim();
    }

    public bool Remember(string url)
    {
        var sanitized = SafeUrl(url);
        if (sanitized is null) return false;
        var index = _urls.FindIndex(u => string.Equals(u, sanitized, StringComparison.Ordinal));
        if (index == 0) return false;
        if (index >= 0) _urls.RemoveAt(index);
        _urls.Insert(0, sanitized);
        if (_urls.Count > MaxEntries) _urls.RemoveRange(MaxEntries, _urls.Count - MaxEntries);
        try
        {
            var folder = Path.GetDirectoryName(_file);
            if (!string.IsNullOrWhiteSpace(folder)) Directory.CreateDirectory(folder);
            var tmp = _file + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";
            try
            {
                File.WriteAllText(tmp, JsonSerializer.Serialize(_urls));
                File.Move(tmp, _file, overwrite: true);
            }
            finally { if (File.Exists(tmp)) File.Delete(tmp); }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // History is optional. Navigation must never fail because the disk is read-only.
        }
        return true;
    }

    private static string Display(string url)
    {
        var uri = new Uri(url);
        if (uri.Scheme != Uri.UriSchemeHttps) return url;
        var host = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            ? uri.Host[4..] : uri.Host;
        var port = uri.IsDefaultPort ? "" : ":" + uri.Port;
        var path = uri.AbsolutePath == "/" ? "" : uri.AbsolutePath;
        return host + port + path;
    }

    private static string? SafeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || url.Length > 4096 ||
            !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
            string.IsNullOrWhiteSpace(uri.Host)) return null;

        var builder = new UriBuilder(uri) { UserName = "", Password = "", Query = "", Fragment = "" };
        var path = uri.AbsolutePath;
        // Security tokens may also appear in callback/recovery paths: store only the origin.
        var sensitive = new[] { "/oauth", "/callback", "/token", "/reset", "/verify",
            "/recovery", "/magic-link", "/activate" };
        if (path.Length > 180 || sensitive.Any(s => path.Contains(s, StringComparison.OrdinalIgnoreCase)))
            builder.Path = "/";
        var sanitized = builder.Uri.AbsoluteUri;
        return sanitized.Length <= 512 ? sanitized : null;
    }
}
