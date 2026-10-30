from pathlib import Path
import shutil
r=Path(r"C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill")
backup=r/"backups"/"auth_gate_source_20261030"
p=r/"src/OpenFill.Core/Hosting/AppHost__v0.23__2026-10-29_0807.cs"
t=p.read_text(encoding="utf-8-sig")
if "AUTH_BEFORE_CAPTURE_20261030" not in t:
    shutil.copy2(p,backup/p.name)
    a='''            if (!string.IsNullOrWhiteSpace(expectedUrl) &&
                !string.Equals(actual.TrimEnd('/'), expectedUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
                return new JsonObject { ["status"] = "error", ["message"] = "URL_MISMATCH. Page was not changed.", ["current_url"] = actual };
            var service = new PageCaptureService(browser, Paths.SharedFor(Config.SharedFolder),'''
    b='''            // The capture service classifies auth redirects before URL mismatch.
            var service = new PageCaptureService(browser, Paths.SharedFor(Config.SharedFolder),'''
    if t.count(a)!=1: raise RuntimeError("APP_EXPECTED_URL_ANCHOR_COUNT="+str(t.count(a)))
    t=t.replace(a,b,1)
    a='return await service.CaptureAsync(mode, selector, regex, directory, maxMatches, ct);'
    b='return await service.CaptureAsync(mode, selector, regex, directory, maxMatches, ct, expectedUrl: expectedUrl);'
    if t.count(a)!=1:raise RuntimeError("APP_CAPTURE_CALL_ANCHOR_COUNT="+str(t.count(a)))
    t=t.replace(a,b,1)
    a='but you are still logged in.'
    b='authentication may have expired; verify the logged-in session before accessing protected data.'
    if t.count(a)!=1:raise RuntimeError("APP_FALSE_LOGIN_ANCHOR_COUNT="+str(t.count(a)))
    t=t.replace(a,b,1)
    p.write_text("// AUTH_BEFORE_CAPTURE_20261030\n"+t,encoding="utf-8")
    print("APP_AUTH_PATCHED")
p=r/"src/OpenFill.Core/Mcp/McpServer__v0.24__2026-10-28_1648.cs"
t=p.read_text(encoding="utf-8-sig")
if "AUTH_BEFORE_CAPTURE_20261030" not in t:
    shutil.copy2(p,backup/p.name)
    a='Captures the currently loaded DOM of one existing OpenFill task tab without invoking Luna and without navigating.'
    b=a+'\nSign in FIRST on a protected page, then verify the authenticated state before capture. Login pages return authentication_required and no files. A copied browser profile is not proof of a live session.'
    if t.count(a)!=1:raise RuntimeError("MCP_DESC_COUNT="+str(t.count(a)))
    t=t.replace(a,b,1)
    p.write_text("// AUTH_BEFORE_CAPTURE_20261030\n"+t,encoding="utf-8")
    print("MCP_AUTH_DOC_PATCHED")
