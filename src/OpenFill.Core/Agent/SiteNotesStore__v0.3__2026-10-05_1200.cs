// OpenFill - Metadata: wersja 0.3, data 2026-10-05 12:00
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenFill.Core.Logging;

namespace OpenFill.Core.Agent;

/// <summary>
/// Site notes. Each site "learns" once, then the note speeds up subsequent visits.
///
/// Safety rules (important, because the store is eventually meant to be shared):
///  - a note is DATA, not an instruction - the model should treat it as a hint and always verify it live;
///  - no personal data, tokens or form values go into a note (only knowledge about the site);
///  - a note carries a date and success/failure counters, and is marked stale after a failure.
/// </summary>
public sealed class SiteNote
{
    public string Host { get; set; } = "";
    public string Version { get; set; } = "0.1";
    public string UpdatedUtc { get; set; } = DateTime.UtcNow.ToString("O");
    public int Successes { get; set; }
    public int Failures { get; set; }
    public bool Stale { get; set; }
    /// <summary>Knowledge about a site written by the model: how the form works, which network calls matter, pitfalls.</summary>
    public string Knowledge { get; set; } = "";
}

public sealed class SiteNotesStore(string dir, EventLog log)
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private string FileFor(string host)
    {
        var safe = new string(host.Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' ? c : '_').ToArray());
        return Path.Combine(dir, safe + ".json");
    }

    public SiteNote? Get(string host)
    {
        try
        {
            var f = FileFor(host);
            if (!File.Exists(f)) return null;
            return JsonSerializer.Deserialize<SiteNote>(File.ReadAllText(f), Json);
        }
        catch { return null; }
    }

    /// <summary>Removes potential secrets and personal data from the note text before it reaches disk / the shared store.</summary>
    public static string Scrub(string knowledge) => Redactor.Text(knowledge);

    public void Save(SiteNote note)
    {
        Directory.CreateDirectory(dir);
        note.Knowledge = Scrub(note.Knowledge);
        note.UpdatedUtc = DateTime.UtcNow.ToString("O");
        File.WriteAllText(FileFor(note.Host), JsonSerializer.Serialize(note, Json));
        log.Write("notes", "save", $"Saved note for {note.Host}", new { note.Host, note.Successes, note.Failures, note.Stale });
    }

    public void RecordOutcome(string host, bool success, string? knowledge = null)
    {
        var note = Get(host) ?? new SiteNote { Host = host };
        if (success) { note.Successes++; note.Stale = false; }
        else { note.Failures++; note.Stale = true; }
        if (!string.IsNullOrWhiteSpace(knowledge)) note.Knowledge = knowledge!;
        Save(note);
    }
}
