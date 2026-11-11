// SOURCE only: file-first deterministic CLI filter. OF-IMP-20261031 A02/A03.
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace OpenFill.Core.Agent;

public static class FileFilterService
{
    // The program never reparses HTML, JSON or regex internally: rg and jq own that work.
    // The file manifest is short; full stdout and stderr remain on disk.
    internal static string? Locate(string tool)
    {
        var file = tool switch
        {
            "rg" => "rg.exe", "jq" => "jq.exe", "sqlite3" => "sqlite3.exe",
            "grep" => "grep.exe", "sed" => "sed.exe", "awk" => "awk.exe",
            _ => null
        };
        if (file is null) return null;
        var user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var roots = new[]
        {
            Environment.GetEnvironmentVariable("OPENFILL_TOOLS_DIR"),
            Path.Combine(user, "OpenFill", "tools", "current"),
            Path.Combine(user, "OpenFill", "tools"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Links"),
            Path.Combine(AppContext.BaseDirectory, "tools"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "usr", "bin")
        };
        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root)) continue;
            var path = Path.Combine(root, file);
            if (File.Exists(path)) return path;
        }
        var pathEntries = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator);
        foreach (var entry in pathEntries)
        {
            if (string.IsNullOrWhiteSpace(entry)) continue;
            try
            {
                var path = Path.Combine(entry, file);
                if (File.Exists(path)) return path;
            }
            catch { }
        }
        return null;
    }

    public static JsonObject Availability()
    {
        var status = new JsonObject();
        foreach (var name in new[] { "rg", "jq", "sqlite3", "grep", "sed", "awk" })
        {
            var executable = Locate(name);
            status[name] = new JsonObject
            {
                ["available"] = executable is not null,
                ["path"] = executable
            };
        }
        return status;
    }

    /// <summary>
    /// Supported operations: text_literal, text_regex, text_count, json_select,
    /// ndjson_select. Query is a literal/regex or jq program. Input is ALWAYS a file.
    /// Result is a compact manifest with a stable path to the entire output.
    /// </summary>
    public static async Task<JsonObject> QueryAsync(
        string? sourcePath, string? operation, string? query, string outputDirectory,
        int maxPreviewRows = 10, int timeoutSeconds = 60, CancellationToken cancellationToken = default)
    {
        var op = (operation ?? "").Trim().ToLowerInvariant();
        if (op is not ("text_literal" or "text_regex" or "text_count" or "json_select" or "ndjson_select"))
            return Error("invalid_operation", "Supported: text_literal, text_regex, text_count, json_select, ndjson_select.");
        if (string.IsNullOrWhiteSpace(sourcePath))
            return Error("source_missing", "source_path is required.");
        var source = Path.GetFullPath(sourcePath);
        if (!File.Exists(source))
            return Error("source_missing", source);
        if (query is null || query.Length is 0 or > 4000)
            return Error("invalid_query", "query must have 1 to 4000 characters.");
        if (op is "json_select" or "ndjson_select" && query.StartsWith("-", StringComparison.Ordinal))
            return Error("invalid_query", "jq filter may not begin with a command line option.");

        var tool = op.StartsWith("json", StringComparison.Ordinal) || op == "ndjson_select" ? "jq" : "rg";
        var executable = Locate(tool);
        if (executable is null)
            return Error("executable_missing", tool + " executable not found in configured tool roots.");
        timeoutSeconds = Math.Clamp(timeoutSeconds, 1, 120);
        maxPreviewRows = Math.Clamp(maxPreviewRows, 0, 30);
        Directory.CreateDirectory(outputDirectory);
        var id = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N")[..10];
        var resultPath = Path.Combine(outputDirectory, "filter_" + id + ".txt");
        var errorPath = resultPath + ".stderr.txt";
        var args = new List<string>();
        if (tool == "rg")
        {
            args.AddRange(new[] { "--no-heading", "--color", "never", "--no-messages" });
            if (op == "text_literal") args.Add("-F");
            if (op == "text_count") args.Add("--count-matches");
            else args.Add("--line-number");
            args.Add("--");
            args.Add(query);
            args.Add(source);
        }
        else
        {
            args.Add("-c");
            if (op == "ndjson_select") args.Add("-n");
            // NDJSON: each input line is a JSON record; inputs skips empty lines.
            args.Add(op == "ndjson_select" ? "inputs | (" + query + ")" : query);
            args.Add(source);
        }
        var psi = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = outputDirectory
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        using var process = new Process { StartInfo = psi };
        var started = Stopwatch.StartNew();
        try
        {
            if (!process.Start())
                return Error("executable_failed", tool + " did not start.");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return Error("executable_failed", ex.Message);
        }

        long lines = 0;
        var previews = new List<string>();
        var stdout = Task.Run(async () =>
        {
            await using var file = new FileStream(resultPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 32768, true);
            await using var writer = new StreamWriter(file, new UTF8Encoding(false));
            string? line;
            while ((line = await process.StandardOutput.ReadLineAsync(cancellationToken)) is not null)
            {
                await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
                lines++;
                if (previews.Count < maxPreviewRows)
                    previews.Add(line.Length > 700 ? line[..700] : line);
            }
        }, cancellationToken);
        var stderr = Task.Run(async () =>
        {
            await using var file = new FileStream(errorPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 16384, true);
            await process.StandardError.BaseStream.CopyToAsync(file, cancellationToken);
        }, cancellationToken);
        var timedOut = false;
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linked.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            await process.WaitForExitAsync(linked.Token);
            await Task.WhenAll(stdout, stderr);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            timedOut = true;
            try { process.Kill(entireProcessTree: true); } catch { }
            try { await process.WaitForExitAsync(CancellationToken.None); } catch { }
            await Task.WhenAll(stdout, stderr);
        }
        started.Stop();
        cancellationToken.ThrowIfCancellationRequested();
        var code = process.ExitCode;
        var status = timedOut ? "timeout" : code == 0 ? "ok" : code == 1 && tool == "rg" ? "zero_matches" : "error";
        string hash = "";
        if (File.Exists(resultPath))
        {
            await using var resultStream = File.OpenRead(resultPath);
            hash = Convert.ToHexString(await SHA256.HashDataAsync(resultStream, cancellationToken));
        }
        var bytes = File.Exists(resultPath) ? new FileInfo(resultPath).Length : 0;
        var errors = File.Exists(errorPath) ? await File.ReadAllTextAsync(errorPath, cancellationToken) : "";
        if (errors.Length > 500) errors = errors[..500];
        return new JsonObject
        {
            ["status"] = status,
            ["tool"] = tool,
            ["operation"] = op,
            ["source_path"] = source,
            ["source_bytes"] = new FileInfo(source).Length,
            ["output_path"] = resultPath,
            ["output_bytes"] = bytes,
            ["output_sha256"] = hash,
            ["stderr_path"] = errorPath,
            ["stderr_preview"] = errors,
            ["output_lines"] = lines,
            ["preview"] = new JsonArray(previews.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()),
            ["exit_code"] = code,
            ["elapsed_ms"] = started.ElapsedMilliseconds,
            ["partial"] = timedOut
        };
    }

    private static JsonObject Error(string status, string detail) => new()
    {
        ["status"] = status,
        ["message"] = detail
    };
}
