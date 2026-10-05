// OpenFill - Metadata: wersja 0.1, data 2026-10-05 14:28
using System.Text;
using System.Text.Json;
using OpenFill.Core.Logging;

namespace OpenFill.Core.Hosting;

/// <summary>One task in the history list (panel task or MCP task).</summary>
public sealed record HistoryRecord(
    string Id,
    string Source,          // "panel" or "mcp"
    string Task,
    DateTime StartedUtc,
    DateTime? EndedUtc,
    string Status,          // running, success, partial, failed, stopped
    string? Summary,
    string? RunId);         // links to logs\runs\run_{RunId}.ndjson

/// <summary>
/// Task history: an append-only NDJSON file. A task is written when it starts (status running) and again when
/// it ends; for each Id the last line wins. Task text and summary are redacted before they get here.
/// </summary>
public sealed class HistoryStore
{
    private readonly string _file;
    private readonly object _lock = new();

    public HistoryStore(string file)
    {
        _file = file;
        try { Directory.CreateDirectory(Path.GetDirectoryName(file)!); } catch { }
    }

    public void Append(HistoryRecord record)
    {
        lock (_lock)
        {
            try { File.AppendAllText(_file, JsonSerializer.Serialize(record, EventLog.Ndjson) + "\n", new UTF8Encoding(false)); }
            catch { /* history must never stop the work */ }
        }
    }

    /// <summary>The newest tasks first. A record that is still "running" but is not the current task becomes "interrupted".</summary>
    public List<HistoryRecord> Load(int max, string? currentId)
    {
        var order = new List<string>();
        var byId = new Dictionary<string, HistoryRecord>();
        lock (_lock)
        {
            if (!File.Exists(_file)) return new List<HistoryRecord>();
            string[] lines;
            try
            {
                using var fs = new FileStream(_file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs, Encoding.UTF8);
                lines = sr.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            }
            catch { return new List<HistoryRecord>(); }
            foreach (var line in lines)
            {
                HistoryRecord? r;
                try { r = JsonSerializer.Deserialize<HistoryRecord>(line, EventLog.Ndjson); } catch { continue; }
                if (r is null || string.IsNullOrEmpty(r.Id)) continue;
                if (!byId.ContainsKey(r.Id)) order.Add(r.Id);
                byId[r.Id] = r;
            }
        }
        var list = new List<HistoryRecord>();
        for (var i = order.Count - 1; i >= 0 && list.Count < max; i--)
        {
            var r = byId[order[i]];
            if (r.Status == "running" && r.Id != currentId) r = r with { Status = "interrupted" };
            list.Add(r);
        }
        return list;
    }

    /// <summary>Events of one task (without their data), read from its run file. Only plain run ids are accepted.</summary>
    public static (List<object> Events, bool Truncated) LoadEvents(string runsDir, string runId, int max)
    {
        var events = new List<object>();
        if (string.IsNullOrEmpty(runId) || !runId.All(c => char.IsLetterOrDigit(c) || c == '_')) return (events, false);
        var file = Path.Combine(runsDir, $"run_{runId}.ndjson");
        if (!File.Exists(file)) return (events, false);
        var truncated = false;
        try
        {
            using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = new StreamReader(fs, Encoding.UTF8);
            string? line;
            while ((line = sr.ReadLine()) is not null)
            {
                if (line.Length == 0) continue;
                if (events.Count >= max) { truncated = true; break; }
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    string S(string n) => root.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
                    events.Add(new { tsUtc = S("tsUtc"), source = S("source"), eventType = S("eventType"), status = S("status"), message = S("message") });
                }
                catch { /* skip a broken line */ }
            }
        }
        catch { }
        return (events, truncated);
    }
}
