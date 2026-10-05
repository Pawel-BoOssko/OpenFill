// OpenFill - Metadata: wersja 0.4, data 2026-10-05 15:15
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace OpenFill.Core.Logging;

/// <summary>One NDJSON log record. The same record goes to the file and to the panel.</summary>
public sealed record LogEvent(
    long Seq,
    DateTime TsUtc,
    string? RunId,
    string Source,
    string EventType,
    string Status,
    string Message,
    JsonNode? Data);

/// <summary>
/// Event log: NDJSON output (global and per run), secret redaction, broadcast to the panel.
/// The panel and the log file cannot drift apart, because both read the same stream.
/// </summary>
public sealed class EventLog : IDisposable
{
    public static readonly JsonSerializerOptions Ndjson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly object _lock = new();
    private readonly string _logsDir;
    private readonly string _runsDir;
    private StreamWriter? _appWriter;
    private StreamWriter? _runWriter;
    private StreamWriter? _networkWriter;
    private StreamWriter? _usageWriter;
    private long _seq;

    public string? CurrentRunId { get; private set; }
    public string? CurrentRunFile { get; private set; }
    public string? CurrentNetworkFile { get; private set; }

    public event Action<LogEvent>? Published;

    public EventLog(string logsDir, string runsDir)
    {
        _logsDir = logsDir;
        _runsDir = runsDir;
        Directory.CreateDirectory(_logsDir);
        Directory.CreateDirectory(_runsDir);
        var appFile = Path.Combine(_logsDir, $"app_{DateTime.UtcNow:yyyyMMdd}.ndjson");
        _appWriter = Open(appFile);
    }

    private static StreamWriter Open(string path) =>
        new(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite), new UTF8Encoding(false)) { AutoFlush = true };

    public string BeginRun(string? runId = null)
    {
        lock (_lock)
        {
            EndRunLocked();
            CurrentRunId = runId ?? $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..6]}";
            CurrentRunFile = Path.Combine(_runsDir, $"run_{CurrentRunId}.ndjson");
            CurrentNetworkFile = Path.Combine(_runsDir, $"run_{CurrentRunId}_network.ndjson");
            _runWriter = Open(CurrentRunFile);
            _networkWriter = Open(CurrentNetworkFile);
            return CurrentRunId;
        }
    }

    public void EndRun()
    {
        lock (_lock) EndRunLocked();
    }

    private void EndRunLocked()
    {
        _runWriter?.Dispose();
        _networkWriter?.Dispose();
        _runWriter = null;
        _networkWriter = null;
        CurrentRunId = null;
    }

    /// <summary>Writes an event. Data is redacted before being written and before being sent to the panel.</summary>
    public LogEvent Write(string source, string eventType, string message, object? data = null, string status = "ok")
    {
        JsonNode? node = data switch
        {
            null => null,
            JsonNode n => n,
            _ => JsonSerializer.SerializeToNode(data, Ndjson)
        };
        LogEvent ev;
        lock (_lock)
        {
            ev = new LogEvent(++_seq, DateTime.UtcNow, CurrentRunId, source, eventType, status,
                Redactor.Text(message), Redactor.Node(node));
            var line = JsonSerializer.Serialize(ev, Ndjson);
            try
            {
                _appWriter?.WriteLine(line);
                _runWriter?.WriteLine(line);
            }
            catch
            {
                // A log write failure must not stop the work.
            }
        }
        try { Published?.Invoke(ev); } catch { /* a subscriber must not stop logging */ }
        return ev;
    }

    /// <summary>Raw network traffic (after redaction) to a separate file, without sending to the panel.</summary>
    public void WriteNetwork(object record)
    {
        lock (_lock)
        {
            if (_networkWriter is null) return;
            try
            {
                var node = Redactor.Node(JsonSerializer.SerializeToNode(record, Ndjson));
                _networkWriter.WriteLine(node?.ToJsonString(Ndjson));
            }
            catch
            {
                // same as above.
            }
        }
    }

    /// <summary>
    /// One line per model call (sizes, tokens, latency, model, task preview) in a monthly usage_YYYYMM.ndjson file.
    /// No prompts or page content are stored here - only numbers, so the file is safe to keep and share.
    /// </summary>
    public void WriteUsage(object record)
    {
        lock (_lock)
        {
            try
            {
                _usageWriter ??= Open(Path.Combine(_logsDir, $"usage_{DateTime.UtcNow:yyyyMM}.ndjson"));
                var node = Redactor.Node(JsonSerializer.SerializeToNode(record, Ndjson));
                _usageWriter.WriteLine(node?.ToJsonString(Ndjson));
            }
            catch
            {
                // Usage logging must never stop the work.
            }
        }
    }

    public void Dispose()
    {
        _usageWriter?.Dispose();
        _usageWriter = null;
        lock (_lock)
        {
            EndRunLocked();
            _appWriter?.Dispose();
            _appWriter = null;
        }
    }
}
