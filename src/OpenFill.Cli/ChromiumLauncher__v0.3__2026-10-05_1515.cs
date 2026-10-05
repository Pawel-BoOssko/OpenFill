// OpenFill - Metadata: wersja 0.3, data 2026-10-05 15:15
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json.Nodes;

namespace OpenFill.Cli;

/// <summary>Starts Chromium with an open debugging port and returns the WebSocket address for CDP.</summary>
public sealed class ChromiumLauncher : IDisposable
{
    private Process? _proc;
    public int Port { get; private set; }
    private readonly Queue<string> _tail = new();

    /// <summary>Last lines of Chromium output (diagnostics).</summary>
    public string Tail()
    {
        var exit = _proc is { HasExited: true } ? $"[proces zakonczony, kod {_proc.ExitCode}]\n" : "";
        lock (_tail) return exit + string.Join('\n', _tail);
    }

    private void Remember(string? line)
    {
        if (string.IsNullOrEmpty(line)) return;
        lock (_tail) { _tail.Enqueue(line); while (_tail.Count > 60) _tail.Dequeue(); }
    }

    private static string? FindChromium()
    {
        var candidates = new List<string>();
        var env = Environment.GetEnvironmentVariable("OPENFILL_CHROMIUM");
        if (!string.IsNullOrEmpty(env)) candidates.Add(env);
        var pw = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH") ?? "/opt/pw-browsers";
        candidates.Add(Path.Combine(pw, "chromium", "chrome-linux", "chrome"));
        foreach (var d in Directory.Exists(pw) ? Directory.GetDirectories(pw, "chromium-*") : Array.Empty<string>())
            candidates.Add(Path.Combine(d, "chrome-linux", "chrome"));
        candidates.AddRange(new[] { "/usr/bin/chromium", "/usr/bin/chromium-browser", "/usr/bin/google-chrome" });
        if (OperatingSystem.IsWindows())
        {
            // On Windows: Edge is always there, Chrome often. The same CDP protocol.
            foreach (var root in new[] { Environment.GetEnvironmentVariable("ProgramFiles(x86)"), Environment.GetEnvironmentVariable("ProgramFiles"), Environment.GetEnvironmentVariable("LOCALAPPDATA") })
            {
                if (string.IsNullOrEmpty(root)) continue;
                candidates.Add(Path.Combine(root, "Microsoft", "Edge", "Application", "msedge.exe"));
                candidates.Add(Path.Combine(root, "Google", "Chrome", "Application", "chrome.exe"));
            }
        }
        if (OperatingSystem.IsMacOS())
            candidates.Add("/Applications/Google Chrome.app/Contents/MacOS/Google Chrome");
        return candidates.FirstOrDefault(File.Exists);
    }

    public async Task<string> LaunchAsync(string userDataDir, bool headless, CancellationToken ct = default)
    {
        var exe = FindChromium() ?? throw new FileNotFoundException("Chromium not found. Set OPENFILL_CHROMIUM.");
        Port = GetFreePort();
        Directory.CreateDirectory(userDataDir);
        var args = new List<string>
        {
            $"--remote-debugging-port={Port}",
            $"--user-data-dir={userDataDir}",
            "--no-first-run", "--no-default-browser-check", "--disable-background-networking",
            "--disable-features=Translate,AcceptCHFrame", "--remote-allow-origins=*",
            "--window-size=1280,900"
        };
        if (headless) args.Add("--headless=new");
        // Container environments:
        if (OperatingSystem.IsLinux()) { args.Add("--no-sandbox"); args.Add("--disable-dev-shm-usage"); }

        var psi = new ProcessStartInfo(exe) { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        _proc = Process.Start(psi) ?? throw new InvalidOperationException("Could not start Chromium.");
        // Chromium output MUST be read continuously: a clogged pipe (64 KB) blocks the process
        // and breaks the CDP connection. We keep only the last lines for diagnostics.
        _proc.OutputDataReceived += (_, e) => Remember(e.Data);
        _proc.ErrorDataReceived += (_, e) => Remember(e.Data);
        _proc.BeginOutputReadLine();
        _proc.BeginErrorReadLine();

        return await WaitForWebSocketAsync(ct);
    }

    private async Task<string> WaitForWebSocketAsync(CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var json = await http.GetStringAsync($"http://127.0.0.1:{Port}/json/version", ct);
                var obj = JsonNode.Parse(json) as JsonObject;
                var ws = obj?["webSocketDebuggerUrl"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(ws)) return ws!;
            }
            catch { await Task.Delay(250, ct); }
        }
        throw new TimeoutException("Chromium did not expose a debugging port.");
    }

    private static int GetFreePort()
    {
        var l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        l.Start();
        var port = ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    public void Dispose()
    {
        try { if (_proc is { HasExited: false }) _proc.Kill(entireProcessTree: true); } catch { }
        _proc?.Dispose();
    }
}
