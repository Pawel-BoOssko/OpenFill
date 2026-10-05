// OpenFill - Metadata: wersja 0.2, data 2026-10-05 12:00
using System.Text;

namespace OpenFill.Core.Logging;

/// <summary>
/// Limits a tool result to a character limit. The full result goes to a file,
/// and the model gets the beginning, a note that it was truncated, and the path to the file.
/// </summary>
public sealed class OutputLimiter
{
    private readonly string _overflowDir;
    private readonly int _limit;

    public OutputLimiter(string overflowDir, int limit)
    {
        _overflowDir = overflowDir;
        _limit = Math.Max(1000, limit);
    }

    public (string Text, string? OverflowFile) Apply(string text, string label)
    {
        if (text.Length <= _limit) return (text, null);
        Directory.CreateDirectory(_overflowDir);
        var safe = new string(label.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        var file = Path.Combine(_overflowDir, $"{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{safe}.txt");
        File.WriteAllText(file, text, new UTF8Encoding(false));
        var keep = _limit - 400;
        var sb = new StringBuilder(_limit);
        sb.Append(text, 0, keep);
        sb.Append($"\n[...] truncated [...] {text.Length - keep} more chars. Full output saved to: {file}\n");
        sb.Append("Narrow the request (scope, filters, limit) to see the rest.");
        return (sb.ToString(), file);
    }

    public static string Head(string? text, int max)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Length <= max ? text : text[..max] + $"… (+{text.Length - max})";
    }
}
