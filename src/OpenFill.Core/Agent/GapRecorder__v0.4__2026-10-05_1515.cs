// OpenFill - Metadata: wersja 0.4, data 2026-10-05 15:15
using OpenFill.Core.Logging;

namespace OpenFill.Core.Agent;

/// <summary>Records gap reports (report_gap) to a separate NDJSON file and to the log/panel.</summary>
public sealed class GapRecorder(string gapsFile, EventLog log)
{
    private readonly object _lock = new();

    public void Record(string needed, string missing, string? workaround)
    {
        var rec = new { tsUtc = DateTime.UtcNow.ToString("O"), runId = log.CurrentRunId, needed, missing, workaround };
        lock (_lock)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(gapsFile)!);
                File.AppendAllText(gapsFile, System.Text.Json.JsonSerializer.Serialize(rec, EventLog.Ndjson) + "\n");
            }
            catch { /* a failed write does not stop the work */ }
        }
        log.Write("gap", "report", $"Gap: {needed} — missing: {missing}", new { needed, missing, workaround }, status: "warn");
    }
}
