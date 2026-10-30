# OpenFill SOURCE patch, idempotent, backups outside compiled directories.
from pathlib import Path
import shutil, datetime
root=Path(r"C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill")
backup=root/"backups"/"source_capture_20261030"
backup.mkdir(parents=True,exist_ok=True)
def patch(rel, changes):
    p=root/rel
    s=p.read_text(encoding="utf-8-sig")
    if "SOURCE_CAPTURE_PATCH_20261030" in s:
        print("ALREADY_PATCHED",rel)
        return
    shutil.copy2(p,backup/p.name)
    for original,replacement in changes:
        if s.count(original)!=1:
            raise RuntimeError(f"{rel}: expected one occurrence, got {s.count(original)}: {original[:90]}")
        s=s.replace(original,replacement,1)
    p.write_text("// SOURCE_CAPTURE_PATCH_20261030\n"+s,encoding="utf-8")
    print("PATCHED",rel)

notes=r"src/OpenFill.Core/Agent/SiteNotesStore__v0.5__2026-10-14_2259.cs"
patch(notes,[
    ("public sealed class SiteNotesStore(string dir, EventLog log)\n{",
     "public sealed class SiteNotesStore(string dir, EventLog log)\n{\n    public string NotesDirectory => dir;")
])
limiter=r"src/OpenFill.Core/Logging/OutputLimiter__v0.2__2026-10-05_1200.cs"
t=(root/limiter).read_text(encoding="utf-8-sig")
print("LIMITER_HEADER",t[:550])
# Need property in OutputLimiter.
patch(limiter,[
    ("private readonly string _overflowDir;", "private readonly string _overflowDir;\n    public string OverflowDirectory => _overflowDir;")
])
registry=r"src/OpenFill.Core/Agent/ToolRegistry__v0.22__2026-10-29_2314.cs"
page_tools="""
        // A single C# service: WebView2 DOM -> unfiltered source files -> deterministic filters -> compact model result.
        tools.Add(new Tool
        {
            Name = "page_capture",
            Description = "Preferred for full-page exports, DOM snapshots and large data. Captures currently loaded DOM to files with C# (never browser downloads), optionally filters text using regex; returns ONLY paths, sizes, hashes and a short preview. Can reuse last successful profile with use_best=true. For navigation use navigate first.",
            Parameters = Schema(Obj(
                ("mode", Str("text, elements, html, all; default all")),
                ("selector", Str("CSS root selector; default body")),
                ("regex", Str("Optional text pattern, regex, case-insensitive")),
                ("output_folder", Str("Optional relative shared subdirectory or explicit Windows folder")),
                ("max_matches", Obj(("type","integer"))),
                ("use_best", Obj(("type","boolean"))))),
            Handler = async (args, ct) =>
            {
                var mode = args["mode"]?.GetValue<string>() ?? "all";
                var selector = args["selector"]?.GetValue<string>() ?? "body";
                var regex = args["regex"]?.GetValue<string>() ?? "";
                if (args["use_best"]?.GetValue<bool>() == true)
                {
                    var now = await _browser.CurrentUrlAsync(ct);
                    if (Uri.TryCreate(now, UriKind.Absolute, out var uri))
                    {
                        var best = _recipes.Best(uri.Host, uri.AbsolutePath);
                        if (best != null)
                        {
                            if (args["mode"] == null) mode = best.Mode;
                            if (args["selector"] == null) selector = best.Selector;
                            if (args["regex"] == null) regex = best.Pattern;
                        }
                    }
                }
                try
                {
                    var value = await _capture.CaptureAsync(mode, selector, regex, args["output_folder"]?.GetValue<string>(),
                        args["max_matches"]?.GetValue<int>() ?? 50, ct);
                    return ToolResult.Text(value.ToJsonString());
                }
                catch (Exception ex) { return ToolResult.Error("Page capture failed: " + ex.Message); }
            }
        });
        tools.Add(new Tool
        {
            Name = "terminal_exec",
            Description = "Run installed CLI tools on PBOX1 to filter or transform saved files. Supports powershell, python, rg, jq. Full stdout/stderr go to files; only status, exit code and previews reach Luna. Prefer this over writing JS for file analysis.",
            Parameters = Schema(Obj(
                ("shell", Str("powershell, python, rg, jq; default powershell")),
                ("command", Str("PowerShell command, Python code or CLI argument string")),
                ("cwd", Str("Optional absolute working directory")),
                ("timeout_seconds", Obj(("type", "integer")))), "command"),
            Handler = async (args, ct) =>
            {
                try
                {
                    var result = await TerminalProcessService.ExecuteAsync(
                        args["shell"]?.GetValue<string>() ?? "powershell", args["command"]?.GetValue<string>() ?? "",
                        args["cwd"]?.GetValue<string>(), Path.Combine(_files?.SharedDir ?? Path.GetTempPath(), "terminal"),
                        args["timeout_seconds"]?.GetValue<int>() ?? 30, ct);
                    return new ToolResult(result.ToJsonString(), result["status"]?.ToString() == "ok");
                }
                catch (Exception ex) { return ToolResult.Error("Terminal execution failed: " + ex.Message); }
            }
        });
        tools.Add(new Tool
        {
            Name = "capture_recipes",
            Description = "List previously verified capture modes, selectors and regex profiles for the current URL; no full DOM is returned.",
            Parameters = Schema(Obj()),
            Handler = async (args, ct) =>
            {
                var now = await _browser.CurrentUrlAsync(ct);
                return ToolResult.Text(Uri.TryCreate(now, UriKind.Absolute, out var uri)
                    ? _recipes.Context(uri.Host, uri.AbsolutePath, 8) : "(no current web host)");
            }
        });

"""
old_run="""            Parameters = Schema(Obj(("code", Str("JS expression, may be async/Promise"))), "code"),
            Handler = async (args, ct) =>
            {
                var code = args["code"]?.GetValue<string>() ?? "";
                var res = await _browser.RunJsAsync(code, ct);
                var (lim, _) = _limiter.Apply(res?.ToJsonString() ?? "null", "run_js");
                return new ToolResult(lim, res?["ok"]?.GetValue<bool>() != false);
            }
"""
new_run="""            Parameters = Schema(Obj(
                ("code", Str("JS expression or statement body (use mode=statements for semicolon-separated statements)")),
                ("mode", Str("expression (default) or statements")),
                ("save_to", Str("Optional relative shared path or absolute Windows path: write full value directly to file"))), "code"),
            Handler = async (args, ct) =>
            {
                var code = args["code"]?.GetValue<string>() ?? "";
                var mode = args["mode"]?.GetValue<string>() ?? "expression";
                if (mode == "statements") code = "(async()=>{ " + code + " })()";
                else if (mode != "expression") return ToolResult.Error("run_js mode must be expression or statements.");
                var res = await _browser.RunJsAsync(code, ct);
                if (res?["ok"]?.GetValue<bool>() == false) return ToolResult.Error(res["error"]?.ToString() ?? "run_js failed");
                var dest = args["save_to"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(dest))
                {
                    var path = Path.IsPathFullyQualified(dest) ? Path.GetFullPath(dest) : SharedPath(dest, out _);
                    if (path == null) return ToolResult.Error("Invalid save_to path.");
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    var value = res?["value"];
                    var full = value is JsonValue jv && jv.TryGetValue<string>(out var str) ? str : value?.ToJsonString() ?? "null";
                    await File.WriteAllTextAsync(path, full, new UTF8Encoding(false), ct);
                    var size = new FileInfo(path).Length;
                    return ToolResult.Text(new JsonObject { ["path"]=path, ["bytes"]=size, ["preview"]=full[..Math.Min(300,full.Length)] }.ToJsonString());
                }
                var (lim, _) = _limiter.Apply(res?.ToJsonString() ?? "null", "run_js");
                return new ToolResult(lim, res?["ok"]?.GetValue<bool>() != false);
            }
"""
# Shared_save adjustments
shared_desc='Description = "Saves a file in the shared folder so the person or the calling model can pick it up (results, lists, texts, a downloaded document). Give exactly one of: text (written as a UTF-8 file) or from_download (name of a file in the browser\'s downloads folder, copied here). Mention the file name in your finish summary.",'
# verify exact with repr
patch(registry,[
    ("    private readonly SiteNotesStore _notes;", "    private readonly SiteNotesStore _notes;\n    private readonly CaptureRecipeStore _recipes;\n    private readonly PageCaptureService _capture;"),
    ("_files = files; _gate = gate; _verified = verified;", "_files = files; _gate = gate; _verified = verified;\n        _recipes = new CaptureRecipeStore(Path.Combine(notes.NotesDirectory, \"recipes\"));\n        _capture = new PageCaptureService(browser, files?.SharedDir ?? Path.GetTempPath(), _recipes);"),
    ("return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? _notes.ContextFor(uri.Host) : \"\";", "return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? _notes.ContextFor(uri.Host) + \"\\n\" + _recipes.Context(uri.Host, uri.AbsolutePath) : \"\";"),
    ("        tools.Add(new Tool\n        {\n            Name = \"get_page\",", page_tools+"        tools.Add(new Tool\n        {\n            Name = \"get_page\","),
    ("memory = _notes.ContextFor(uri.Host);", "memory = _notes.ContextFor(uri.Host) + \"\\n\" + _recipes.Context(uri.Host, uri.AbsolutePath);"),
    (old_run,new_run),
    ("""                ("from_download", Str("name of a downloaded file to copy instead of giving text")),""", """                ("from_download", Str("name of a downloaded file to copy instead of giving text")),
                ("from_overflow", Str("path returned by OutputLimiter for an oversized result")),"""),
    ("""                var fromDl = args["from_download"]?.GetValue<string>();
                if ((text is null) == string.IsNullOrWhiteSpace(fromDl)) return ToolResult.Error("Give exactly one of text or from_download.");""",
     """                var fromDl = args["from_download"]?.GetValue<string>();
                var fromOverflow = args["from_overflow"]?.GetValue<string>();
                if ((text is not null ? 1 : 0) + (!string.IsNullOrWhiteSpace(fromDl) ? 1 : 0) + (!string.IsNullOrWhiteSpace(fromOverflow) ? 1 : 0) != 1)
                    return ToolResult.Error("Give exactly one of text, from_download or from_overflow.");"""),
    ("""                    var src = Path.GetFullPath(Path.Combine(_files.DownloadsDir, Path.GetFileName(fromDl!)));
                    if (!File.Exists(src)) return ToolResult.Error("There is no such file in the downloads folder.");
                    File.Copy(src, path, true);""",
     """                    var overflow = !string.IsNullOrWhiteSpace(fromOverflow);
                    var src = overflow ? Path.GetFullPath(fromOverflow!) : Path.GetFullPath(Path.Combine(_files.DownloadsDir, Path.GetFileName(fromDl!)));
                    if (overflow)
                    {
                        var root = Path.GetFullPath(_limiter.OverflowDirectory).TrimEnd(Path.DirectorySeparatorChar);
                        if (!src.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                            return ToolResult.Error("from_overflow must point to the actual OutputLimiter overflow folder.");
                    }
                    if (!File.Exists(src)) return ToolResult.Error(overflow ? "There is no such overflow file." : "There is no such file in the downloads folder.");
                    File.Copy(src, path, true);""")
])
runner=r"src/OpenFill.Core/Agent/AgentRunner__v0.29__2026-10-29_2314.cs"
patch(runner,[
    ("    private readonly Queue<string> _recentActions = new();", "    private readonly Queue<string> _recentActions = new();\n    private readonly FailedCallGuard _failedCallGuard = new();"),
    ("""                if (!byName.TryGetValue(name, out var tool))
                    result = ToolResult.Error($"Unknown tool: {name}");""",
     """                var repeatBlock = _failedCallGuard.Before(name, argObj);
                if (repeatBlock is not null) result = ToolResult.Error(repeatBlock);
                else if (!byName.TryGetValue(name, out var tool))
                    result = ToolResult.Error($"Unknown tool: {name}");"""),
    ("""                _log.Write("tool", "result",""",
     """                _failedCallGuard.After(name, argObj, result.Ok, result.Output);
                _log.Write("tool", "result","""),
    ("### Files and page operations\n", """### Files and page operations
- For bulk page exports, prefer page_capture: it writes raw DOM/text/elements to files, applies deterministic filters, and returns ONLY a compact manifest. Never create JavaScript Blob downloads for DOM exports.
- For file queries and transformation, prefer terminal_exec with installed CLI (PowerShell/Python/rg/jq). Output stays in files, only summaries enter your context.
- Before repeated export on a known site, inspect capture_recipes; page_capture use_best=true applies a successfully recorded filter profile.
- run_js is for unusual page interactions; use mode=statements for sequential JS. For large values use save_to, never paste megabytes into the model.
- After the same error twice with unchanged arguments, choose a different method. The runtime will refuse a third identical attempt.
""")
])
print("SOURCE_PATCH_COMPLETE")

