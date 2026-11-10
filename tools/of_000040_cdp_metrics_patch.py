from pathlib import Path
from datetime import datetime
import shutil
import re

root = Path(r"C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill")
changes = [
    (
        root / "src/OpenFill.Core/Cdp/CdpSession__v0.2__2026-10-05_1515.cs",
        [
            (
                '''    public Task<JsonNode?> SendAsync(string method, JsonObject? @params = null, CancellationToken ct = default)
        => Connection.SendAsync(method, @params, SessionId, ct);''',
                '''    public Task<JsonNode?> SendAsync(string method, JsonObject? @params = null, CancellationToken ct = default)
    {
        // E03: count actual CDP read-like requests only inside an explicit read-tool scope.
        CdpReadCallScope.Track(method);
        return Connection.SendAsync(method, @params, SessionId, ct);
    }'''
            ),
            (
                '''public sealed class CdpSession(ICdpConnection conn)''',
                '''// E03: per-async-flow telemetry, no global counters and no page content in logs.
// These are CDP requests made through CdpSession during read tools, not a
// claim about unique DOM snapshots or calls made directly through ICdpConnection.
public sealed class CdpReadCallScope : IDisposable
{
    private static readonly AsyncLocal<CdpReadCallScope?> Current = new();
    private readonly CdpReadCallScope? _previous;
    private int _calls;

    public int Calls => Volatile.Read(ref _calls);

    public CdpReadCallScope()
    {
        _previous = Current.Value;
        Current.Value = this;
    }

    internal static void Track(string method)
    {
        // Runtime.evaluate is the primary physical read path. DOM methods
        // below are explicit read operations. Mutating CDP methods are excluded.
        if (method is not ("Runtime.evaluate" or "Runtime.callFunctionOn"
            or "DOM.getDocument" or "DOM.querySelector" or "DOM.querySelectorAll"
            or "DOM.getOuterHTML" or "DOM.describeNode")) return;
        if (Current.Value is { } scope) Interlocked.Increment(ref scope._calls);
    }

    public void Dispose() => Current.Value = _previous;
}

public sealed class CdpSession(ICdpConnection conn)'''
            )
        ]
    ),
    (
        root / "src/OpenFill.Core/Agent/AgentRunner__v0.29__2026-10-29_2314.cs",
        [
            (
                '''using OpenFill.Core.Browser;''',
                '''using OpenFill.Core.Browser;
using OpenFill.Core.Cdp;'''
            ),
            (
                '''    public int RepeatedPageReadRequests { get; private set; }''',
                '''    public int RepeatedPageReadRequests { get; private set; }
    // Real CDP evaluation / read requests from CdpSession while a read tool runs.
    // Does not include initial context prefetch or direct ICdpConnection calls.
    public int CdpReadCalls { get; private set; }'''
            ),
            (
                '''                var toolWatch = Stopwatch.StartNew();
                ToolResult result;''',
                '''                var toolWatch = Stopwatch.StartNew();
                var cdpReadTool = PageReadTools.Contains(name) &&
                    (name != "read_view" || string.IsNullOrWhiteSpace(argObj["capture_id"]?.ToString()));
                using var cdpReadScope = cdpReadTool ? new CdpReadCallScope() : null;
                ToolResult result;'''
            ),
            (
                '''                ToolElapsedMs += toolWatch.ElapsedMilliseconds;''',
                '''                ToolElapsedMs += toolWatch.ElapsedMilliseconds;
                var cdpReadCalls = cdpReadScope?.Calls ?? 0;
                CdpReadCalls += cdpReadCalls;'''
            ),
            (
                '''                        repeatedPageReadRequest = isRepeatedPageRead,
                        outputBytes = Encoding.UTF8.GetByteCount(result.Output),''',
                '''                        repeatedPageReadRequest = isRepeatedPageRead,
                        cdpReadCalls,
                        outputBytes = Encoding.UTF8.GetByteCount(result.Output),'''
            )
        ]
    ),
    (
        root / "src/OpenFill.Core/Hosting/TaskRun__v0.1__2026-10-07_1732.cs",
        [
            (
                '''                    repeatedPageReadRequests = runner.RepeatedPageReadRequests,''',
                '''                    repeatedPageReadRequests = runner.RepeatedPageReadRequests,
                    cdpReadCalls = runner.CdpReadCalls,'''
            ),
            (
                '''                        repeatedPageReadRequests = runner.RepeatedPageReadRequests,''',
                '''                        repeatedPageReadRequests = runner.RepeatedPageReadRequests,
                        cdpReadCalls = runner.CdpReadCalls,'''
            )
        ]
    )
]
# Guard against replacement overlap in TaskRun (longer needle matches a substring).
for path, reps in changes:
    raw = path.read_bytes()
    bom = raw.startswith(b"\xef\xbb\xbf")
    newline = "\r\n" if b"\r\n" in raw else "\n"
    content = raw.decode("utf-8-sig").replace("\r\n", "\n")
    original = content
    if path.name.startswith("CdpSession") and "CdpReadCallScope.Track(method)" in content:
        print("ALREADY_PATCHED", path.name)
        continue
    for idx, (old, new) in enumerate(reps):
        anchored = r"(?m)^" + re.escape(old) + r"$"
        count = len(re.findall(anchored, content))
        if count != 1:
            raise RuntimeError(f"PATCH_MISMATCH {path.name} change={idx} matches={count}")
        content = re.sub(anchored, lambda _: new, content, count=1)
    backup = path.with_suffix(path.suffix + ".before_of000040")
    shutil.copy2(path, backup)
    path.write_bytes((b"\xef\xbb\xbf" if bom else b"") + content.replace("\n", newline).encode("utf-8"))
    print("PATCHED", path.name, "backup", backup.name)
print("OF000040_SOURCE_PATCH_OK")
