// OpenFill - Metadata: wersja 0.2, data 2026-10-05 12:00
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace OpenFill.Core.Logging;

/// <summary>
/// Removes secrets (tokens, cookies, passwords, API keys) from text and JSON data
/// before they reach the log, the panel or the model context.
/// </summary>
public static partial class Redactor
{
    public const string Mask = "[REDACTED]";

    private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "passwd", "pwd", "pass", "secret", "client_secret", "token", "access_token", "refresh_token",
        "id_token", "auth", "authorization", "proxy-authorization", "cookie", "set-cookie", "api_key", "apikey",
        "x-api-key", "session", "sessionid", "session_id", "sid", "csrf", "csrf_token", "xsrf", "x-csrf-token",
        "x-xsrf-token", "otp", "pin", "cvv", "cvc", "card_number", "cardnumber", "iban"
    };

    [GeneratedRegex(@"eyJ[A-Za-z0-9_-]{6,}\.[A-Za-z0-9_-]{6,}\.[A-Za-z0-9_-]{4,}", RegexOptions.CultureInvariant)]
    private static partial Regex JwtRegex();

    [GeneratedRegex(@"(?i)\b(bearer|basic)\s+[A-Za-z0-9._~+/=-]{8,}", RegexOptions.CultureInvariant)]
    private static partial Regex AuthSchemeRegex();

    [GeneratedRegex(@"\bsk-[A-Za-z0-9_-]{16,}", RegexOptions.CultureInvariant)]
    private static partial Regex ApiKeyRegex();

    [GeneratedRegex(@"(?i)([?&;](?:access_token|refresh_token|id_token|token|auth|session|sessionid|sid|key|apikey|api_key|password|passwd|secret|signature|sig|code|csrf|xsrf)=)[^&#\s""']+", RegexOptions.CultureInvariant)]
    private static partial Regex QueryParamRegex();

    [GeneratedRegex(@"(?i)(""(?:password|passwd|pwd|secret|token|access_token|refresh_token|id_token|api_key|apikey|authorization|cookie|csrf_token|cvv|cvc|card_number)""\s*:\s*"")[^""]*("")", RegexOptions.CultureInvariant)]
    private static partial Regex JsonFieldRegex();

    public static bool IsSensitiveKey(string key) => SensitiveKeys.Contains(key.Trim());

    public static string Text(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? "";
        var s = JwtRegex().Replace(text, Mask);
        s = AuthSchemeRegex().Replace(s, m => m.Groups[1].Value + " " + Mask);
        s = ApiKeyRegex().Replace(s, Mask);
        s = QueryParamRegex().Replace(s, m => m.Groups[1].Value + Mask);
        s = JsonFieldRegex().Replace(s, m => m.Groups[1].Value + Mask + m.Groups[2].Value);
        return s;
    }

    /// <summary>Copy of the JSON tree with sensitive keys and texts masked.</summary>
    public static JsonNode? Node(JsonNode? node)
    {
        switch (node)
        {
            case null:
                return null;
            case JsonObject obj:
            {
                var copy = new JsonObject();
                foreach (var (k, v) in obj)
                    copy[k] = IsSensitiveKey(k) && v is not null ? JsonValue.Create(Mask) : Node(v);
                return copy;
            }
            case JsonArray arr:
            {
                var copy = new JsonArray();
                foreach (var v in arr) copy.Add(Node(v));
                return copy;
            }
            case JsonValue val:
                return val.TryGetValue<string>(out var s) ? JsonValue.Create(Text(s)) : val.DeepClone();
            default:
                return node.DeepClone();
        }
    }

    /// <summary>HTTP headers without cookies and authorization.</summary>
    public static Dictionary<string, string> Headers(IEnumerable<KeyValuePair<string, string>> headers)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in headers)
            d[k] = IsSensitiveKey(k) ? Mask : Text(v);
        return d;
    }
}
