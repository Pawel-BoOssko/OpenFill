// OpenFill SOURCE: the authenticated page must be verified before any requested export is written.
using System.Text.Json.Nodes;

namespace OpenFill.Core.Agent;

public static class PageAuthenticationGate
{
    private static bool IsLinkedIn(Uri? uri)
        => uri is not null && (uri.Host.Equals("linkedin.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith(".linkedin.com", StringComparison.OrdinalIgnoreCase));

    private static bool IsLinkedInLogin(Uri uri)
    {
        var path = uri.AbsolutePath.TrimEnd('/').ToLowerInvariant();
        return path is "/login" or "/signup" or "/checkpoint/challenge" or "/uas/login"
            || path.StartsWith("/checkpoint/", StringComparison.Ordinal)
            || path.StartsWith("/uas/login", StringComparison.Ordinal);
    }

    private static bool IsLinkedInAccountArea(Uri uri)
    {
        var path = uri.AbsolutePath.ToLowerInvariant();
        return path.StartsWith("/messaging", StringComparison.Ordinal)
            || path.StartsWith("/feed", StringComparison.Ordinal)
            || path.StartsWith("/mynetwork", StringComparison.Ordinal)
            || path.StartsWith("/in/", StringComparison.Ordinal);
    }

    public static JsonObject Check(JsonObject captured, string? expectedUrl = null,
        bool requireAuthenticated = false, bool allowLoginPage = false)
    {
        var current = captured["url"]?.ToString() ?? "";
        Uri.TryCreate(current, UriKind.Absolute, out var uri);
        if (uri is null) return new JsonObject { ["status"] = "error", ["code"] = "MISSING_URL",
            ["message"] = "Browser did not return a valid current URL." };
        var login = (IsLinkedIn(uri) && IsLinkedInLogin(uri))
            || captured["loginForm"]?.GetValue<bool>() == true;
        var matched = captured["authenticatedMarker"]?.GetValue<bool>() == true;
        var protectedLinkedIn = IsLinkedIn(uri) && IsLinkedInAccountArea(uri);
        var required = requireAuthenticated || protectedLinkedIn;
        if (login && !allowLoginPage)
        {
            return new JsonObject {
                ["status"] = "authentication_required", ["code"] = "LOGIN_FIRST",
                ["current_url"] = current, ["requested_url"] = expectedUrl,
                ["message"] = "Authentication page detected. Log in in this browser session, verify the authenticated page, then retry capture. No export files were created."
            };
        }
        if (!string.IsNullOrWhiteSpace(expectedUrl) &&
            !string.Equals(current.TrimEnd('/'), expectedUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
            return new JsonObject { ["status"] = "error", ["code"] = "URL_MISMATCH",
                ["current_url"] = current, ["requested_url"] = expectedUrl,
                ["message"] = "Current browser URL does not match the requested page. No export files were created." };
        if (required && !matched)
            return new JsonObject {
                ["status"] = "authentication_unverified", ["code"] = "VERIFY_LOGIN",
                ["current_url"] = current, ["requested_url"] = expectedUrl,
                ["message"] = "No positive authenticated-session marker found. Finish login or provide the appropriate authenticated selector before retrying. No export files were created."
            };
        return new JsonObject { ["status"] = "ready", ["current_url"] = current,
            ["auth_verified"] = required ? matched : null };
    }
}
