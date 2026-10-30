from pathlib import Path
import shutil
root=Path(r"C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill")
backup=root/"backups"/"auth_gate_source_20261030"
backup.mkdir(parents=True,exist_ok=True)
def patch(rel, replacements):
    p=root/rel
    txt=p.read_text(encoding="utf-8-sig")
    if "AUTH_BEFORE_CAPTURE_20261030" in txt:
        print("ALREADY_PATCHED",rel);return
    shutil.copy2(p,backup/p.name)
    for old,new in replacements:
        n=txt.count(old)
        if n!=1:raise RuntimeError(f"Wrong occurrence count {n} for {rel}: {old[:110]}")
        txt=txt.replace(old,new,1)
    p.write_text("// AUTH_BEFORE_CAPTURE_20261030\n"+txt,encoding="utf-8")
    print("PATCHED",rel)

p="src/OpenFill.Core/Agent/PageCaptureService__v0.1__2026-10-30.cs"
patch(p,[
('''public async Task<JsonObject> CaptureAsync(string mode, string selector, string pattern, string? outputFolder, int maxMatches, CancellationToken ct)''',
'''public async Task<JsonObject> CaptureAsync(string mode, string selector, string pattern, string? outputFolder, int maxMatches, CancellationToken ct,
        string? expectedUrl = null, bool requireAuthenticated = false, string? authenticatedSelector = null, bool allowLoginPage = false)'''),
('''        var css = JsonSerializer.Serialize(selector);
        var js = """''',
'''        if (!string.IsNullOrWhiteSpace(authenticatedSelector) && authenticatedSelector.Length > 400)
            throw new ArgumentException("authenticatedSelector too long.");
        var css = JsonSerializer.Serialize(selector);
        var authCss = JsonSerializer.Serialize(authenticatedSelector ?? "");
        var js = """'''),
('''          const mode = "__MODE__";
          const data = { url: location.href, title: document.title, capturedUtc: new Date().toISOString(), selector: sel };''',
'''          const mode = "__MODE__";
          const authSel = __AUTH_SELECTOR__;
          const isLinkedIn = location.hostname === "linkedin.com" || location.hostname.endsWith(".linkedin.com");
          const defaultAuthSel = isLinkedIn ? "nav.global-nav,header.global-nav,#global-nav,[data-test-global-nav],.global-nav__me" : "";
          const markerSelector = authSel || defaultAuthSel;
          const authMarker = markerSelector ? !!document.querySelector(markerSelector) : false;
          const signInForm = !!document.querySelector('form[action*="/login"],input[type="password"],input[name="session_key"]');
          const data = { url: location.href, title: document.title, capturedUtc: new Date().toISOString(),
            selector: sel, authenticatedMarker: authMarker, loginForm: signInForm };'''),
('''.Replace("__SELECTOR__", css).Replace("__MODE__", mode);''',
'''.Replace("__SELECTOR__", css).Replace("__MODE__", mode).Replace("__AUTH_SELECTOR__", authCss);'''),
('''        if (payload["captureError"] is not null) throw new InvalidOperationException(payload["captureError"]!.ToString() + ": " + selector);
        var folder = ResolveOutputFolder(outputFolder);''',
'''        if (payload["captureError"] is not null) throw new InvalidOperationException(payload["captureError"]!.ToString() + ": " + selector);
        // Gate before creating any export directory/file and before recording a recipe success.
        var auth = PageAuthenticationGate.Check(payload, expectedUrl, requireAuthenticated, allowLoginPage);
        if (auth["status"]?.ToString() != "ready") return auth;
        var folder = ResolveOutputFolder(outputFolder);'''),
('''        saved["status"] = "saved";
        return saved;''',
'''        saved["status"] = "saved";
        saved["authentication_verified"] = auth["auth_verified"]?.DeepClone();
        return saved;''')
])

