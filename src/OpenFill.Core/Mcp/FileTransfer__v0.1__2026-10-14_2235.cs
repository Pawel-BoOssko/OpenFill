// OpenFill - Metadata: wersja 0.1, data 2026-10-14 22:35
using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace OpenFill.Core.Mcp;

/// <summary>Short-lived, scoped HTTP file transfers. Binary bytes never enter the MCP JSON context.</summary>
public sealed class FileTransfer
{
    private const long MaxBytes = 25L * 1024 * 1024;
    private readonly string _root;
    private readonly ConcurrentDictionary<string, Ticket> _tickets = new(StringComparer.Ordinal);
    private sealed record Ticket(string Mode, string FullPath, string RelativeName, DateTime ExpiresUtc);

    public FileTransfer(string sharedFolder) => _root = Path.GetFullPath(sharedFolder);

    public JsonObject Prepare(string? mode, string? file, string linkBase)
    {
        mode = mode?.Trim().ToLowerInvariant();
        if (mode is not ("upload" or "download"))
            return Fail("mode must be upload or download");
        if (string.IsNullOrWhiteSpace(file) || file.Length > 120 ||
            !Regex.IsMatch(file, @"^[A-Za-z0-9][A-Za-z0-9._ -]{0,119}$") ||
            file is "." or ".." || file.Contains("..", StringComparison.Ordinal))
            return Fail("file must be a simple file name (no directories or traversal)");
        Directory.CreateDirectory(_root);
        string relative;
        string full;
        if (mode == "download")
        {
            relative = file;
            full = Path.Combine(_root, relative);
            if (!File.Exists(full) || (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
                return Fail("File not found in shared folder (or is a link)");
            if (new FileInfo(full).Length > MaxBytes) return Fail("File exceeds 25 MiB");
        }
        else
        {
            var suffix = Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant();
            relative = "incoming/" + suffix + "-" + file;
            full = Path.Combine(_root, "incoming", suffix + "-" + file);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        }
        foreach (var (key, ticket) in _tickets)
            if (ticket.ExpiresUtc < DateTime.UtcNow) _tickets.TryRemove(key, out _);
        if (_tickets.Count >= 100) return Fail("Too many pending file transfers");
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        _tickets[token] = new Ticket(mode, full, relative, DateTime.UtcNow.AddMinutes(10));
        return new JsonObject
        {
            ["status"] = "ok",
            ["mode"] = mode,
            ["url"] = TransferUrl(linkBase, token),
            ["http_method"] = mode == "upload" ? "PUT" : "GET",
            ["file"] = relative,
            ["local_path"] = full,
            ["expires_in_seconds"] = 600,
            ["max_bytes"] = MaxBytes,
            ["note"] = "Private, short-lived URL. Transfer raw file bytes over HTTP; do not paste binary or base64 into MCP messages. The recipient should use the URL only for this authorized file transfer."
        };
    }

    // Trusted, internal screenshot source: the model sees an independent scoped URL, not the MCP master secret.
    internal string? PrepareScreenshot(string absoluteFile, string linkBase)
    {
        if (!File.Exists(absoluteFile) || new FileInfo(absoluteFile).Length > MaxBytes) return null;
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        _tickets[token] = new Ticket("download", absoluteFile, Path.GetFileName(absoluteFile), DateTime.UtcNow.AddMinutes(10));
        return TransferUrl(linkBase, token);
    }

    private static string TransferUrl(string linkBase, string token)
    {
        var marker = linkBase.IndexOf("/mcp/", StringComparison.Ordinal);
        var root = marker >= 0 ? linkBase[..marker] : new Uri(linkBase).GetLeftPart(UriPartial.Authority);
        return root.TrimEnd('/') + "/files/" + token;
    }

    public async Task ServeAsync(HttpListenerContext ctx, string token)
    {
        var res = ctx.Response;
        try
        {
            if (!Regex.IsMatch(token, "^[a-f0-9]{48}$") || !_tickets.TryGetValue(token, out var t) ||
                t.ExpiresUtc <= DateTime.UtcNow)
            {
                await SendAsync(res, 404, "Not found");
                return;
            }
            if ((t.Mode == "upload" && ctx.Request.HttpMethod != "PUT") ||
                (t.Mode == "download" && ctx.Request.HttpMethod != "GET"))
            {
                await SendAsync(res, 405, "Method not allowed");
                return;
            }
            res.AddHeader("Cache-Control", "private, no-store");
            res.AddHeader("X-Content-Type-Options", "nosniff");
            if (t.Mode == "download")
            {
                if (!File.Exists(t.FullPath) || (File.GetAttributes(t.FullPath) & FileAttributes.ReparsePoint) != 0)
                {
                    await SendAsync(res, 404, "File not found"); return;
                }
                using var source = new FileStream(t.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (source.Length > MaxBytes) { await SendAsync(res, 413, "File too large"); return; }
                res.StatusCode = 200;
                res.ContentType = "application/octet-stream";
                res.AddHeader("Content-Disposition", "attachment; filename*=UTF-8''" + Uri.EscapeDataString(Path.GetFileName(t.FullPath)));
                res.ContentLength64 = source.Length;
                await source.CopyToAsync(res.OutputStream);
                res.Close();
                return;
            }

            if (!_tickets.TryRemove(token, out _)) { await SendAsync(res, 409, "Token used"); return; }
            if (ctx.Request.ContentLength64 > MaxBytes)
            {
                await SendAsync(res, 413, "File too large"); return;
            }
            long copied = 0;
            try
            {
                using (var sink = new FileStream(t.FullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    var buffer = new byte[65536];
                    int read;
                    while ((read = await ctx.Request.InputStream.ReadAsync(buffer)) > 0)
                    {
                        copied += read;
                        if (copied > MaxBytes) throw new InvalidDataException("File too large");
                        await sink.WriteAsync(buffer.AsMemory(0, read));
                    }
                }
                await SendAsync(res, 201, "Saved " + t.RelativeName + " (" + copied + " bytes)");
            }
            catch
            {
                try { File.Delete(t.FullPath); } catch { }
                throw;
            }
        }
        catch (Exception)
        {
            if (res.OutputStream.CanWrite)
                try { await SendAsync(res, 400, "Transfer failed"); } catch { }
        }
    }

    private static JsonObject Fail(string detail) => new() { ["status"] = "error", ["message"] = detail };
    private static async Task SendAsync(HttpListenerResponse res, int status, string message)
    {
        var body = Encoding.UTF8.GetBytes(message);
        res.StatusCode = status;
        res.ContentType = "text/plain; charset=utf-8";
        res.ContentLength64 = body.Length;
        await res.OutputStream.WriteAsync(body);
        res.Close();
    }
}
