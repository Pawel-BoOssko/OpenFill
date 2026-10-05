// OpenFill - Metadata: wersja 0.1, data 2026-10-05 13:48
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace OpenFill.Core.Mcp;

/// <summary>
/// Quick Cloudflare tunnel (no account): gives the local MCP server a temporary public HTTPS address.
/// The tunnel only makes an outbound connection, so no router settings or open ports are needed.
/// The address changes every time the tunnel starts.
/// </summary>
public sealed class CloudflareTunnel : IAsyncDisposable
{
    private static readonly Regex UrlRx = new(@"https://[a-z0-9-]+\.trycloudflare\.com", RegexOptions.Compiled);

    private Process? _proc;

    public string Url { get; private set; } = "";

    public static async Task<CloudflareTunnel> StartAsync(int localPort, string binDir, Action<string>? log, CancellationToken ct)
    {
        var exe = await EnsureBinaryAsync(binDir, log, ct);
        var t = new CloudflareTunnel();
        var psi = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        // The Host header must match what the local listener expects, otherwise it answers 400.
        foreach (var a in new[] { "tunnel", "--url", $"http://127.0.0.1:{localPort}", "--http-host-header", $"127.0.0.1:{localPort}", "--no-autoupdate" })
            psi.ArgumentList.Add(a);
        var proc = Process.Start(psi) ?? throw new InvalidOperationException("cloudflared did not start.");
        t._proc = proc;

        var found = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tail = new Queue<string>();
        void OnLine(string? line)
        {
            if (line is null) return;
            lock (tail) { tail.Enqueue(line); while (tail.Count > 12) tail.Dequeue(); }
            var m = UrlRx.Match(line);
            if (m.Success) found.TrySetResult(m.Value);
        }
        proc.OutputDataReceived += (_, e) => OnLine(e.Data);
        proc.ErrorDataReceived += (_, e) => OnLine(e.Data);
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));
        using var reg = timeout.Token.Register(() => found.TrySetCanceled());
        try { t.Url = await found.Task; }
        catch (OperationCanceledException)
        {
            string last; lock (tail) last = string.Join(" | ", tail);
            await t.DisposeAsync();
            throw new InvalidOperationException("cloudflared gave no address within 45 s. Last output: " + last);
        }
        log?.Invoke("Tunnel is up.");
        return t;
    }

    private static async Task<string> EnsureBinaryAsync(string binDir, Action<string>? log, CancellationToken ct)
    {
        Directory.CreateDirectory(binDir);
        string asset;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) asset = "cloudflared-windows-amd64.exe";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            asset = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "cloudflared-linux-arm64" : "cloudflared-linux-amd64";
        else throw new PlatformNotSupportedException("Install cloudflared yourself on this system and set mcpPublicBaseUrl in config.json.");

        var target = Path.Combine(binDir, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cloudflared.exe" : "cloudflared");
        if (File.Exists(target)) return target;

        log?.Invoke("Downloading cloudflared (one time)...");
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        var url = "https://github.com/cloudflare/cloudflared/releases/latest/download/" + asset;
        var tmp = target + ".download";
        await using (var src = await http.GetStreamAsync(url, ct))
        await using (var dst = File.Create(tmp))
            await src.CopyToAsync(dst, ct);
        File.Move(tmp, target, overwrite: true);
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            File.SetUnixFileMode(target, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return target;
    }

    public ValueTask DisposeAsync()
    {
        try { if (_proc is { HasExited: false }) _proc.Kill(entireProcessTree: true); } catch { }
        _proc?.Dispose();
        _proc = null;
        return ValueTask.CompletedTask;
    }
}
