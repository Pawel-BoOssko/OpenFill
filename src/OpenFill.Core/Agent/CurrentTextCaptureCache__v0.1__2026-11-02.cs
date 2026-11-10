using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using OpenFill.Core.Browser;

namespace OpenFill.Core.Agent;

// A27: exact current-DOM text reuse for neutral pages. A saved capture is NEVER
// treated as current without a fresh live comparison; private hosts stay excluded.
public static class CurrentTextCaptureCache
{
    private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(30);
    private const string ProbeScript = """
    (() => {
      const root=document.querySelector(__SELECTOR__);
      const verifyStructure=__VERIFY_STRUCTURE__;
      let structure=null;
      if(root && verifyStructure) {
        const nodes=root.querySelectorAll("a[href],button,input,textarea,select,[role=button],[role=link],[role=tab],[role=checkbox],[role=textbox],h1,h2,h3");
        if(nodes.length<=3000) {
          structure="";
          for(const e of nodes) {
            structure+=e.tagName.toLowerCase()+":"+(e.getAttribute("role")||"")+":"+
              (e.getAttribute("type")||"")+":"+(e.getAttribute("name")||"")+";";
          }
        }
      }
      return { url:location.href, title:document.title,
        login_form:!!document.querySelector('input[type="password"]'),
        text:root?(root.innerText||root.textContent||""):null, structure };
    })()
    """;

    public static string Sha(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    // Pure matcher, useful in a single focused test without a browser.
    public static bool IsExactMatch(JsonObject manifest, string currentUrl,
        string selector, string currentText, string capturesRoot, DateTimeOffset now)
    {
        if (!Uri.TryCreate(currentUrl, UriKind.Absolute, out var url) ||
            url.Scheme is not ("https" or "http") ||
            CaptureRecipeScopeGate.IsSensitiveHost(url.Host)) return false;
        if (!string.Equals(manifest["source_url"]?.ToString(), currentUrl, StringComparison.Ordinal) ||
            !string.Equals(manifest["source_selector"]?.ToString(), selector, StringComparison.Ordinal) ||
            manifest["capture_mode"]?.ToString() != "text" ||
            !string.IsNullOrWhiteSpace(manifest["account_scope"]?.ToString()) ||
            manifest["source_scope"]?.ToString() != "currently_loaded_dom") return false;
        if (!DateTimeOffset.TryParse(manifest["observed_at_utc"]?.ToString(), out var when) ||
            when > now || now - when > MaxAge) return false;
        if (manifest["files"] is not JsonArray files ||
            files.Count != 1 || files[0] is not JsonObject entry ||
            entry["kind"]?.ToString() != "text") return false;
        var expectedHash = Sha(currentText);
        if (!string.Equals(entry["sha256"]?.ToString(), expectedHash,
                StringComparison.OrdinalIgnoreCase)) return false;
        var full = Path.GetFullPath(entry["path"]?.ToString() ?? "");
        var root = Path.GetFullPath(capturesRoot).TrimEnd(Path.DirectorySeparatorChar)
                   + Path.DirectorySeparatorChar;
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(full)) return false;
        return string.Equals(
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(full))),
            expectedHash, StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<JsonObject?> TryReuseAsync(
        BrowserController browser, string capturesRoot, string selector, CancellationToken ct,
        string? expectedUrl = null, string? expectedViewSignature = null)
    {
        if (!Directory.Exists(capturesRoot)) return null;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var currentUrl = await browser.CurrentUrlAsync(ct);
        if (!Uri.TryCreate(currentUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("https" or "http") ||
            CaptureRecipeScopeGate.IsSensitiveHost(uri.Host) ||
            (!string.IsNullOrWhiteSpace(expectedUrl) &&
             !string.Equals(expectedUrl, currentUrl, StringComparison.Ordinal))) return null;

        // Check source URL, selector and freshness before asking WebView2 for
        // a live text snapshot. An unrelated cache must not cost a DOM read.
        var now = DateTimeOffset.UtcNow;
        var eligible = new List<JsonObject>();
        var candidates = Directory.EnumerateFiles(capturesRoot, "capture_*.manifest.json")
            .Select(p => new FileInfo(p)).OrderByDescending(f => f.LastWriteTimeUtc)
            .Take(32);
        foreach (var file in candidates)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var manifest = JsonNode.Parse(await File.ReadAllTextAsync(file.FullName, ct)) as JsonObject;
                if (manifest is null ||
                    manifest["source_url"]?.ToString() != currentUrl ||
                    manifest["source_selector"]?.ToString() != selector ||
                    manifest["capture_mode"]?.ToString() != "text" ||
                    !string.IsNullOrWhiteSpace(manifest["account_scope"]?.ToString()) ||
                    manifest["source_scope"]?.ToString() != "currently_loaded_dom" ||
                    (!string.IsNullOrWhiteSpace(expectedViewSignature) &&
                     !string.Equals(manifest["view_signature"]?.ToString(), expectedViewSignature,
                         StringComparison.OrdinalIgnoreCase)) ||
                    !DateTimeOffset.TryParse(manifest["observed_at_utc"]?.ToString(), out var when) ||
                    when > now || now - when > MaxAge) continue;
                eligible.Add(manifest);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (System.Text.Json.JsonException) { }
            catch (ArgumentException) { }
        }
        if (eligible.Count == 0) return null;

        var js = ProbeScript.Replace("__SELECTOR__",
            System.Text.Json.JsonSerializer.Serialize(selector))
            .Replace("__VERIFY_STRUCTURE__", string.IsNullOrWhiteSpace(expectedViewSignature) ? "false" : "true");
        var result = await browser.RunJsAsync(js, ct);
        if (result?["ok"]?.GetValue<bool>() != true ||
            result["value"] is not JsonObject live ||
            live["login_form"]?.GetValue<bool>() == true ||
            live["text"] is null ||
            live["url"]?.ToString() != currentUrl) return null;
        if (!string.IsNullOrWhiteSpace(expectedViewSignature) &&
            (live["structure"] is null ||
             !string.Equals(CaptureManifestMetadata.ComputeViewSignatureFromStructure(
                 currentUrl, selector, "text", live["structure"]!.ToString()),
                 expectedViewSignature, StringComparison.OrdinalIgnoreCase)))
            return null;
        var text = live["text"]!.ToString();
        foreach (var manifest in eligible)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (!IsExactMatch(manifest, currentUrl, selector, text, capturesRoot, now))
                    continue;
                var reuse = (JsonObject)manifest.DeepClone();
                reuse["status"] = "saved";
                reuse["reused_snapshot"] = true;
                reuse["cache_verification"] = "exact_live_dom_text_sha256";
                reuse["url"] = currentUrl;
                reuse["title"] = live["title"]?.ToString();
                reuse["selector"] = selector;
                reuse["mode"] = "text";
                reuse["elapsed_ms"] = clock.ElapsedMilliseconds;
                return reuse;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (System.Text.Json.JsonException) { }
            catch (ArgumentException) { }
        }
        return null;
    }
}
