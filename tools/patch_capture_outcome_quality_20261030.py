from pathlib import Path
import shutil
r=Path(r"C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill")
b=r/"backups"/"source_capture_quality_20261030"
b.mkdir(exist_ok=True)
def patch(rel, pairs):
    p=r/rel
    s=p.read_text(encoding="utf-8-sig")
    if "CAPTURE_TASK_OUTCOME_20261030" in s:
        print("ALREADY",rel);return
    shutil.copy2(p,b/p.name)
    for old,new in pairs:
        if s.count(old)!=1:raise RuntimeError(f"{rel} occurrence {s.count(old)} instead of 1: {old[:90]}")
        s=s.replace(old,new,1)
    p.write_text("// CAPTURE_TASK_OUTCOME_20261030\n"+s,encoding="utf-8")
    print("PATCHED",rel)

p="src/OpenFill.Core/Agent/CaptureRecipeStore__v0.1__2026-10-30.cs"
patch(p,[
('    public int Successes { get; set; }','''    public int Successes { get; set; } // successful technical DOM captures
    public int TaskSuccesses { get; set; }
    public int TaskFailures { get; set; }
    public int TaskPartials { get; set; }
    public string LastTaskOutcomeUtc { get; set; } = "";'''),
('''    public CaptureRecipe? Best(string host, string path)
        => Get(host).Where(x => string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Successes).ThenBy(x => x.LastElapsedMs).FirstOrDefault();''',
'''    private static int Observations(CaptureRecipe x) => x.TaskSuccesses + x.TaskFailures + x.TaskPartials;

    private static double Rank(CaptureRecipe x)
    {
        var n = Observations(x);
        // Verified end-to-end outcomes dominate raw capture counts. Smoothed rate avoids overrating 1/1.
        return n == 0 ? Math.Min(x.Successes, 1000) / 100000.0 :
            (x.TaskSuccesses + 1.0) / (n + 2.0) + Math.Min(n, 20) / 100.0;
    }

    public CaptureRecipe? Best(string host, string path)
        => Get(host).Where(x => string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(Rank).ThenByDescending(x => Observations(x)).ThenByDescending(x => x.Successes)
            .ThenBy(x => x.LastElapsedMs).FirstOrDefault();'''),
('''    public string Context(string host, string path, int max = 3)''',
'''    public void RecordTaskOutcome(string host, string path, string mode, string selector, string pattern, string outcome)
    {
        if (outcome is not ("success" or "partial" or "failed")) return;
        lock (Gate)
        {
            var file = FileFor(host);
            if (!File.Exists(file)) return;
            List<CaptureRecipe> rows;
            try { rows = JsonSerializer.Deserialize<List<CaptureRecipe>>(File.ReadAllText(file), Json) ?? []; }
            catch { return; }
            var rec = rows.FirstOrDefault(x => x.Path == path && x.Mode == mode &&
                x.Selector == selector && x.Pattern == pattern);
            if (rec is null) return;
            if (outcome == "success") rec.TaskSuccesses++;
            else if (outcome == "partial") rec.TaskPartials++;
            else rec.TaskFailures++;
            rec.LastTaskOutcomeUtc = DateTime.UtcNow.ToString("O");
            var temp = file + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(rows, Json));
            File.Move(temp, file, true);
        }
    }

    public string Context(string host, string path, int max = 3)'''),
('''var found = Get(host).Where(x => x.Path == path).OrderByDescending(x => x.Successes).Take(Math.Clamp(max, 0, 10)).ToArray();''',
'''var found = Get(host).Where(x => x.Path == path).OrderByDescending(Rank).Take(Math.Clamp(max, 0, 10)).ToArray();'''),
('''validated={x.Successes}; last_ms={x.LastElapsedMs}''',
'''technical_ok={x.Successes}; task_ok={x.TaskSuccesses}; task_partial={x.TaskPartials}; task_failed={x.TaskFailures}; last_ms={x.LastElapsedMs}''')
])
p="src/OpenFill.Core/Agent/ToolRegistry__v0.22__2026-10-29_2314.cs"
patch(p,[
('''    private readonly PageCaptureService _capture;''',
'''    private readonly PageCaptureService _capture;
    private readonly HashSet<(string Host,string Path,string Mode,string Selector,string Pattern)> _usedCaptures = new();

    /// <summary>Record one whole-task verdict per actually used capture method (not per successful snapshot).</summary>
    public void RecordCaptureTaskOutcome(string status)
    {
        foreach (var x in _usedCaptures)
            _recipes.RecordTaskOutcome(x.Host,x.Path,x.Mode,x.Selector,x.Pattern,status);
    }'''),
('''                    return ToolResult.Text(value.ToJsonString());
                }
                catch (Exception ex) { return ToolResult.Error("Page capture failed: " + ex.Message); }''',
'''                    if (Uri.TryCreate(value["url"]?.ToString(), UriKind.Absolute, out var captured))
                        _usedCaptures.Add((captured.Host,captured.AbsolutePath,mode,selector,regex));
                    return ToolResult.Text(value.ToJsonString());
                }
                catch (Exception ex) { return ToolResult.Error("Page capture failed: " + ex.Message); }''')
])
p="src/OpenFill.Core/Hosting/TaskRun__v0.1__2026-10-07_1732.cs"
patch(p,[
('''            var conclusion = await runner.RunAsync(task, ct);

            string host = "";''',
'''            var conclusion = await runner.RunAsync(task, ct);
            registry.RecordCaptureTaskOutcome(conclusion.Status);

            string host = "";''')
])
p="tests/OpenFill.Tests/CapturePipelineTests__v0.1__2026-10-30.cs"
patch(p,[
('''                T.Eq("reused correct filter", "speaker|invite", best.Pattern);''',
'''                T.Eq("reused correct filter", "speaker|invite", best.Pattern);
                store.Record("www.example.org","/feed","text","article","hidden",80);
                store.Record("www.example.org","/feed","text","article","hidden",80);
                store.Record("www.example.org","/feed","text","article","hidden",80);
                store.RecordTaskOutcome("www.example.org","/feed","text","main","speaker|invite","success");
                store.RecordTaskOutcome("www.example.org","/feed","text","article","hidden","failed");
                store.RecordTaskOutcome("www.example.org","/feed","text","article","hidden","partial");
                var verdictBest = new CaptureRecipeStore(temp).Best("www.example.org", "/feed");
                T.Eq("whole-task success outranks more technical captures", "main", verdictBest!.Selector);
                T.Eq("whole-task success persisted", 1, verdictBest.TaskSuccesses);
                T.Contains("recipe exposes actual task outcomes", store.Context("www.example.org","/feed"), "task_ok=1");'''),
('''                T.Check("Luna handler captures successfully", compact.Ok);''',
'''                T.Check("Luna handler captures successfully", compact.Ok);
                registry.RecordCaptureTaskOutcome("success");
                var lastUri = new Uri(result["url"]!.ToString());
                var savedBest = new CaptureRecipeStore(Path.Combine(paths.Notes,"recipes")).Best(lastUri.Host,lastUri.AbsolutePath);
                T.Eq("real ToolRegistry records full task verdict", 1, savedBest!.TaskSuccesses);''')
])
print("CAPTURE_QUALITY_PATCH_FINISHED")
