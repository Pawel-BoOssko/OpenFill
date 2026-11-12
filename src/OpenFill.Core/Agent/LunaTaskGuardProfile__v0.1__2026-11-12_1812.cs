using System.Text.RegularExpressions;

namespace OpenFill.Core.Agent;

/// <summary>
/// Narrow tool profile for a single, explicit ChatGPT conversation send.
/// A message referencing other projects is payload data, not permission to investigate them.
/// </summary>
public static class LunaTaskGuardProfile
{
    private static readonly HashSet<string> OneShotTools = new(StringComparer.Ordinal)
    {
        "navigate", "read_view", "get_page", "read_text", "view_digest",
        "inspect_view", "observe_query", "screenshot", "wait", "act",
        "act_many", "act_locator", "verify_view", "get_network",
        "finish", "ask_user"
    };

    public static bool IsAllowedOneShotTool(string name) => OneShotTools.Contains(name);

    public static Tool[] SelectOneShotTools(IReadOnlyList<Tool> tools) =>
        tools.Where(t => OneShotTools.Contains(t.Name)).ToArray();

    public static bool IsOneShotChatGptSend(string? task)
    {
        if (string.IsNullOrWhiteSpace(task) || task.Length > 12000) return false;
        try
        {
            if (!Regex.IsMatch(task, @"\b(wyślij|wyslij|send)\b",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50)))
                return false;
            if (!Regex.IsMatch(task,
                    @"\b(dokładnie\s+jeden\s+raz|dokladnie\s+jeden\s+raz|exactly\s+once|jeden\s+raz)\b",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50)))
                return false;

            var matches = Regex.Matches(task, @"https?://[^\s<>'""\]\)]+",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
            if (matches.Count != 1 ||
                !Uri.TryCreate(matches[0].Value.TrimEnd('.', ',', ';'), UriKind.Absolute, out var url))
                return false;
            return url.Scheme == "https" &&
                   url.Host.Equals("chatgpt.com", StringComparison.OrdinalIgnoreCase) &&
                   url.AbsolutePath.StartsWith("/c/", StringComparison.OrdinalIgnoreCase) &&
                   url.AbsolutePath.Length > 3;
        }
        catch (RegexMatchTimeoutException) { return false; }
    }

    /// <summary>Moderate task defaults. Explicit MCP limits always override these.</summary>
    public static (double CostUsd, int DurationSeconds, int IdleSeconds) Defaults(string task) =>
        IsOneShotChatGptSend(task)
            ? (0.04, 180, 90)
            : (0.15, 900, 150);
}
