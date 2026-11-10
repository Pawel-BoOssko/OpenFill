// AUTH_BEFORE_CAPTURE_20261030
// OpenFill SOURCE only - DOM -> files -> filters -> compact Luna result, 2026-10-30
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using OpenFill.Core.Browser;

namespace OpenFill.Core.Agent;

public sealed class PageCaptureService(BrowserController browser, string sharedDirectory, CaptureRecipeStore recipes)
{
    private const int MaxDocumentChars = 10_000_000;
    public async Task<JsonObject> CaptureAsync(string mode, string selector, string pattern, string? outputFolder, int maxMatches, CancellationToken ct,
        string? expectedUrl = null, bool requireAuthenticated = false, string? authenticatedSelector = null, bool allowLoginPage = false,
        string? expectedViewSignature = null, string? expectedAccountScope = null)
    {
        mode = string.IsNullOrWhiteSpace(mode) ? "all" : mode.ToLowerInvariant();
        if (mode is not ("text" or "elements" or "html" or "all")) throw new ArgumentException("mode must be text, elements, html or all");
        selector = string.IsNullOrWhiteSpace(selector) ? "body" : selector.Trim();
        pattern = (pattern ?? "").Trim();
        if (selector.Length > 400 || pattern.Length > 400) throw new ArgumentException("Selector or regex is too long.");
        maxMatches = Math.Clamp(maxMatches, 1, 500);
        // A27: reuse only a recent text-only capture of a neutral page after
        // comparing its SHA256 with the CURRENT live DOM; no stale account data.
        if (mode == "text" && pattern.Length == 0 &&
            string.IsNullOrWhiteSpace(outputFolder) &&
            !requireAuthenticated && string.IsNullOrWhiteSpace(authenticatedSelector) &&
            !allowLoginPage && string.IsNullOrWhiteSpace(expectedAccountScope))
        {
            try
            {
                var cached = await CurrentTextCaptureCache.TryReuseAsync(browser,
                    Path.Combine(sharedDirectory, "captures"), selector, ct,
                    expectedUrl, expectedViewSignature);
                if (cached is not null) return cached;
            }
            catch (OperationCanceledException) { throw; }
            catch (IOException) { } // fresh capture on cache I/O failure
            catch (InvalidOperationException) { }
        }
        // Validate a filter before invoking the browser.
        if (pattern.Length > 0) _ = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(300));
        var sw = Stopwatch.StartNew();
        if (!string.IsNullOrWhiteSpace(authenticatedSelector) && authenticatedSelector.Length > 400)
            throw new ArgumentException("authenticatedSelector too long.");
        var css = JsonSerializer.Serialize(selector);
        var authCss = JsonSerializer.Serialize(authenticatedSelector ?? "");
        var js = """
        (async () => {
          const sel = __SELECTOR__;
          const root = document.querySelector(sel);
          // Root check is AFTER authentication metadata; login pages may not have the requested selector.
          const mode = "__MODE__";
          const authSel = __AUTH_SELECTOR__;
          const isLinkedIn = location.hostname === "linkedin.com" || location.hostname.endsWith(".linkedin.com");
          const defaultAuthSel = isLinkedIn ? "nav.global-nav,header.global-nav,#global-nav,[data-test-global-nav],.global-nav__me" : "";
          const markerSelector = authSel || defaultAuthSel;
          // LinkedIn's newer navigation has no global-nav class. Require its
          // account-area trio plus a profile-menu toggle, not a generic nav.
          const modernAuthNav = isLinkedIn && [...document.querySelectorAll('nav')].some(n =>
            !!n.querySelector('a[href*="/mynetwork"]') &&
            !!n.querySelector('a[href*="/messaging"]') &&
            !!n.querySelector('a[href*="/notifications"]') &&
            !!n.querySelector('button[aria-expanded]'));
          const authMarker = (!!markerSelector && !!document.querySelector(markerSelector)) ||
            (!authSel && modernAuthNav);
          const signInForm = !!document.querySelector('form[action*="/login"],input[type="password"],input[name="session_key"]');
          const accountSelfLinks = isLinkedIn ? await __LINKEDIN_SCOPE_PROBE__ : [];
          const data = { url: location.href, title: document.title, capturedUtc: new Date().toISOString(),
            selector: sel, authenticatedMarker: authMarker, loginForm: signInForm, accountSelfLinks };
          if (!root) return { ...data, captureError: "SELECTOR_NOT_FOUND" };
          if (mode === "all" || mode === "text") data.text = root.innerText || root.textContent || "";
          // A05: text-only capture records just the selector-relative DOM shape.
          // No labels, message content, HTML or element JSON are added to this mode.
          if (mode === "text") {
            const nodes = root.querySelectorAll("a[href],button,input,textarea,select,[role=button],[role=link],[role=tab],[role=checkbox],[role=textbox],h1,h2,h3");
            data.structureComplete = nodes.length <= 3000;
            if (data.structureComplete) {
              let structure = "";
              for (const e of nodes) {
                structure += e.tagName.toLowerCase()+":"+(e.getAttribute("role")||"")+":"+
                  (e.getAttribute("type")||"")+":"+(e.getAttribute("name")||"")+";";
              }
              data.structure = structure;
            }
          }
          if (mode === "all" || mode === "html") data.html = root.outerHTML || "";
          if (mode === "all" || mode === "elements") {
            const nodes = [...root.querySelectorAll("a[href],button,input,textarea,select,[role=button],[role=link],[role=tab],[role=checkbox],[role=textbox],h1,h2,h3")];
            data.totalElements = nodes.length;
            data.elementsComplete = nodes.length <= 3000;
            data.elements = nodes.slice(0,3000).map((e,i)=>({
              index:i,tag:e.tagName.toLowerCase(),role:e.getAttribute("role")||"",
              label:(e.getAttribute("aria-label")||e.innerText||e.getAttribute("title")||"").trim().slice(0,250),
              href:e.getAttribute("href")||"",type:e.getAttribute("type")||"",
              name:e.getAttribute("name")||"",disabled:!!e.disabled
            }));
          }
          return data;
        })()
        """.Replace("__SELECTOR__", css).Replace("__MODE__", mode)
            .Replace("__AUTH_SELECTOR__", authCss)
            .Replace("__LINKEDIN_SCOPE_PROBE__", LinkedInAccountMenuProbe.Javascript);
        var result = await browser.RunJsAsync(js, ct);
        if (result?["ok"]?.GetValue<bool>() != true)
            throw new InvalidOperationException("WebView2 capture failed: " + result?["error"]?.ToString());
        var payload = result["value"] as JsonObject ?? throw new InvalidOperationException("DOM capture returned no object.");
        // Authentication is checked before selector failure and before writing even one file.
        var auth = PageAuthenticationGate.Check(payload, expectedUrl, requireAuthenticated, allowLoginPage);
        if (auth["status"]?.ToString() != "ready") return auth;
        if (!string.IsNullOrWhiteSpace(expectedAccountScope) &&
            !string.Equals(CaptureAccountScopeGate.Resolve(payload), expectedAccountScope, StringComparison.Ordinal))
            return new JsonObject { ["status"] = "account_changed", ["reason_code"] = "recipe_account_changed_or_unverified",
                ["files_created"] = 0, ["current_url"] = payload["url"]?.ToString() };
        if (payload["captureError"] is not null) throw new InvalidOperationException(payload["captureError"]!.ToString() + ": " + selector);
        if (!string.IsNullOrWhiteSpace(expectedViewSignature))
        {
            var reason = CaptureRecipeRaceGate.Check(payload, expectedUrl ?? "", selector, mode, expectedViewSignature);
            if (reason != "ready") return new JsonObject {
                ["status"] = "view_changed", ["reason_code"] = reason,
                ["current_url"] = payload["url"]?.ToString(), ["files_created"] = 0
            };
        }
        var folder = ResolveOutputFolder(outputFolder);
        var saved = await SaveSnapshotAsync(payload, mode, pattern, folder, maxMatches, ct,
            Path.Combine(sharedDirectory, "captures"));
        sw.Stop();
        var uri = Uri.TryCreate(payload["url"]?.ToString(), UriKind.Absolute, out var parsed) ? parsed : null;
        // Never learn a scoped recipe from a truncated or unknown DOM shape.
        var structureReady = mode switch
        {
            "text" => payload["structureComplete"]?.ToString() == "true",
            "all" or "elements" => payload["elementsComplete"]?.ToString() == "true",
            _ => false
        };
        if (uri is not null && structureReady)
            recipes.RecordScoped(uri.Host, uri.AbsolutePath, mode, selector,
                pattern, sw.ElapsedMilliseconds, saved["view_signature"]?.ToString(),
                saved["account_scope"]?.ToString());
        saved["elapsed_ms"] = sw.ElapsedMilliseconds;
        saved["url"] = payload["url"]?.ToString();
        saved["title"] = payload["title"]?.ToString();
        saved["selector"] = selector;
        saved["mode"] = mode;
        saved["status"] = "saved";
        saved["authentication_verified"] = auth["auth_verified"]?.DeepClone();
        return saved;
    }

    private string ResolveOutputFolder(string? directory)
    {
        var root = Path.GetFullPath(sharedDirectory);
        // Explicit absolute Windows destinations are permitted for locally authorized tasks.
        var destination = string.IsNullOrWhiteSpace(directory) ? Path.Combine(root, "captures") :
            Path.GetFullPath(Path.IsPathFullyQualified(directory) ? directory : Path.Combine(root, directory));
        Directory.CreateDirectory(destination);
        return destination;
    }

    private static async Task<JsonObject> SaveSnapshotAsync(JsonObject data, string mode, string pattern,
        string folder, int maxMatches, CancellationToken ct, string? captureIndexRoot = null)
    {
        var prefix = "capture_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N")[..6];
        var files = new JsonArray();
        // Stage every artifact before publishing. Any failure leaves no partial capture.
        var stagedFiles = new List<(string Temporary, string Final)>();
        string? preview = null;
        int count = 0;
        async Task Save(string extension, string contents, string kind)
        {
            if (contents.Length > MaxDocumentChars) throw new InvalidOperationException(kind + " exceeds max capture size " + MaxDocumentChars + " characters; use a narrower selector.");
            var path = Path.Combine(folder, prefix + extension);
            var temporary = path + ".pending";
            stagedFiles.Add((temporary, path));
            await File.WriteAllTextAsync(temporary, contents, new UTF8Encoding(false), ct);
            await using var sourceStream = File.OpenRead(temporary);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(sourceStream, ct)).ToLowerInvariant();
            files.Add(new JsonObject { ["kind"] = kind, ["path"] = path, ["bytes"] = new FileInfo(temporary).Length, ["sha256"] = hash });
        }

        try
        {
        if (mode is "text" or "all")
        {
            var text = data["text"]?.GetValue<string>() ?? "";
            await Save(".txt", text, "text");
            preview = text.Length > 400 ? text[..400] : text;
            if (pattern.Length > 0)
            {
                var rx = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(300));
                var lines = text.ReplaceLineEndings("\n").Split('\n');
                var matched = new List<string>();
                foreach (var line in lines)
                {
                    if (!rx.IsMatch(line)) continue;
                    count++;
                    if (matched.Count < maxMatches) matched.Add(line.Length > 2000 ? line[..2000] : line);
                }
                var filtered = string.Join("\n", matched);
                await Save(".filtered.txt", filtered, "filtered_matches");
                preview = filtered.Length > 400 ? filtered[..400] : filtered;
            }
        }
        if (mode is "elements" or "all")
        {
            var elements = data["elements"]?.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) ?? "[]";
            await Save(".elements.json", elements, "elements");
        }
        if (mode is "html" or "all")
        {
            await Save(".html", data["html"]?.GetValue<string>() ?? "", "html");
        }
        var manifest = new JsonObject {
            ["capture_id"] = prefix,
            ["source_scope"] = "currently_loaded_dom",
            ["all_pages_complete"] = false,
            ["source_url"] = data["url"]?.ToString(),
            ["source_title"] = data["title"]?.ToString(),
            ["source_selector"] = data["selector"]?.ToString(),
            ["authentication_marker_seen"] = data["authenticatedMarker"]?.DeepClone(),
            ["login_form_seen"] = data["loginForm"]?.DeepClone(),
            ["files"] = files,
            ["preview"] = preview ?? "(file-only capture)",
            ["matches"] = pattern.Length > 0 ? count : null,
            ["items"] = data["totalElements"]?.DeepClone(),
            ["elements_complete"] = data["elementsComplete"]?.DeepClone(),
            ["captured_utc"] = data["capturedUtc"]?.ToString(),
            ["filtered_truncated"] = pattern.Length > 0 && count > maxMatches
        };
        CaptureManifestMetadata.Apply(manifest, data, mode);
        // Publish a persistent manifest together with the capture, without exposing partial files.
        var manifestPath = Path.Combine(folder, prefix + ".manifest.json");
        manifest["manifest_path"] = manifestPath;
        var pendingManifest = manifestPath + ".pending";
        stagedFiles.Add((pendingManifest, manifestPath));
        var manifestBytes = manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(pendingManifest, manifestBytes, new UTF8Encoding(false), ct);
        // OF-000028: an explicitly selected output folder may be outside
        // shared/captures. Publish a tiny immutable lookup index there, not
        // a second copy of the potentially large DOM and HTML files.
        if (!string.IsNullOrWhiteSpace(captureIndexRoot))
        {
            var indexRoot = Path.GetFullPath(captureIndexRoot);
            var actualFolder = Path.GetFullPath(folder);
            var indexPrefix = indexRoot.TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            if (!actualFolder.Equals(indexRoot, StringComparison.OrdinalIgnoreCase) &&
                !actualFolder.StartsWith(indexPrefix, StringComparison.OrdinalIgnoreCase))
            {
                Directory.CreateDirectory(indexRoot);
                var indexPath = Path.Combine(indexRoot, prefix + ".external-index.json");
                var indexTemporary = indexPath + ".pending";
                var index = new JsonObject {
                    ["capture_id"] = prefix,
                    ["manifest_path"] = manifestPath,
                    ["manifest_sha256"] = Convert.ToHexString(
                        SHA256.HashData(Encoding.UTF8.GetBytes(manifestBytes))),
                    ["source_directory"] = actualFolder
                };
                stagedFiles.Add((indexTemporary, indexPath));
                await File.WriteAllTextAsync(indexTemporary, index.ToJsonString(),
                    new UTF8Encoding(false), ct);
            }
        }
        foreach (var staged in stagedFiles) File.Move(staged.Temporary, staged.Final);
        return manifest;
        }
        catch
        {
            foreach (var staged in stagedFiles)
            {
                try { File.Delete(staged.Temporary); } catch { }
                try { File.Delete(staged.Final); } catch { }
            }
            throw;
        }
    }

    // Pure data-path for tests. Source payload is never passed to the model.
    public static Task<JsonObject> SaveSnapshotForTestAsync(JsonObject payload, string mode, string pattern,
        string folder, int maxMatches, CancellationToken ct = default, string? captureIndexRoot = null)
        => SaveSnapshotAsync(payload, mode, pattern, folder, maxMatches, ct, captureIndexRoot);
}
