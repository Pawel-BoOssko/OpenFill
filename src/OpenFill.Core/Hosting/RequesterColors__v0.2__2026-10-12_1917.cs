// OpenFill - Metadata: wersja 0.2, data 2026-10-12 19:17
namespace OpenFill.Core.Hosting;

/// <summary>
/// Colour of the chat (model) that handed OpenFill a task: frames on cards and events, browser tabs, history.
/// The rule is the same as colorOf() in panel.html: known names have fixed colours, any other name gets a stable colour from its text.
/// </summary>
public static class RequesterColors
{
    private static readonly string[] Palette = { "#e07b1f", "#1f8fd6", "#16a5a5", "#b5483a", "#a0a020", "#8a5ad6" };

    /// <summary>"#rrggbb" for the client part of a requester ("Client · Chat"), or null when there is no name.</summary>
    public static string? For(string? name)
    {
        var n = Split(name).Client.ToLowerInvariant();
        if (n.Length == 0) return null;
        if (n.Contains("gpt") || n.Contains("codex") || n.Contains("openai")) return "#2e9e5b";
        if (n.Contains("pap")) return "#e0559a";
        if (n.Contains("bishop")) return "#7a5bd6";
        uint h = 2166136261;
        foreach (var c in n) { h ^= c; h = unchecked(h * 16777619); }
        return Palette[h % (uint)Palette.Length];
    }

    private static readonly string[] ChatPalette = { "#f2c200", "#14b8a6", "#ef4444", "#3b82f6", "#84cc16", "#f97316", "#a855f7", "#111827" };

    /// <summary>"#rrggbb" for the chat part of a requester ("Client · Chat"), or null when there is none.</summary>
    public static string? ForChat(string? name)
    {
        var n = Split(name).Chat.ToLowerInvariant();
        if (n.Length == 0) return null;
        uint h = 2166136261;
        foreach (var c in n) { h ^= c; h = unchecked(h * 16777619); }
        return ChatPalette[h % (uint)ChatPalette.Length];
    }

    private static (string Client, string Chat) Split(string? name)
    {
        var s = (name ?? "").Trim();
        var i = s.IndexOf(" · ");
        return i < 0 ? (s, "") : (s[..i].Trim(), s[(i + 3)..].Trim());
    }
}