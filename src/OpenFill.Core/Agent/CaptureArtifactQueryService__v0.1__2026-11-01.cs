using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace OpenFill.Core.Agent;

// A06: resolve an existing capture by its opaque ID, verify its bytes, then query locally.
// No browser navigation or account inference. This is historical data, not current UI state.
public static class CaptureArtifactQueryService
{
    private static readonly Regex CaptureIdPattern = new(
        @"^capture_[0-9]{8}_[0-9]{6}_[0-9]{3}_[0-9a-f]{6}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static JsonObject Resolve(string capturesRoot, string captureId, string fileKind)
    {
        if (!CaptureIdPattern.IsMatch(captureId ?? ""))
            return Error("capture_id_invalid");
        if (fileKind is not ("text" or "elements" or "html"))
            return Error("capture_kind_invalid");
        try
        {
            var root = Path.GetFullPath(capturesRoot);
            var rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var manifestPath = Path.Combine(root, captureId + ".manifest.json");
            string? externalFolder = null;
            if (!File.Exists(manifestPath))
            {
                // OF-000027: find captures in nested folders of the shared root.
                if (!Directory.Exists(root)) return Error("capture_manifest_missing");
                var candidates = Directory.EnumerateFiles(root,
                    captureId + ".manifest.json",
                    new EnumerationOptions {
                        RecurseSubdirectories = true,
                        IgnoreInaccessible = true,
                        MaxRecursionDepth = 4
                    }).Take(2).ToArray();
                if (candidates.Length > 1) return Error("capture_manifest_ambiguous");
                if (candidates.Length == 1) manifestPath = candidates[0];
                else
                {
                    // OF-000028: explicit absolute capture destinations are
                    // registered at capture time, never discovered by disk scans.
                    var indexPath = Path.Combine(root, captureId + ".external-index.json");
                    if (!File.Exists(indexPath)) return Error("capture_manifest_missing");
                    var index = JsonNode.Parse(File.ReadAllText(indexPath)) as JsonObject;
                    if (index is null || index["capture_id"]?.ToString() != captureId)
                        return Error("capture_external_index_invalid");
                    var registeredPath = index["manifest_path"]?.ToString() ?? "";
                    var registeredFolder = index["source_directory"]?.ToString() ?? "";
                    if (!Path.IsPathFullyQualified(registeredPath) ||
                        !Path.IsPathFullyQualified(registeredFolder))
                        return Error("capture_external_index_invalid");
                    manifestPath = Path.GetFullPath(registeredPath);
                    externalFolder = Path.GetFullPath(registeredFolder);
                    if (!Path.GetDirectoryName(manifestPath)!.Equals(
                            externalFolder, StringComparison.OrdinalIgnoreCase) ||
                        !Path.GetFileName(manifestPath).Equals(
                            captureId + ".manifest.json", StringComparison.Ordinal))
                        return Error("capture_external_index_invalid");
                    if (!File.Exists(manifestPath)) return Error("capture_manifest_missing");
                    var indexedHash = index["manifest_sha256"]?.ToString() ?? "";
                    if (indexedHash.Length != 64 ||
                        !Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(manifestPath)))
                            .Equals(indexedHash, StringComparison.OrdinalIgnoreCase))
                        return Error("capture_external_manifest_hash_mismatch");
                }
            }
            var manifest = JsonNode.Parse(File.ReadAllText(manifestPath)) as JsonObject;
            if (manifest is null) return Error("capture_manifest_invalid");
            if (!string.Equals(manifest["capture_id"]?.ToString(), captureId, StringComparison.Ordinal))
                return Error("capture_manifest_id_mismatch");
            if (manifest["files"] is not JsonArray files)
                return Error("capture_files_invalid");
            var matches = files.OfType<JsonObject>()
                .Where(x => x["kind"]?.ToString() == fileKind).ToArray();
            if (matches.Length != 1) return Error("capture_kind_missing_or_ambiguous");
            var entry = matches[0];
            var rawPath = entry["path"]?.ToString();
            if (string.IsNullOrWhiteSpace(rawPath) || !Path.IsPathFullyQualified(rawPath))
                return Error("capture_path_invalid");
            var path = Path.GetFullPath(rawPath);
            var allowedPrefix = externalFolder is null ? rootPrefix :
                externalFolder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(allowedPrefix, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(path).StartsWith(captureId + ".", StringComparison.OrdinalIgnoreCase))
                return Error("capture_path_outside_scope");
            if (!File.Exists(path)) return Error("capture_file_missing");
            var expectedHash = entry["sha256"]?.ToString();
            if (string.IsNullOrWhiteSpace(expectedHash) || expectedHash.Length != 64)
                return Error("capture_hash_invalid");
            var actualHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
            if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                return Error("capture_hash_mismatch");
            if (!long.TryParse(entry["bytes"]?.ToString(), out var bytes) ||
                bytes != new FileInfo(path).Length)
                return Error("capture_size_mismatch");
            return new JsonObject {
                ["status"] = "ready", ["capture_id"] = captureId,
                ["file_kind"] = fileKind, ["source_path"] = path,
                ["source_sha256"] = actualHash.ToLowerInvariant(),
                ["manifest_path"] = manifestPath,
                ["source_url"] = manifest["source_url"]?.ToString(),
                ["source_scope"] = manifest["source_scope"]?.ToString(),
                ["completeness"] = manifest["completeness"]?.ToString()
            };
        }
        catch (JsonException) { return Error("capture_manifest_invalid"); }
        catch (IOException) { return Error("capture_file_unreadable"); }
        catch (UnauthorizedAccessException) { return Error("capture_file_unreadable"); }
        catch (ArgumentException) { return Error("capture_path_invalid"); }
    }

    public static async Task<JsonObject> QueryAsync(string capturesRoot, string captureId,
        string fileKind, string operation, string query, string outputRoot,
        int maxPreviewRows, CancellationToken ct)
    {
        var resolved = Resolve(capturesRoot, captureId, fileKind);
        if (resolved["status"]?.ToString() != "ready") return resolved;
        if ((fileKind == "elements" && operation != "json_select") ||
            (fileKind != "elements" && operation is not ("text_literal" or "text_regex" or "text_count")))
            return Error("capture_operation_kind_mismatch");
        var result = await FileFilterService.QueryAsync(resolved["source_path"]!.ToString(),
            operation, query, outputRoot, maxPreviewRows, 60, ct);
        result["capture_id"] = captureId;
        result["file_kind"] = fileKind;
        result["manifest_path"] = resolved["manifest_path"]?.ToString();
        result["source_sha256"] = resolved["source_sha256"]?.ToString();
        result["source_url"] = resolved["source_url"]?.ToString();
        result["source_scope"] = resolved["source_scope"]?.ToString();
        result["completeness"] = resolved["completeness"]?.ToString();
        result["reused_snapshot"] = true;
        return result;
    }

    private static JsonObject Error(string reason) => new() {
        ["status"] = "error", ["reason_code"] = reason, ["reused_snapshot"] = false
    };
}
