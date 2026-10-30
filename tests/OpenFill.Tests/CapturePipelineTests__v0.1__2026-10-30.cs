// CAPTURE_TASK_OUTCOME_20261030
// SOURCE capture-filter-recipe-terminal tests, 2026-10-30
using System.Text.Json.Nodes;
using OpenFill.Cli;
using OpenFill.Core.Agent;
using OpenFill.Core.Browser;
using OpenFill.Core.Cdp;
using OpenFill.Core.Config;
using OpenFill.Core.Logging;

namespace OpenFill.Tests;

public static class CapturePipelineTests
{
    public static async Task RunAsync()
    {
        await T.Section("Capture pipeline: source, filtered files, hashes and metadata", async () =>
        {
            var temp = Path.Combine(Path.GetTempPath(), "openfill_capture_unit_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(temp);
            try
            {
                var data = new JsonObject {
                    ["url"] = "https://example.com/messages",
                    ["capturedUtc"] = DateTime.UtcNow.ToString("O"),
                    ["text"] = "menu\nImportant: speaker one\nfooter\nImportant: speaker two\n",
                    ["html"] = "<main><p>Important</p><svg><path></path></svg></main>",
                    ["totalElements"] = 1, ["elementsComplete"] = true,
                    ["elements"] = new JsonArray(new JsonObject { ["tag"] = "a", ["label"] = "Speaker" })
                };
                var manifest = await PageCaptureService.SaveSnapshotForTestAsync(data, "all", "important", temp, 5);
                var files = manifest["files"]!.AsArray();
                T.Eq("all sources plus filtered file", 4, files.Count);
                var sourcePath = files[0]!["path"]!.GetValue<string>();
                var filterPath = files[1]!["path"]!.GetValue<string>();
                T.Contains("raw file kept complete", File.ReadAllText(sourcePath), "footer");
                T.NotContains("filtered removes irrelevant footer", File.ReadAllText(filterPath), "footer");
                T.Eq("filtered match count", 2, manifest["matches"]!.GetValue<int>());
                T.Check("model sees only short preview", manifest["preview"]!.GetValue<string>().Length <= 400);
                T.Check("each file has SHA256", files.All(x => x?["sha256"]?.ToString().Length == 64));
                T.Eq("element extraction completeness", true, manifest["elements_complete"]!.GetValue<bool>());
                T.Eq("element count included", 1, manifest["items"]!.GetValue<int>());
            }
            finally { Directory.Delete(temp, true); }
        });
        // BROWSER_NOTIFICATION_TEST_20261030
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
        await T.Section("Authentication-first: signed-out LinkedIn never counts as export", () =>
        {
            var redirect = new JsonObject { ["url"]="https://www.linkedin.com/login/?session_redirect=https%3A%2F%2Fwww.linkedin.com%2Fmessaging%2F",
                ["loginForm"]=true, ["authenticatedMarker"]=false, ["captureError"]="SELECTOR_NOT_FOUND" };
            var refusal = PageAuthenticationGate.Check(redirect, expectedUrl:"https://www.linkedin.com/messaging/");
            T.Eq("login redirect reports LOGIN_FIRST", "authentication_required", refusal["status"]!.ToString());
            T.Eq("login redirect exact code", "LOGIN_FIRST", refusal["code"]!.ToString());
            T.Check("login screen not a successful export", refusal["files"] is null);
            var accepted = new JsonObject { ["url"]="https://www.linkedin.com/messaging/",
                ["loginForm"]=false, ["authenticatedMarker"]=true };
            var ready = PageAuthenticationGate.Check(accepted, expectedUrl:"https://www.linkedin.com/messaging/");
            T.Eq("verified authenticated LinkedIn permits capture", "ready", ready["status"]!.ToString());
            var unverified = PageAuthenticationGate.Check(new JsonObject { ["url"]="https://www.linkedin.com/messaging/",
                ["loginForm"]=false, ["authenticatedMarker"]=false });
            T.Eq("missing proof is not success", "authentication_unverified", unverified["status"]!.ToString());
            var unrelated = PageAuthenticationGate.Check(new JsonObject { ["url"]="https://example.org/landing",
                ["loginForm"]=false, ["authenticatedMarker"]=false });
            T.Eq("public page does not require login", "ready", unrelated["status"]!.ToString());
            return Task.CompletedTask;
        });
        await T.Section("Capture recipes: verified methods, scope and reuse", () =>
        {
            var temp = Path.Combine(Path.GetTempPath(), "openfill_capture_recipes_" + Guid.NewGuid().ToString("N")[..8]);
            try
            {
                var store = new CaptureRecipeStore(temp);
                store.Record("www.example.org", "/feed", "text", "main", "speaker|invite", 200);
                store.Record("www.example.org", "/feed", "text", "main", "speaker|invite", 120);
                store.Record("www.example.org", "/about", "html", "body", "", 40);
                var best = new CaptureRecipeStore(temp).Best("www.example.org", "/feed");
                T.Eq("reused recipe count", 2, best!.Successes);
                T.Eq("reused correct selector", "main", best.Selector);
                T.Eq("reused correct filter", "speaker|invite", best.Pattern);
                store.Record("www.example.org","/feed","text","article","hidden",80);
                store.Record("www.example.org","/feed","text","article","hidden",80);
                store.Record("www.example.org","/feed","text","article","hidden",80);
                store.RecordTaskOutcome("www.example.org","/feed","text","main","speaker|invite","success");
                store.RecordTaskOutcome("www.example.org","/feed","text","article","hidden","failed");
                store.RecordTaskOutcome("www.example.org","/feed","text","article","hidden","partial");
                var verdictBest = new CaptureRecipeStore(temp).Best("www.example.org", "/feed");
                T.Eq("whole-task success outranks more technical captures", "main", verdictBest!.Selector);
                T.Eq("whole-task success persisted", 1, verdictBest.TaskSuccesses);
                T.Contains("recipe exposes actual task outcomes", store.Context("www.example.org","/feed"), "task_ok=1");
                T.Check("different host isolated", store.Best("other.example.org", "/feed") is null);
                T.Contains("model recipe context compact", store.Context("www.example.org", "/feed"), "technical_ok=2");
            }
            finally { try { Directory.Delete(temp, true); } catch { } }
            return Task.CompletedTask;
        });
        await T.Section("FailedCallGuard: third identical failure blocked, successful navigation resets", () =>
        {
            var g = new FailedCallGuard();
            var args = new JsonObject { ["name"] = "feed_ui.txt", ["from_download"] = "feed_ui.txt" };
            T.Check("first attempt allowed", g.Before("shared_save", args) is null);
            g.After("shared_save", args, false, "There is no such file in the downloads folder.");
            T.Check("second attempt allowed", g.Before("shared_save", args) is null);
            g.After("shared_save", args, false, "There is no such file in the downloads folder.");
            T.Contains("third attempt blocked", g.Before("shared_save", args)!, "BLOCKED_REPEAT");
            T.Check("different args allowed", g.Before("shared_save", new JsonObject { ["name"] = "other.txt" }) is null);
            g.After("page_capture", new JsonObject(), true, "saved");
            T.Check("fresh capture resets failure", g.Before("shared_save", args) is null);
            return Task.CompletedTask;
        });
        await T.Section("Terminal: exit code, stdout/stderr and full output file", async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "openfill_terminal_test_" + Guid.NewGuid().ToString("N")[..8]);
            try
            {
                var ok = await TerminalProcessService.ExecuteAsync("powershell", "Write-Output 'alpha'; 1..100 | ForEach-Object { 'row-' + $_ }", null, dir, 15);
                T.Eq("terminal exit success", "ok", ok["status"]!.ToString());
                T.Eq("terminal exit code 0", 0, ok["exit_code"]!.GetValue<int>());
                T.Check("model stdout preview bounded", ok["stdout_preview"]!.ToString().Length <= 400);
                T.Contains("complete stdout stored", File.ReadAllText(ok["stdout_path"]!.ToString()), "row-100");
                var bad = await TerminalProcessService.ExecuteAsync("powershell", "Write-Output 'before'; Write-Error 'expected-diagnostic'; exit 7", null, dir, 15);
                T.Eq("terminal error nonzero exit", 7, bad["exit_code"]!.GetValue<int>());
                T.Contains("stderr accessible", File.ReadAllText(bad["stderr_path"]!.ToString()), "expected-diagnostic");
                var py = await TerminalProcessService.ExecuteAsync("python", "print('python-ok')", null, dir, 15);
                T.Contains("python CLI works", py["stdout_preview"]!.ToString(), "python-ok");
                var bash = await TerminalProcessService.ExecuteAsync("bash", "printf 'alpha\\nbeta\\n' | grep beta", null, dir, 15);
                T.Eq("Git Bash pipeline exit 0", 0, bash["exit_code"]!.GetValue<int>());
                T.Contains("GNU grep via Git Bash", bash["stdout_preview"]!.ToString(), "beta");
                T.Eq("stdout truncation is explicitly reported", false, bash["stdout_truncated"]!.GetValue<bool>());
            }
            finally { try { Directory.Delete(dir, true); } catch { } }
        });
    }

    public static async Task RunBrowserAsync(string testsite, Func<string, string> page)
    {
        await T.Section("PageCaptureService real CDP: one DOM -> files, filter and remembered recipe", async () =>
        {
            using var site = new StaticSite(testsite);
            using var chromium = new ChromiumLauncher();
            var ws = await chromium.LaunchAsync(
                Path.Combine(Path.GetTempPath(), "openfill_capture_browser_" + Guid.NewGuid().ToString("N")[..8]), headless: true);
            var conn = new WebSocketCdpConnection();
            await conn.ConnectAsync(ws);
            var root = Path.Combine(Path.GetTempPath(), "openfill_capture_e2e_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(root);
            try
            {
                using var log = new EventLog(Path.Combine(root, "logs"), Path.Combine(root, "runs"));
                var cdp = await CdpBootstrap.AttachPageAsync(conn, "about:blank");
                var browser = new BrowserController(cdp, log);
                await browser.InitAsync();
                await browser.NavigateAsync(site.BaseUrl + page("xing"));
                var store = new CaptureRecipeStore(Path.Combine(root, "notes", "recipes"));
                var capture = new PageCaptureService(browser, Path.Combine(root, "shared"), store);
                var result = await capture.CaptureAsync("all", "body", "password|email|signup", null, 25, CancellationToken.None);
                // The same actual CDP page is rejected without positive login evidence,
                // then accepted after a verified session marker appears.
                var protectedFolder = Path.Combine(root,"after-login");
                var refused = await capture.CaptureAsync("all", "main", "email", protectedFolder, 20,
                    CancellationToken.None, requireAuthenticated:true, authenticatedSelector:"html[data-qa-auth='yes']");
                T.Eq("unverified login blocks physical DOM export", "authentication_unverified", refused["status"]!.ToString());
                T.Check("no output directory created before authentication", !Directory.Exists(protectedFolder));
                var loggedIn = await browser.RunJsAsync("(document.documentElement.setAttribute('data-qa-auth','yes'), true)");
                T.Check("test page simulated successful login", loggedIn?["ok"]?.GetValue<bool>() == true);
                var afterLogin = await capture.CaptureAsync("all", "body", "email", protectedFolder, 20,
                    CancellationToken.None, requireAuthenticated:true, authenticatedSelector:"html[data-qa-auth='yes']");
                T.Eq("verified login allows physical capture", "saved", afterLogin["status"]!.ToString());
                T.Eq("verified physical capture four files", 4, afterLogin["files"]!.AsArray().Count);
                T.Eq("CDP capture status saved", "saved", result["status"]!.ToString());
                T.Eq("CDP three sources and filter", 4, result["files"]!.AsArray().Count);
                T.Check("CDP capture file exists", File.Exists(result["files"]![0]!["path"]!.ToString()));
                T.Check("CDP selector recipe persisted", store.Get(new Uri(result["url"]!.ToString()).Host).Count > 0);
                T.Check("CDP model preview bounded", result["preview"]!.ToString().Length <= 400);
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
                registry.RecordCaptureTaskOutcome("success");
                var lastUri = new Uri(result["url"]!.ToString());
                var savedBest = new CaptureRecipeStore(Path.Combine(paths.Notes,"recipes")).Best(lastUri.Host,lastUri.AbsolutePath);
                T.Eq("real ToolRegistry records full task verdict", 1, savedBest!.TaskSuccesses);
                T.Check("Luna handler response compact", compact.Output.Length < 2500);
                var runJsTool = toolList.Single(t => t.Name == "run_js");
                var js = await runJsTool.Handler(new JsonObject {
                    ["code"] = "const amount=2+5; return amount*3;", ["mode"] = "statements", ["save_to"] = "qa_js_result.txt"
                }, CancellationToken.None);
                T.Check("run_js statements returns success", js.Ok);
                T.Eq("run_js save_to exact value", "21", File.ReadAllText(Path.Combine(root,"shared","qa_js_result.txt")));
                T.Check("run_js value not returned in full", js.Output.Length < 500);
                var raw = await runJsTool.Handler(new JsonObject { ["code"] = "'legacy expression ok'" }, CancellationToken.None);
                T.Contains("run_js legacy expression preserved", raw.Output, "legacy expression ok");
            }
            finally { await conn.DisposeAsync(); try { Directory.Delete(root, true); } catch { } }
        });
    }
}