p="src/OpenFill.Core/Agent/ToolRegistry__v0.22__2026-10-29_2314.cs"
patch(p,[
('''            Description = "Preferred for full-page exports, DOM snapshots and large data. Captures currently loaded DOM to files with C# (never browser downloads), optionally filters text using regex; returns ONLY paths, sizes, hashes and a short preview. Can reuse last successful profile with use_best=true. For navigation use navigate first.",''',
'''            Description = "Preferred for bulk DOM exports. Sign in FIRST using existing browser UI if required, verify authenticated session, THEN capture and filter. Never count a login page as a completed export. LinkedIn account pages are checked automatically. require_authenticated=true and authenticated_selector allow explicit verification for other sites. Full data to files only. use_best reuses a verified technical profile.",'''),
('''                ("use_best", Obj(("type","boolean"))))),''',
'''                ("use_best", Obj(("type","boolean"))),
                ("require_authenticated", Obj(("type","boolean"))),
                ("authenticated_selector", Str("Positive CSS selector visible only in a logged-in session")),
                ("allow_login_page", Obj(("type","boolean"))))),'''),
('''                    var value = await _capture.CaptureAsync(mode, selector, regex, args["output_folder"]?.GetValue<string>(),
                        args["max_matches"]?.GetValue<int>() ?? 50, ct);
                    if (Uri.TryCreate(value["url"]?.ToString(), UriKind.Absolute, out var captured))''',
'''                    var value = await _capture.CaptureAsync(mode, selector, regex, args["output_folder"]?.GetValue<string>(),
                        args["max_matches"]?.GetValue<int>() ?? 50, ct,
                        requireAuthenticated: args["require_authenticated"]?.GetValue<bool>() ?? false,
                        authenticatedSelector: args["authenticated_selector"]?.GetValue<string>(),
                        allowLoginPage: args["allow_login_page"]?.GetValue<bool>() ?? false);
                    if (value["status"]?.ToString() != "saved") return new ToolResult(value.ToJsonString(), false);
                    if (Uri.TryCreate(value["url"]?.ToString(), UriKind.Absolute, out var captured))'''),

])

p="src/OpenFill.Core/Agent/AgentRunner__v0.29__2026-10-29_2314.cs"
patch(p,[
('''- For bulk page exports, prefer page_capture: it writes raw DOM/text/elements to files, applies deterministic filters, and returns ONLY a compact manifest. Never create JavaScript Blob downloads for DOM exports.''',
'''- For protected page exports, log in first with existing browser UI/autofill, verify authenticated session, THEN capture. A copied profile or reopened tab does NOT guarantee a login.
- For bulk page exports, prefer page_capture: it writes raw DOM/text/elements to files, applies deterministic filters, and returns ONLY a compact manifest. Never create JavaScript Blob downloads for DOM exports.
- If page_capture returns authentication_required / authentication_unverified: no export occurred. Inspect login via get_page, use normal browser controls to log in, confirm protected page, then retry. If login needs human verification, request it through the existing interaction. Never report success using a login-page snapshot.''')
])
p="src/OpenFill.Core/Hosting/AppHost__v0.23__2026-10-29_0807.cs"
patch(p,[
('''            if (!string.IsNullOrWhiteSpace(expectedUrl) &&
                !string.Equals(actual.TrimEnd('/'), expectedUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
                return new JsonObject { ["status"] = "error", ["message"] = "URL_MISMATCH. Page was not changed.", ["current_url"] = actual };
            var service = new PageCaptureService(browser, Paths.SharedFor(Config.SharedFolder),''',
'''            // The capture service classifies auth redirects BEFORE enforcing URL mismatch
            // so a signed-out session returns LOGIN_FIRST rather than a false successful snapshot.
            var service = new PageCaptureService(browser, Paths.SharedFor(Config.SharedFolder),'''),
('''            return await service.CaptureAsync(mode, selector, regex, directory, maxMatches, ct);''',
'''            return await service.CaptureAsync(mode, selector, regex, directory, maxMatches, ct, expectedUrl: expectedUrl);'''),
('''                ? $"The earlier tab had been closed, so a new tab was opened at {start.Url}; anything typed into forms there is gone, but you are still logged in."''',
'''                ? $"The earlier tab had been closed, so a new tab was opened at {start.Url}; anything typed into forms there is gone. Authentication is NOT guaranteed: verify the login state before accessing protected data."''')
])
p="src/OpenFill.Core/Mcp/McpServer__v0.24__2026-10-28_1648.cs"
patch(p,[
('''Captures the currently loaded DOM of one existing OpenFill task tab without invoking Luna and without navigating.
Uses the exact task_id''',
'''Captures the currently loaded DOM of one existing OpenFill task tab without invoking Luna and without navigating.
For a protected page, SIGN IN FIRST on this physical tab, verify the authenticated session, then capture. Login pages return authentication_required and NO exported data; a copied browser profile is not proof of login.
Uses the exact task_id''')
])
print("AUTHENTICATION_CAPTURE_PATCH_DONE")
