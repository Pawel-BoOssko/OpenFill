// OpenFill - Metadata: wersja 0.1, data 2026-10-05 19:05
using System.Text.Json.Nodes;
using OpenFill.Core.Agent;
using OpenFill.Core.Browser;
using OpenFill.Core.Cdp;
using OpenFill.Core.Config;
using OpenFill.Core.Logging;

namespace OpenFill.Tests;

public static class SharedTests
{
    private static JsonObject Call(string id, string name, JsonObject args)
        => new() { ["type"] = "function_call", ["id"] = "f" + id, ["call_id"] = "c" + id, ["name"] = name, ["arguments"] = args.ToJsonString() };

    private static JsonObject Resp(params JsonObject[] calls) => new() { ["id"] = "r", ["output"] = new JsonArray(calls.Select(c => (JsonNode)c).ToArray()) };

    private static string Out(ScriptedModel model, string callId)
    {
        foreach (var n in model.Requests[1]["input"]!.AsArray())
            if (n is JsonObject o && o["call_id"]?.GetValue<string>() == callId && o["type"]?.GetValue<string>() == "function_call_output")
                return o["output"]!.GetValue<string>();
        return "";
    }

    public static async Task RunAsync()
    {
        await T.Section("Shared folder: list, save, read, path safety", async () =>
        {
            var root = Path.Combine(Path.GetTempPath(), "openfill_test_shared_" + Guid.NewGuid().ToString("N")[..6]);
            var shared = Path.Combine(root, "shared");
            var downloads = Path.Combine(root, "downloads");
            Directory.CreateDirectory(downloads);
            File.WriteAllText(Path.Combine(downloads, "cv.txt"), "downloaded text");
            var paths = new AppPaths("test", Path.Combine(root, "data"));
            paths.EnsureCreated();
            using var log = new EventLog(paths.Logs, paths.Runs);
            var browser = new BrowserController(new CdpSession(new NullCdpConnection()), log);
            var config = new AppConfig();
            var registry = new ToolRegistry(browser, log, config, new OutputLimiter(paths.Overflow, 20000), new SiteNotesStore(paths.Notes, log),
                new HeadlessInteraction(), new GapRecorder(paths.GapsFile, log), null, new SharedFiles(shared, downloads));

            var first = Resp(
                Call("1", "shared_list", new JsonObject()),
                Call("2", "shared_save", new JsonObject { ["name"] = "offers.csv", ["text"] = "a;b\n1;2" }),
                Call("3", "shared_save", new JsonObject { ["name"] = "offers.csv", ["text"] = "again" }),
                Call("4", "shared_save", new JsonObject { ["name"] = "..\\evil.txt", ["text"] = "x" }),
                Call("5", "shared_save", new JsonObject { ["name"] = "sub/cv-copy.txt", ["from_download"] = "cv.txt" }),
                Call("6", "shared_read", new JsonObject { ["name"] = "offers.csv" }),
                Call("7", "shared_save", new JsonObject { ["name"] = "offers.csv", ["text"] = "new", ["overwrite"] = true }),
                Call("8", "shared_list", new JsonObject()),
                Call("9", "shared_read", new JsonObject { ["name"] = "..\\..\\data\\config.json" }),
                Call("10", "shared_save", new JsonObject { ["name"] = "x.txt" }));
            var finish = Resp(Call("11", "finish", new JsonObject { ["status"] = "success", ["summary"] = "ok" }));
            var model = new ScriptedModel(first, finish);
            var result = await new AgentRunner(model, registry, config, log).RunAsync("use the shared folder");
            T.Eq("task finishes", "success", result.Status);

            T.Contains("empty folder listed with its path", Out(model, "c1"), shared);
            T.Contains("text saved", Out(model, "c2"), "Saved offers.csv");
            T.Eq("file was replaced by the overwrite call", "new", File.ReadAllText(Path.Combine(shared, "offers.csv")));
            T.Contains("no silent overwrite", Out(model, "c3"), "already exists");
            T.Contains("path trick refused", Out(model, "c4"), "outside the shared folder");
            T.Check("nothing written outside", !File.Exists(Path.Combine(root, "evil.txt")));
            T.Check("download copied into a subfolder", File.ReadAllText(Path.Combine(shared, "sub", "cv-copy.txt")) == "downloaded text");
            T.Contains("file read back as data", Out(model, "c6"), "a;b");
            T.Contains("overwrite on request", Out(model, "c7"), "Saved");
            T.Contains("list shows files", Out(model, "c8"), "offers.csv");
            T.Contains("list shows the subfolder file", Out(model, "c8"), "cv-copy.txt");
            T.Contains("read outside refused", Out(model, "c9"), "outside the shared folder");
            T.Contains("text or download required", Out(model, "c10"), "exactly one");
            // SHARED_OVERFLOW_REPEAT_TEST_20261030
            var smallLimiter = new OutputLimiter(paths.Overflow, 80);
            var (limited, overflowPath) = smallLimiter.Apply(new string('Q', 2500), "qa_run_js");
            T.Check("overflow file created", overflowPath != null && File.Exists(overflowPath));
            var saver = registry.Build().Single(t => t.Name == "shared_save");
            var restored = await saver.Handler(new JsonObject {
                ["name"] = "overflow_copied.txt", ["from_overflow"] = overflowPath
            }, CancellationToken.None);
            T.Check("from_overflow saved", restored.Ok);
            T.Eq("from_overflow data identical", 2500, File.ReadAllText(Path.Combine(shared,"overflow_copied.txt")).Length);
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
            T.Contains("third identical tool error refused inside agent", Out(loopModel,"cc"), "BLOCKED_REPEAT");
        });
    }
}

