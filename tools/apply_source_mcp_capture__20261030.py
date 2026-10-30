from pathlib import Path
import shutil
root=Path(r"C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill")
backup=root/"backups"/"source_capture_20261030"
def patch(rel, changes):
    p=root/rel
    t=p.read_text(encoding="utf-8-sig")
    if "SOURCE_MCP_CAPTURE_20261030" in t: print("ALREADY",rel);return
    shutil.copy2(p,backup/p.name)
    for old,new in changes:
        n=t.count(old)
        if n!=1: raise RuntimeError(rel+" expected one got "+str(n)+" "+old[:95])
        t=t.replace(old,new,1)
    p.write_text("// SOURCE_MCP_CAPTURE_20261030\n"+t,encoding="utf-8")
    print("PATCHED",rel)

iface="src/OpenFill.Core/Mcp/IRunHost__v0.10__2026-10-12_1810.cs"
patch(iface,[
    ('    /// <summary>Stops one task (or every task when the key is null).</summary>',
     '''    /// <summary>Captures one existing, physically open tab without model inference or navigation.</summary>
    Task<JsonObject> CapturePageAsync(string taskKey, string? expectedUrl, string mode,
        string selector, string regex, string? directory, int maxMatches, CancellationToken ct)
        => Task.FromResult(new JsonObject { ["status"] = "error", ["message"] = "Direct page capture not supported by this host." });

    /// <summary>Stops one task (or every task when the key is null).</summary>''')
])
app="src/OpenFill.Core/Hosting/AppHost__v0.23__2026-10-29_0807.cs"
method='''    /// <summary>Direct model-free snapshot of an existing tab: no new task, no navigation.</summary>
    public async Task<JsonObject> CapturePageAsync(string taskKey, string? expectedUrl, string mode,
        string selector, string regex, string? directory, int maxMatches, CancellationToken ct)
    {
        var tab = Tabs?.Find(taskKey);
        if (tab is null || Tabs?.IsTabPresent(taskKey) != true || Tabs.ConnectionOf(tab.TabId) is not { } conn)
            return new JsonObject { ["status"] = "error", ["message"] = "The requested physical task tab is not open." };
        var browser = new Browser.BrowserController(new CdpSession(conn), Session.Log);
        try
        {
            await browser.InitAsync(ct);
            var actual = await browser.CurrentUrlAsync(ct);
            if (!string.IsNullOrWhiteSpace(expectedUrl) &&
                !string.Equals(actual.TrimEnd('/'), expectedUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
                return new JsonObject { ["status"] = "error", ["message"] = "URL_MISMATCH. Page was not changed.", ["current_url"] = actual };
            var service = new PageCaptureService(browser, Paths.SharedFor(Config.SharedFolder),
                new CaptureRecipeStore(Path.Combine(Paths.Notes, "recipes")));
            return await service.CaptureAsync(mode, selector, regex, directory, maxMatches, ct);
        }
        catch (Exception ex) { return new JsonObject { ["status"] = "error", ["message"] = "Direct capture failed: " + ex.Message }; }
        finally { browser.Detach(); }
    }

'''
patch(app,[
    ("    public async Task<string?> CaptureScreenshotAsync(string? taskKey, CancellationToken ct)",method+"    public async Task<string?> CaptureScreenshotAsync(string? taskKey, CancellationToken ct)")
])
manager="src/OpenFill.Core/Mcp/McpTaskManager__v0.21__2026-10-28_2216.cs"
new_method='''    /// <summary>Direct C# snapshot. Reuses a known task card and verifies both caller identity fields.</summary>
    public async Task<JsonObject> PageSnapshotAsync(string? taskId, string? requester, string? chat,
        string? expectedUrl, string? mode, string? selector, string? regex, string? directory, int maxMatches, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(taskId)) return Error("task_id is required for direct page capture.");
        var who = CleanWho(requester);
        var chatName = CleanWho(chat);
        if (who is null || chatName is null) return Error("requester and chat are required.");
        var rec = Find(taskId, out _);
        if (rec is null) return Error("No such task.");
        if (!string.Equals(rec.Requester, who + " · " + chatName, StringComparison.Ordinal))
            return Error("Requester/chat identity does not match the owner of this task.");
        return await _host.CapturePageAsync(rec.Id, expectedUrl, mode ?? "all",
            selector ?? "body", regex ?? "", directory, Math.Clamp(maxMatches, 1, 500), ct);
    }

'''
patch(manager,[
    ("    /// <summary>Screenshot of the browser tab of one task", new_method+"    /// <summary>Screenshot of the browser tab of one task")
])
server="src/OpenFill.Core/Mcp/McpServer__v0.24__2026-10-28_1648.cs"
schema='''            ToolDef("openfill_page_snapshot",
"""
Captures the currently loaded DOM of one existing OpenFill task tab without invoking Luna and without navigating.
Uses the exact task_id and caller identity (requester + chat) from the original task.
The task tab must still be physically open. Optional url must match the current URL or nothing is read.
The full text/element list/HTML is written directly by C# to files, never through WebView2 downloads.
Returns compact file paths, byte counts, SHA-256, regex-match count and short preview.
Supported modes: text, elements, html, all. Optional CSS selector and case-insensitive regex.
This is a current loaded-DOM snapshot, not a complete remote-site crawl.
""",
                new JsonObject {
                    ["task_id"] = Prop("string","Exact original task id with physically open tab"),
                    ["requester"] = Prop("string","Same requester as original OpenFill task"),
                    ["chat"] = Prop("string","Same chat as original OpenFill task"),
                    ["url"] = Prop("string","Optional current URL assertion; mismatch aborts"),
                    ["mode"] = Prop("string","text, elements, html, all; default all"),
                    ["selector"] = Prop("string","CSS selector; default body"),
                    ["regex"] = Prop("string","Optional line filter for text; source retained"),
                    ["output_folder"] = Prop("string","Optional shared-relative or absolute Windows directory"),
                    ["max_matches"] = Prop("integer","Maximum filtered matches stored; default 50")
                },
                new[] { "task_id","requester","chat" }, idempotent:false, readOnly:true),
'''
patch(server,[
    ('            ToolDef("openfill_screenshot",',schema+'            ToolDef("openfill_screenshot",'),
    ('                "openfill_screenshot" => await ScreenshotResultAsync(Str(args, "task_id")),',
     '''                "openfill_page_snapshot" => await _tasks.PageSnapshotAsync(
                    Str(args, "task_id"), Str(args, "requester"), Str(args, "chat"), Str(args, "url"),
                    Str(args, "mode"), Str(args, "selector"), Str(args, "regex"),
                    Str(args, "output_folder"), Int(args, "max_matches", 50), _cts.Token),
                "openfill_screenshot" => await ScreenshotResultAsync(Str(args, "task_id")),''')
])
print("MCP_CAPTURE_SOURCE_PATCH_DONE")
