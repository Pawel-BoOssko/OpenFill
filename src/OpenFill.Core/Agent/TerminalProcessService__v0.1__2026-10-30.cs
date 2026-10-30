// OpenFill SOURCE only: terminal utilities with bounded output returned to Luna, 2026-10-30
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace OpenFill.Core.Agent;

public static class TerminalProcessService
{
    public static async Task<JsonObject> ExecuteAsync(string shell, string command, string? cwd, string outputDirectory, int timeoutSeconds, CancellationToken ct = default)
    {
        shell = (shell ?? "powershell").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(command) || command.Length > 12000) throw new ArgumentException("Empty or overlong terminal command.");
        var executable = shell switch {
            "powershell" => "powershell.exe",
            "python" => "python.exe",
            "rg" => "rg.exe",
            "jq" => "jq.exe",
            _ => throw new ArgumentException("Unsupported shell. Use powershell/python/rg/jq.")
        };
        timeoutSeconds = Math.Clamp(timeoutSeconds, 1, 120);
        Directory.CreateDirectory(outputDirectory);
        var outFile = Path.Combine(outputDirectory, "terminal_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N")[..6] + ".txt");
        var errFile = outFile + ".stderr.txt";
        var psi = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = string.IsNullOrWhiteSpace(cwd) ? outputDirectory : Path.GetFullPath(cwd)
        };
        if (shell == "powershell") {
            psi.ArgumentList.Add("-NoProfile"); psi.ArgumentList.Add("-NonInteractive"); psi.ArgumentList.Add("-Command"); psi.ArgumentList.Add(command);
        } else if (shell == "python") {
            psi.ArgumentList.Add("-c"); psi.ArgumentList.Add(command);
        } else {
            // Native CLI arguments are handled as a complete command-line fragment.
            psi.Arguments = command;
        }
        var sw = Stopwatch.StartNew();
        using var proc = new Process { StartInfo = psi };
        try { if (!proc.Start()) throw new InvalidOperationException(executable + " did not start"); }
        catch (System.ComponentModel.Win32Exception ex) { throw new InvalidOperationException("Executable not found or refused: " + executable + ": " + ex.Message, ex); }
        var outTask = CopyStreamAsync(proc.StandardOutput, outFile);
        var errTask = CopyStreamAsync(proc.StandardError, errFile);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        bool timedOut = false;
        try { await proc.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) {
            timedOut = !ct.IsCancellationRequested;
            try { proc.Kill(entireProcessTree: true); } catch { }
            await proc.WaitForExitAsync(CancellationToken.None);
        }
        await Task.WhenAll(outTask, errTask);
        ct.ThrowIfCancellationRequested();
        sw.Stop();
        var stdout = await File.ReadAllTextAsync(outFile, ct);
        var stderr = await File.ReadAllTextAsync(errFile, ct);
        var result = new JsonObject {
            ["status"] = timedOut ? "timeout" : proc.ExitCode == 0 ? "ok" : "error",
            ["exit_code"] = proc.ExitCode, ["timeout"] = timedOut,
            ["stdout_path"] = outFile, ["stdout_bytes"] = new FileInfo(outFile).Length,
            ["stderr_path"] = errFile, ["stderr_bytes"] = new FileInfo(errFile).Length,
            ["stdout_preview"] = stdout.Length > 400 ? stdout[..400] : stdout,
            ["stderr_preview"] = stderr.Length > 400 ? stderr[..400] : stderr,
            ["elapsed_ms"] = sw.ElapsedMilliseconds
        };
        return result;
    }

    private static async Task CopyStreamAsync(StreamReader reader, string path)
    {
        await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 32768, useAsync:true);
        using var writer = new StreamWriter(output, new UTF8Encoding(false));
        var chars = new char[16384];
        int count;
        while ((count = await reader.ReadAsync(chars)) > 0) await writer.WriteAsync(chars.AsMemory(0, count));
        await writer.FlushAsync();
    }
}
