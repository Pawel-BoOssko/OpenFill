// OF-000027: real snapshot to nested captures folder -> resolve by ID ->
// local rg query, manifest/SHA verification, ambiguity check. No WebView2.
using System.Text.Json.Nodes;
using OpenFill.Core.Agent;

internal static class NestedCaptureQuerySmoke
{
    private static int checks;
    private static void Check(bool condition, string code)
    {
        if (!condition) throw new InvalidOperationException("OF027_NESTED_CAPTURE_" + code);
        checks++;
    }

    public static async Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "of027_capture_" + Guid.NewGuid().ToString("N"));
        var captures = Path.Combine(root, "captures");
        var nested = Path.Combine(captures, "task-027", "page");
        Directory.CreateDirectory(nested);
        try
        {
            var snapshot = await PageCaptureService.SaveSnapshotForTestAsync(
                new JsonObject
                {
                    ["text"] = "Header\nneedle OF027 found\nFooter",
                    ["url"] = "https://example.test/inbox",
                    ["title"] = "Fixture",
                    ["selector"] = "body",
                    ["capturedUtc"] = DateTimeOffset.UtcNow.ToString("O")
                }, "text", "", nested, 10);
            var id = snapshot["capture_id"]?.ToString() ?? "";
            Check(id.StartsWith("capture_", StringComparison.Ordinal), "created_id");
            var resolved = CaptureArtifactQueryService.Resolve(captures, id, "text");
            Check(resolved["status"]?.ToString() == "ready", "nested_resolved");
            Check(resolved["manifest_path"]?.ToString() ==
                  Path.Combine(nested, id + ".manifest.json"), "nested_manifest");
            var result = await CaptureArtifactQueryService.QueryAsync(captures,
                id, "text", "text_literal", "needle", Path.Combine(root, "filters"),
                5, CancellationToken.None);
            Check(result["status"]?.ToString() == "ok" &&
                  result["reused_snapshot"]?.GetValue<bool>() == true, "query_ok_no_dom");
            Check(result["preview"] is JsonArray lines &&
                  lines.Any(x => x?.ToString().Contains("needle", StringComparison.Ordinal) == true),
                  "result_matches");
            Check(File.Exists(result["output_path"]?.ToString() ?? ""), "file_output");
            var source = resolved["source_path"]?.ToString() ?? "";
            await File.AppendAllTextAsync(source, "\ntampered");
            Check(CaptureArtifactQueryService.Resolve(captures, id, "text")["reason_code"]?.ToString()
                  == "capture_hash_mismatch", "tampered_blocked");

            var other = Path.Combine(captures, "task-other");
            Directory.CreateDirectory(other);
            File.Copy(Path.Combine(nested, id + ".manifest.json"),
                Path.Combine(other, id + ".manifest.json"));
            Check(CaptureArtifactQueryService.Resolve(captures, id, "text")["reason_code"]?.ToString()
                  == "capture_manifest_ambiguous", "duplicate_manifest_blocked");

            // OF-000028: a locally authorized destination outside shared/captures
            // must remain accessible by ID through a small root index.
            var external = Path.Combine(root, "explicit-output");
            Directory.CreateDirectory(external);
            var outsideSnapshot = await PageCaptureService.SaveSnapshotForTestAsync(
                new JsonObject { ["text"] = "external OF028 capture",
                    ["url"] = "https://example.test/external",
                    ["selector"] = "body" }, "text", "", external, 10,
                CancellationToken.None, captures);
            var externalId = outsideSnapshot["capture_id"]?.ToString() ?? "";
            var indexPath = Path.Combine(captures, externalId + ".external-index.json");
            Check(File.Exists(indexPath), "external_index_published");
            var externalResolved = CaptureArtifactQueryService.Resolve(captures, externalId, "text");
            Check(externalResolved["status"]?.ToString() == "ready" &&
                  externalResolved["source_path"]?.ToString() ==
                  Path.Combine(external, externalId + ".txt"), "external_index_resolved");
            var externalQuery = await CaptureArtifactQueryService.QueryAsync(captures,
                externalId, "text", "text_literal", "OF028",
                Path.Combine(root, "filters"), 5, CancellationToken.None);
            Check(externalQuery["status"]?.ToString() == "ok" &&
                  externalQuery["reused_snapshot"]?.GetValue<bool>() == true,
                  "external_query_works");
            var externalFile = Path.Combine(external, externalId + ".txt");
            await File.AppendAllTextAsync(externalFile, "tamper");
            Check(CaptureArtifactQueryService.Resolve(captures, externalId, "text")
                  ["reason_code"]?.ToString() == "capture_hash_mismatch",
                  "external_data_hash_mismatch");
            var externalManifest = Path.Combine(external, externalId + ".manifest.json");
            await File.AppendAllTextAsync(externalManifest, " ");
            Check(CaptureArtifactQueryService.Resolve(captures, externalId, "text")
                  ["reason_code"]?.ToString() == "capture_external_manifest_hash_mismatch",
                  "external_manifest_hash_mismatch");
            Console.WriteLine($"OF028_NESTED_EXTERNAL_CAPTURE_{checks}_CHECKS_OK");
        }
        finally
        {
            try { Directory.Delete(root, true); } catch (IOException) { }
        }
    }
}
