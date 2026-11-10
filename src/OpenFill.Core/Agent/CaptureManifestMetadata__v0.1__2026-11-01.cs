using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace OpenFill.Core.Agent;

// A04: provenance and stable view identity. No inferred account or pagination completeness.
public static class CaptureManifestMetadata
{
    // Shared by persisted captures and the lightweight live DOM probe.
    public static string ComputeViewSignature(string url, string selector, string mode, JsonArray? elements)
    {
        var structure = new StringBuilder();
        if (elements is not null)
            foreach (var element in elements)
            {
                if (element is not JsonObject e) continue;
                structure.Append(e["tag"]?.ToString()).Append(':')
                    .Append(e["role"]?.ToString()).Append(':')
                    .Append(e["type"]?.ToString()).Append(':')
                    .Append(e["name"]?.ToString()).Append(';');
            }
        return ComputeViewSignatureFromStructure(url, selector, mode, structure.ToString());
    }

    public static string ComputeViewSignatureFromStructure(string url, string selector,
        string mode, string structure)
    {
        var uri = Uri.TryCreate(url, UriKind.Absolute, out var parsed) ? parsed : null;
        return Hash((uri?.Host.ToLowerInvariant() ?? "") + "\n" +
            (uri?.AbsolutePath ?? "") + "\n" + selector + "\n" + mode + "\n" + structure);
    }

    public static void Apply(JsonObject manifest, JsonObject observation, string mode)
    {
        var url = observation["url"]?.ToString() ?? "";
        var uri = Uri.TryCreate(url, UriKind.Absolute, out var parsed) &&
                  parsed.Scheme is "https" or "http" ? parsed : null;
        var host = uri?.Host.ToLowerInvariant() ?? "";
        var route = uri?.AbsolutePath ?? "";
        var selector = observation["selector"]?.ToString() ?? "body";
        var fileHashes = new StringBuilder();
        if (manifest["files"] is JsonArray files)
            foreach (var file in files)
                if (file is JsonObject f)
                    fileHashes.Append(f["kind"]?.ToString()).Append(':')
                        .Append(f["sha256"]?.ToString()).Append('\n');
        manifest["provider"] = host.EndsWith(".linkedin.com") || host == "linkedin.com" ? "linkedin" :
            host.EndsWith(".airbnb.com") || host == "airbnb.com" ? "airbnb" :
            host == "web.whatsapp.com" ? "whatsapp" : "other";
        manifest["source_kind"] = "webview2_dom";
        manifest["normalized_route"] = route;
        manifest["root_selector"] = selector;
        manifest["capture_mode"] = mode;
        // Text captures now carry the same bounded selector-relative structure
        // used by CaptureRecipeLiveProbe, without exporting element contents.
        manifest["view_signature"] = mode == "text" &&
            observation["structureComplete"]?.ToString() == "true" &&
            observation["structure"] is not null
            ? ComputeViewSignatureFromStructure(url, selector, mode,
                observation["structure"]!.ToString())
            : ComputeViewSignature(url, selector, mode,
                observation["elements"] as JsonArray);
        manifest["content_fingerprint"] = Hash(fileHashes.ToString());
        manifest["observed_at_utc"] = observation["capturedUtc"]?.ToString();
        manifest["completeness"] = "unverified";
        manifest["virtualized"] = null;
        manifest["lazy_not_loaded"] = null;
        manifest["account_scope"] = CaptureAccountScopeGate.Resolve(observation);
        manifest["account_scope_source"] = manifest["account_scope"] is null ? null : "linkedin_signed_in_self_menu";
    }

    private static string Hash(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
