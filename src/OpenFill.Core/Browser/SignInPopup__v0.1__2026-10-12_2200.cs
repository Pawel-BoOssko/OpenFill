// OpenFill - Metadata: wersja 0.1, data 2026-10-12 22:00
namespace OpenFill.Core.Browser;

/// <summary>Sign-in windows that must stay a real pop-up even when the page gives no size (the page waits for the result via window.opener).</summary>
public static class SignInPopup
{
    public static bool Looks(string? uri)
    {
        if (string.IsNullOrEmpty(uri) || !Uri.TryCreate(uri, UriKind.Absolute, out var u)) return false;
        var q = u.Query ?? "";
        if (q.IndexOf("display=popup", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        var h = u.Host.ToLowerInvariant();
        if (h == "accounts.google.com" || h == "appleid.apple.com" || h == "idmsa.apple.com" || h == "login.microsoftonline.com" || h == "login.live.com") return true;
        if (h.EndsWith("facebook.com") && u.AbsolutePath.Contains("/dialog/")) return true;
        if ((h == "api.twitter.com" || h == "x.com" || h == "twitter.com") && u.AbsolutePath.Contains("oauth")) return true;
        return false;
    }
}
