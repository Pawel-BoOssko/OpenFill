// Narrow production routing: one non-persistent search/filter field, one known URL.
// Never infer that a message/send/save/payment is a local draft operation.
using System.Text.RegularExpressions;
using OpenFill.Core.Agent;

namespace OpenFill.Core.Mcp;

public static class LocalPlanAutoRouting
{
    private static readonly Regex Urls = new(@"https?://[^\s<>()""']+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SafeIntent = new(
        @"(?i)\b(?:ustaw|ustawić|wpisz|wpisac|set|fill)\s+(?:\w+\s+){0,2}(?:filtr|filter|search|wyszukiwani[ae]|pole\s+wyszukiwania)\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex SideEffects = new(
        @"(?i)\b(?:wyślij|wyslij|wysłać|send|submit|zapisz|zapisać|save|publish|post|opublikuj|usuń|usun|delete|remove|kup|buy|purchase|pay|płatność|platnosc|transfer|prześlij|przeslij|upload|invite|zaproszenie|wiadomość|wiadomosc|message|e-?mail|login|loguj|zaloguj|hasło|haslo|password|kod|code|konto|account)\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool TrySelect(string? task, string? continuation, string? selectedMode,
        string? explicitStartUrl, out string startUrl)
    {
        startUrl = "";
        if (!string.IsNullOrWhiteSpace(continuation) ||
            !string.IsNullOrWhiteSpace(selectedMode) ||
            !string.IsNullOrWhiteSpace(explicitStartUrl) ||
            string.IsNullOrWhiteSpace(task) || task.Length > 500 ||
            task.Contains('\n') || task.Contains(';') ||
            !SafeIntent.IsMatch(task) || SideEffects.IsMatch(task))
            return false;
        var urls = Urls.Matches(task);
        if (urls.Count != 1) return false;
        var candidate = urls[0].Value.TrimEnd('.', ',', '!', ')', ']');
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment) ||
            CaptureRecipeScopeGate.IsSensitiveHost(uri.Host))
            return false;
        startUrl = uri.AbsoluteUri;
        return true;
    }
}
