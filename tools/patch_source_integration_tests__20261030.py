from pathlib import Path
r=Path(r"C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill")
p=r/"tests/OpenFill.Tests/CapturePipelineTests__v0.1__2026-10-30.cs"
s=p.read_text(encoding="utf-8-sig")
if "BROWSER_NOTIFICATION_TEST_20261030" not in s:
    anchor='''        await T.Section("Capture recipes: verified methods, scope and reuse", () =>'''
    suite='''        // BROWSER_NOTIFICATION_TEST_20261030
        await T.Section("WebView2 notice routing: per-tab, drain after read", () =>
        {
            OpenFill.Core.Browser.BrowserNotificationBus.Post("qa-tab", "MultipleAutomaticDownloads allowed");
            OpenFill.Core.Browser.BrowserNotificationBus.Post("other-tab", "Unrelated notice");
            var found = OpenFill.Core.Browser.BrowserNotificationBus.Take("qa-tab");
            T.Eq("permission delivered to matching tab", 1, found.Count);
            T.Contains("permission details preserved", found[0], "MultipleAutomaticDownloads");
            T.Eq("permission is not repeated", 0, OpenFill.Core.Browser.BrowserNotificationBus.Take("qa-tab").Count);
            T.Eq("other tab remains isolated", 1, OpenFill.Core.Browser.BrowserNotificationBus.Take("other-tab").Count);
            return Task.CompletedTask;
        });
        await T.Section("Capture recipes: verified methods, scope and reuse", () =>'''
    if s.count(anchor)!=1:raise RuntimeError("unit anchor error")
    s=s.replace(anchor,suite,1)
    anchor='''                T.Check("CDP model preview bounded", result["preview"]!.ToString().Length <= 400);'''
    new='''                T.Check("CDP model preview bounded", result["preview"]!.ToString().Length <= 400);
                // Luna's actual tool handler uses the same service and stays compact.
                var paths = new AppPaths("capture-test", root);
                paths.EnsureCreated();
                var registry = new ToolRegistry(browser, log, new AppConfig(), new OutputLimiter(paths.Overflow, 1000),
                    new SiteNotesStore(paths.Notes, log), new HeadlessInteraction(), new GapRecorder(paths.GapsFile, log),
                    files: new SharedFiles(Path.Combine(root, "shared"), Path.Combine(root, "downloads")));
                var toolList = registry.Build();
                T.Check("Luna page_capture exists", toolList.Any(t => t.Name == "page_capture"));
                T.Check("Luna terminal_exec exists", toolList.Any(t => t.Name == "terminal_exec"));
                var captureTool = toolList.Single(t => t.Name == "page_capture");
                var compact = await captureTool.Handler(new JsonObject {
                    ["mode"] = "text", ["selector"] = "body", ["regex"] = "password|email", ["max_matches"] = 10,
                    ["use_best"] = false
                }, CancellationToken.None);
                T.Check("Luna handler captures successfully", compact.Ok);
                T.Check("Luna handler response compact", compact.Output.Length < 2500);
                var runJsTool = toolList.Single(t => t.Name == "run_js");
                var js = await runJsTool.Handler(new JsonObject {
                    ["code"] = "const amount=2+5; return amount*3;", ["mode"] = "statements", ["save_to"] = "qa_js_result.txt"
                }, CancellationToken.None);
                T.Check("run_js statements returns success", js.Ok);
                T.Eq("run_js save_to exact value", "21", File.ReadAllText(Path.Combine(root,"shared","qa_js_result.txt")));
                T.Check("run_js value not returned in full", js.Output.Length < 500);
                var raw = await runJsTool.Handler(new JsonObject { ["code"] = "'legacy expression ok'" }, CancellationToken.None);
                T.Contains("run_js legacy expression preserved", raw.Output, "legacy expression ok");'''
    if s.count(anchor)!=1:raise RuntimeError("browser anchor error")
    s=s.replace(anchor,new,1)
    p.write_text(s,encoding="utf-8")
    print("CAPTURE_BROWSER_TOOL_TESTS_PATCHED")

p=r/"tests/OpenFill.Tests/SharedTests__v0.1__2026-10-05_1905.cs"
s=p.read_text(encoding="utf-8-sig")
if "SHARED_OVERFLOW_REPEAT_TEST_20261030" not in s:
    anchor='''            T.Contains("text or download required", Out(model, "c10"), "exactly one");'''
    new='''            T.Contains("text or download required", Out(model, "c10"), "exactly one");
            // SHARED_OVERFLOW_REPEAT_TEST_20261030
            var smallLimiter = new OutputLimiter(paths.Overflow, 80);
            var (limited, overflowPath) = smallLimiter.Apply(new string('Q', 1000), "qa_run_js");
            T.Check("overflow file created", overflowPath != null && File.Exists(overflowPath));
            var saver = registry.Build().Single(t => t.Name == "shared_save");
            var restored = await saver.Handler(new JsonObject {
                ["name"] = "overflow_copied.txt", ["from_overflow"] = overflowPath
            }, CancellationToken.None);
            T.Check("from_overflow saved", restored.Ok);
            T.Eq("from_overflow data identical", 1000, File.ReadAllText(Path.Combine(shared,"overflow_copied.txt")).Length);
            var rejected = await saver.Handler(new JsonObject {
                ["name"] = "not_allowed.txt", ["from_overflow"] = Path.Combine(downloads,"cv.txt")
            }, CancellationToken.None);
            T.Check("from_overflow path outside correct folder refused", !rejected.Ok);
            var loopModel = new ScriptedModel(
                Resp(
                    Call("a", "shared_save", new JsonObject { ["name"]="ghost.txt", ["from_download"]="does-not-exist.txt" }),
                    Call("b", "shared_save", new JsonObject { ["name"]="ghost.txt", ["from_download"]="does-not-exist.txt" }),
                    Call("c", "shared_save", new JsonObject { ["name"]="ghost.txt", ["from_download"]="does-not-exist.txt" })
                ),
                Resp(Call("d", "finish", new JsonObject { ["status"]="partial", ["summary"]="blocked" }))
            );
            var loopRegistry = new ToolRegistry(browser,log,config,new OutputLimiter(paths.Overflow,1000),
                new SiteNotesStore(paths.Notes,log),new HeadlessInteraction(),new GapRecorder(paths.GapsFile,log),
                files:new SharedFiles(shared,downloads));
            await new AgentRunner(loopModel,loopRegistry,config,log).RunAsync("test duplicate file failure");
            T.Contains("third identical tool error refused inside agent", Out(loopModel,"cc"), "BLOCKED_REPEAT");'''
    if s.count(anchor)!=1:raise RuntimeError("shared anchor error")
    s=s.replace(anchor,new,1)
    p.write_text(s,encoding="utf-8")
    print("SHARED_OVERFLOW_AND_LOOP_TESTS_PATCHED")
