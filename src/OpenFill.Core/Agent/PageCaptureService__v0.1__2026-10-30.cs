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
        string? expectedUrl = null, bool requireAuthenticated = false, string? authenticatedSelector = null, bool allowLoginPage = false)
    {
        mode = string.IsNullOrWhiteSpace(mode) ? "all" : mode.ToLowerInvariant();
        if (mode is not ("text" or "elements" or "html" or "all")) throw new ArgumentException("mode must be text, elements, html or all");
        selector = string.IsNullOrWhiteSpace(selector) ? "body" : selector.Trim();
        pattern = (pattern ?? "").Trim();
        if (selector.Length > 400 || pattern.Length > 400) throw new ArgumentException("Selector or regex is too long.");
        maxMatches = Math.Clamp(maxMatches, 1, 500);
        // Validate a filter before invoking the browser.
        if (pattern.Length > 0) _ = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(300));
        var sw = Stopwatch.StartNew();
        if (!string.IsNullOrWhiteSpace(authenticatedSelector) && authenticatedSelector.Length > 400)
            throw new ArgumentException("authenticatedSelector too long.");
        var css = JsonSerializer.Serialize(selector);
        var authCss = JsonSerializer.Serialize(authenticatedSelector ?? "");
        var js = """
        (() => {
          const sel = __SELECTOR__;
          const root = document.querySelector(sel);
          // Root check is AFTER authentication metadata; login pages may not have the requested selector.
          const mode = "__MODE__";
          const authSel = __AUTH_SELECTOR__;
          const isLinkedIn = location.hostname === "linkedin.com" || location.hostname.endsWith(".linkedin.com");
          const defaultAuthSel = isLinkedIn ? "nav.global-nav,header.global-nav,#global-nav,[data-test-global-nav],.global-nav__me" : "";
          const markerSelector = authSel || defaultAuthSel;
          const authMarker = markerSelector ? !!document.querySelector(markerSelector) : false;
          const signInForm = !!document.querySelector('form[action*="/login"],input[type="password"],input[name="session_key"]');
          const data = { url: location.href, title: document.title, capturedUtc: new Date().toISOString(),
            selector: sel, authenticatedMarker: authMarker, loginForm: signInForm };
          if (!root) return { ...data, captureError: "SELECTOR_NOT_FOUND" };
          if (mode === "all" || mode === "text") data.text = root.innerText || root.textContent || "";
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
        """.Replace("__SELECTOR__", css).Replace("__MODE__", mode).Replace("__AUTH_SELECTOR__", authCss);
        var result = await browser.RunJsAsync(js, ct);
        if (result?["ok"]?.GetValue<bool>() != true)
            throw new InvalidOperationException("WebView2 capture failed: " + result?["error"]?.ToString());
        var payload = result["value"] as JsonObject ?? throw new InvalidOperationException("DOM capture returned no object.");
        // Authentication is checked before selector failure and before writing even one file.
        var auth = PageAuthenticationGate.Check(payload, expectedUrl, requireAuthenticated, allowLoginPage);
        if (auth["status"]?.ToString() != "ready") return auth;
        if (payload["captureError"] is not null) throw new InvalidOperationException(payload["captureError"]!.ToString() + ": " + selector);
        var folder = ResolveOutputFolder(outputFolder);
        var saved = await SaveSnapshotAsync(payload, mode, pattern, folder, maxMatches, ct);
        sw.Stop();
        var uri = Uri.TryCreate(payload["url"]?.ToString(), UriKind.Absolute, out var parsed) ? parsed : null;
        if (uri is not null) recipes.Record(uri.Host, uri.AbsolutePath, mode, selector, pattern, sw.ElapsedMilliseconds);
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

    private static async Task<JsonObject> SaveSnapshotAsync(JsonObject data, string mode, string pattern, string folder, int maxMatches, CancellationToken ct)
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
            var hash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(temporary, ct))).ToLowerInvariant();
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
            ["files"] = files,
            ["preview"] = preview ?? "(file-only capture)",
            ["matches"] = pattern.Length > 0 ? count : null,
            ["items"] = data["totalElements"]?.DeepClone(),
            ["elements_complete"] = data["elementsComplete"]?.DeepClone(),
            ["captured_utc"] = data["capturedUtc"]?.ToString(),
            ["filtered_truncated"] = pattern.Length > 0 && count > maxMatches
        };
        // Publish completed files only after every artifact and hash succeeded.
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
    public static Task<JsonObject> SaveSnapshotForTestAsync(JsonObject payload, string mode, string pattern, string folder, int maxMatches, CancellationToken ct = default)
        => SaveSnapshotAsync(payload, mode, pattern, folder, maxMatches, ct);
}
