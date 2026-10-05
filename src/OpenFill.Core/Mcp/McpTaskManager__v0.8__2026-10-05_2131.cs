// OpenFill - Metadata: wersja 0.8, data 2026-10-05 21:31
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using OpenFill.Core.Agent;
using OpenFill.Core.Logging;

namespace OpenFill.Core.Mcp;

/// <summary>One task handed to OpenFill through MCP. Persisted as JSON in the tasks folder.</summary>
public sealed class McpTaskRecord
{
    public string Id { get; set; } = "";
    public string TaskText { get; set; } = "";
    /// <summary>running | needs_input | done | failed | cancelled</summary>
    public string Status { get; set; } = "running";
    /// <summary>Verdict of the inner model: success | partial | failed.</summary>
    public string? Outcome { get; set; }
    public string? Summary { get; set; }
    public string? Question { get; set; }
    /// <summary>Id of the earlier task this one continues (same browser tab or the same last address).</summary>
    public string? ContinuedFrom { get; set; }
    /// <summary>Model steps and estimated cost of the task (filled in when it ends).</summary>
    public int Steps { get; set; }
    public double? CostUsd { get; set; }
    /// <summary>OpenFill version that ran the task.</summary>
    public string? AppVersion { get; set; }
    public List<string>? Options { get; set; }
    public List<string> Activity { get; set; } = new();
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

/// <summary>
/// Task life cycle for MCP callers. One task at a time. A task has an ID that the caller uses to wait for the
/// result, answer the inner model's questions and cancel. Tasks are saved to disk (history only, no resume).
/// </summary>
public sealed class McpTaskManager
{
    public const int MaxWaitSeconds = 30;

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private readonly IRunHost _host;
    private readonly string _dir;
    private readonly TimeSpan _answerTimeout;
    private readonly object _lock = new();
    private McpTaskRecord? _active;
    private TaskCompletionSource<string>? _answer;
    private int _version;

    public McpTaskManager(IRunHost host, string tasksDir, TimeSpan answerTimeout)
    {
        _host = host;
        _dir = tasksDir;
        _answerTimeout = answerTimeout;
        _host.Activity += OnActivity;
        _host.UserWaiting += OnUserWaiting;
    }

    private static bool IsOpen(McpTaskRecord r) => r.Status is "running" or "needs_input" or "waiting_for_user";

    // ------------------------------------------------------------------ public operations

    /// <summary>Screenshot of the browser as it is now. Returns the saved file name in "file".</summary>
    public async Task<JsonObject> ScreenshotAsync(CancellationToken ct)
    {
        var file = await _host.CaptureScreenshotAsync(ct);
        if (file is null) return Error("OpenFill could not take a screenshot (the browser may not be ready).");
        return new JsonObject { ["status"] = "ok", ["file"] = file };
    }

    public async Task<JsonObject> StartAsync(string? task, int waitSeconds, CancellationToken ct, string? continueTaskId = null)
    {
        task = (task ?? "").Trim();
        if (task.Length == 0) return Error("The task text is empty. Describe what OpenFill should do in the browser, with all the data it needs.");
        if (_host.CheckBlocked(task) is { } why) return new JsonObject { ["status"] = "blocked", ["message"] = why, ["next_step"] = BlockedStep };

        McpTaskRecord? existing = null;
        bool similar = false;
        lock (_lock)
        {
            if (_active is not null && IsOpen(_active))
            {
                existing = _active;
                similar = Similar(task, existing.TaskText);
            }
        }
        if (existing is not null)
        {
            if (!similar) return Busy(existing);
            int v0; lock (_lock) v0 = _version;
            await WaitAsync(existing, v0, waitSeconds, ct);
            return Snapshot(existing, "This matches the task that is already open, so no new task was started.");
        }

        // Going back to an earlier session: by id, or - when the new text is about the same thing as a recent finished task - by content.
        McpTaskRecord? prior = null;
        if (!string.IsNullOrWhiteSpace(continueTaskId))
        {
            prior = Find(continueTaskId, out _);
            if (prior is null) return Error("There is no earlier task with id " + continueTaskId.Trim() + ", so there is nothing to continue. Start the task without continue_task_id.");
        }
        else prior = FindSimilarFinished(task);

        McpTaskRecord rec;
        lock (_lock)
        {
            if (_active is not null && IsOpen(_active)) return Busy(_active);
            var now = DateTime.UtcNow;
            rec = new McpTaskRecord
            {
                Id = $"OF-{DateTime.Now:yyyyMMdd-HHmm}-{Guid.NewGuid().ToString("N")[..4]}",
                TaskText = task,
                Status = "running",
                ContinuedFrom = prior?.Id,
                AppVersion = BuildInfo.Describe(),
                CreatedUtc = now,
                UpdatedUtc = now
            };
            _active = rec;
            _host.AskInterceptor = OnAskAsync;
            Save(rec);
        }

        Task<RunConclusion>? run = null;
        var options = new RunOptions(rec.Id, prior?.Id, prior is null ? null : DescribeEarlier(prior));
        try { run = _host.StartRun(task, options); } catch { }
        if (run is null)
        {
            lock (_lock)
            {
                rec.Status = "failed";
                rec.Summary = "OpenFill could not start the task: another task is running in the OpenFill window, or the OpenAI key is missing.";
                _host.AskInterceptor = null;
                Touch(rec);
            }
            return Snapshot(rec, null);
        }

        _ = Task.Run(async () =>
        {
            RunConclusion c;
            try { c = await run; }
            catch (Exception ex) { c = new RunConclusion("failed", "Unexpected error: " + ex.Message, null); }
            Complete(rec, c);
        });

        int ver; lock (_lock) ver = _version;
        await WaitAsync(rec, ver, waitSeconds, ct);
        return Snapshot(rec, null);
    }

    public async Task<JsonObject> StatusAsync(string? id, int waitSeconds, CancellationToken ct)
    {
        var rec = Find(id, out var fromDisk);
        if (rec is null) return NotFound(id);
        if (!fromDisk)
        {
            int v; lock (_lock) v = _version;
            await WaitAsync(rec, v, waitSeconds, ct);
        }
        return Snapshot(rec, null, fromDisk);
    }

    public async Task<JsonObject> ReplyAsync(string? id, string? answer, int waitSeconds, CancellationToken ct)
    {
        var rec = Find(id, out var fromDisk);
        if (rec is null) return NotFound(id);
        if (fromDisk) return Snapshot(rec, "This task is not active any more, so nothing is waiting for an answer.", true);
        if (string.IsNullOrWhiteSpace(answer)) return Error("The answer is empty.");

        TaskCompletionSource<string>? tcs;
        int ver;
        lock (_lock)
        {
            tcs = _answer;
            if (rec.Status == "waiting_for_user")
                return Snapshot(rec, "This question can only be answered by the user, in the OpenFill window. Do not send an answer here.");
            if (rec.Status != "needs_input" || tcs is null)
                return Snapshot(rec, "No question is waiting for an answer in this task. Use openfill_task_status to see where it stands.");
            rec.Status = "running";
            rec.Question = null;
            rec.Options = null;
            Touch(rec);
            ver = _version;
        }
        tcs.TrySetResult(answer);
        await WaitAsync(rec, ver, waitSeconds, ct);
        return Snapshot(rec, null);
    }

    public JsonObject Cancel(string? id)
    {
        var rec = Find(id, out var fromDisk);
        if (rec is null) return NotFound(id);
        if (fromDisk || !IsOpen(rec)) return Snapshot(rec, "The task was not running, nothing to cancel.", fromDisk);
        TaskCompletionSource<string>? tcs;
        lock (_lock)
        {
            rec.Status = "cancelled";
            rec.Question = null;
            rec.Options = null;
            tcs = _answer;
            Touch(rec);
        }
        tcs?.TrySetCanceled();
        _host.Stop();
        return Snapshot(rec, "The task was cancelled.");
    }

    // ------------------------------------------------------------------ inner model hooks

    private async Task<string> OnAskAsync(string question, IReadOnlyList<string>? options, CancellationToken ct)
    {
        TaskCompletionSource<string> tcs;
        lock (_lock)
        {
            if (_active is null || !IsOpen(_active))
                return "(nobody is available to answer - carry on on your own if that is safe)";
            tcs = _answer = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            _active.Status = "needs_input";
            _active.Question = question;
            _active.Options = options?.ToList();
            Touch(_active);
        }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(_answerTimeout);
        using var reg = timeout.Token.Register(() => tcs.TrySetCanceled());
        try { return await tcs.Task; }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return "(the caller did not answer in time - do not guess: finish now with status partial, and say what you were waiting for and which page the browser is on)";
        }
        finally
        {
            lock (_lock)
            {
                if (_active is { Status: "needs_input" })
                {
                    _active.Status = "running";
                    _active.Question = null;
                    _active.Options = null;
                    Touch(_active);
                }
                _answer = null;
            }
        }
    }

    /// <summary>The inner model waits for the person at the computer (e.g. a code from their e-mail): status waiting_for_user.</summary>
    private void OnUserWaiting(string? question)
    {
        lock (_lock)
        {
            if (_active is null || !IsOpen(_active)) return;
            if (question is not null)
            {
                _active.Status = "waiting_for_user";
                _active.Question = question;
                _active.Options = null;
                Touch(_active);
            }
            else if (_active.Status == "waiting_for_user")
            {
                _active.Status = "running";
                _active.Question = null;
                Touch(_active);
            }
        }
    }

    private void OnActivity(string message)
    {
        lock (_lock)
        {
            if (_active is null || !IsOpen(_active)) return;
            _active.Activity.Add(message.Length > 160 ? message[..160] + "..." : message);
            while (_active.Activity.Count > 8) _active.Activity.RemoveAt(0);
        }
    }

    private void Complete(McpTaskRecord rec, RunConclusion c)
    {
        lock (_lock)
        {
            if (rec.Status != "cancelled") rec.Status = c.Status == "failed" ? "failed" : c.Status == "blocked" ? "blocked" : "done";
            if (_host.Progress is { } prog) { rec.Steps = prog.Steps; rec.CostUsd = Math.Round(prog.CostUsd, 4); }
            rec.Outcome = c.Status;
            rec.Summary = c.Summary;
            rec.Question = null;
            rec.Options = null;
            if (ReferenceEquals(_active, rec)) _host.AskInterceptor = null;
            Touch(rec);
        }
    }

    // ------------------------------------------------------------------ helpers

    private async Task WaitAsync(McpTaskRecord rec, int startVersion, int seconds, CancellationToken ct)
    {
        var end = DateTime.UtcNow.AddSeconds(Math.Clamp(seconds, 0, MaxWaitSeconds));
        while (DateTime.UtcNow < end)
        {
            // waiting_for_user is a long wait on the person: keep waiting until something changes, do not return at once.
            lock (_lock) { if ((rec.Status != "running" && rec.Status != "waiting_for_user") || _version != startVersion) return; }
            try { await Task.Delay(250, ct); }
            catch (OperationCanceledException) { return; }
        }
    }

    private void Touch(McpTaskRecord rec)
    {
        _version++;
        rec.UpdatedUtc = DateTime.UtcNow;
        Save(rec);
    }

    private void Save(McpTaskRecord rec)
    {
        try
        {
            Directory.CreateDirectory(_dir);
            var copy = JsonSerializer.Deserialize<McpTaskRecord>(JsonSerializer.Serialize(rec))!;
            copy.TaskText = Redactor.Text(copy.TaskText);
            if (copy.Summary is not null) copy.Summary = Redactor.Text(copy.Summary);
            File.WriteAllText(Path.Combine(_dir, rec.Id + ".json"), JsonSerializer.Serialize(copy, JsonOpts));
        }
        catch { /* history is best effort */ }
    }

    private McpTaskRecord? Find(string? id, out bool fromDisk)
    {
        fromDisk = false;
        lock (_lock)
        {
            if (string.IsNullOrWhiteSpace(id)) return _active;
            if (_active is not null && _active.Id == id.Trim()) return _active;
        }
        var safe = new string(id!.Trim().Where(ch => char.IsLetterOrDigit(ch) || ch == '-').ToArray());
        var file = Path.Combine(_dir, safe + ".json");
        if (safe.Length == 0 || !File.Exists(file)) return null;
        try
        {
            var rec = JsonSerializer.Deserialize<McpTaskRecord>(File.ReadAllText(file));
            fromDisk = rec is not null;
            return rec;
        }
        catch { return null; }
    }

    /// <summary>Short account of an earlier task for the inner model: what it was and how it ended.</summary>
    internal static string DescribeEarlier(McpTaskRecord prior)
    {
        var s = $"Earlier task: \"{OutputLimiter.Head(prior.TaskText.ReplaceLineEndings(" "), 300)}\".";
        if (!string.IsNullOrWhiteSpace(prior.Summary))
            s += $" It ended {(prior.Outcome ?? prior.Status)}; its result: {OutputLimiter.Head(prior.Summary.ReplaceLineEndings(" "), 700)}";
        else
            s += $" It was left {prior.Status}.";
        return s + " (If the new task is clearly about something else, ignore this and start fresh.)";
    }

    /// <summary>The most recent finished task (of the last 30 on disk) whose text is about the same thing as the new one.</summary>
    private McpTaskRecord? FindSimilarFinished(string task)
    {
        try
        {
            if (!Directory.Exists(_dir)) return null;
            foreach (var f in new DirectoryInfo(_dir).GetFiles("OF-*.json").OrderByDescending(x => x.LastWriteTimeUtc).Take(30))
            {
                McpTaskRecord? r;
                try { r = JsonSerializer.Deserialize<McpTaskRecord>(File.ReadAllText(f.FullName)); } catch { continue; }
                if (r is null || IsOpen(r)) continue;
                if (SameJob(task, r.TaskText)) return r;
            }
        }
        catch { }
        return null;
    }

    /// <summary>
    /// Stricter than Similar: used to pick an earlier session by content, which hands over its browser tab. A repeat or a retry of
    /// the same job qualifies; the same sentence about another site does not (the web addresses must match, the words nearly all).
    /// </summary>
    public static bool SameJob(string a, string b)
    {
        static HashSet<string> Words(string s) =>
            new(Regex.Split(s.ToLowerInvariant(), @"[^\p{L}\p{N}]+").Where(t => t.Length >= 3));
        static HashSet<string> Hosts(string s) =>
            new(Regex.Matches(s.ToLowerInvariant(), @"https?://([^/\s""'<>]+)").Select(m => m.Groups[1].Value.Replace("www.", "")));
        var x = Words(a);
        var y = Words(b);
        if (x.Count == 0 || y.Count == 0) return false;
        if (!Hosts(a).SetEquals(Hosts(b))) return false;
        return (double)x.Intersect(y).Count() / x.Union(y).Count() >= 0.85;
    }

    internal static bool Similar(string a, string b)
    {
        static HashSet<string> Tok(string s) =>
            new(Regex.Split(s.ToLowerInvariant(), @"[^\p{L}\p{N}]+").Where(t => t.Length >= 3));
        var x = Tok(a);
        var y = Tok(b);
        if (x.Count == 0 || y.Count == 0) return false;
        var inter = x.Intersect(y).Count();
        var union = x.Union(y).Count();
        if ((double)inter / union >= 0.6) return true;
        return Math.Min(x.Count, y.Count) >= 3 && (double)inter / Math.Min(x.Count, y.Count) >= 0.9;
    }

    private const string BlockedStep = "The site reached its hard cost limit and is blocked. Tell the user what was done so far (summary). The block can be lifted only by the user, in the OpenFill window - you cannot do it. Do not start more tasks on that site.";

    private static string Preview(string s) => s.Length > 200 ? s[..200] + "..." : s;

    /// <summary>One answer with everything that helps to find out what is wrong: version, runtime, active and recent tasks, and what the host knows.</summary>
    public JsonObject Info()
    {
        var proc = System.Diagnostics.Process.GetCurrentProcess();
        var o = new JsonObject
        {
            ["status"] = "ok",
            ["app"] = new JsonObject
            {
                ["name"] = "OpenFill",
                ["version"] = BuildInfo.Version,
                ["version_date"] = BuildInfo.Format(BuildInfo.VersionDateLocal),
                ["build"] = BuildInfo.Format(BuildInfo.BuildTimeLocal)
            },
            ["now"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            ["runtime"] = new JsonObject
            {
                ["os"] = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                ["dotnet"] = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                ["started"] = proc.StartTime.ToString("yyyy-MM-dd HH:mm:ss"),
                ["uptime_minutes"] = (int)(DateTime.Now - proc.StartTime).TotalMinutes,
                ["memory_mb"] = (int)(proc.WorkingSet64 / (1024 * 1024)),
                ["processors"] = Environment.ProcessorCount
            },
            ["caller_answer_timeout_minutes"] = _answerTimeout.TotalMinutes
        };

        McpTaskRecord? active;
        lock (_lock) active = _active is not null && IsOpen(_active) ? _active : null;
        o["active_task"] = active is null ? null : Snapshot(active, null);

        var recent = new JsonArray();
        try
        {
            if (Directory.Exists(_dir))
                foreach (var f in new DirectoryInfo(_dir).GetFiles("OF-*.json").OrderByDescending(x => x.LastWriteTimeUtc).Take(8))
                {
                    McpTaskRecord? r;
                    try { r = JsonSerializer.Deserialize<McpTaskRecord>(File.ReadAllText(f.FullName)); } catch { continue; }
                    if (r is null) continue;
                    recent.Add(new JsonObject
                    {
                        ["task_id"] = r.Id,
                        ["status"] = r.Status,
                        ["outcome"] = r.Outcome,
                        ["steps"] = r.Steps,
                        ["cost_usd"] = r.CostUsd,
                        ["started"] = r.CreatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                        ["version"] = r.AppVersion,
                        ["task"] = Preview(r.TaskText),
                        ["summary"] = r.Summary is { Length: > 160 } s ? s[..160] + "..." : r.Summary
                    });
                }
        }
        catch { }
        o["recent_tasks"] = recent;

        try { if (_host.Diagnostics() is { } d) o["openfill"] = d; } catch (Exception ex) { o["openfill_error"] = ex.Message; }
        return o;
    }

    private JsonObject Snapshot(McpTaskRecord rec, string? note, bool fromDisk = false)
    {
        lock (_lock)
        {
            var status = rec.Status;
            string? o0Url = null;
            if (fromDisk && IsOpen(rec))
            {
                status = "interrupted";
                note = "This task was cut off when OpenFill was closed or restarted. It is no longer running.";
                if (_host.LastUrlOf(rec.Id) is { } lastUrl) o0Url = lastUrl;
            }
            var o = new JsonObject
            {
                ["task_id"] = rec.Id,
                ["status"] = status,
                ["task"] = Preview(rec.TaskText),
                ["started"] = rec.CreatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                ["updated"] = rec.UpdatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
            };
            if (rec.ContinuedFrom is not null) o["continued_from"] = rec.ContinuedFrom;
            if (o0Url is not null) o["last_url"] = o0Url;
            if (rec.Outcome is not null) o["outcome"] = rec.Outcome;
            if (rec.Summary is not null) o["summary"] = rec.Summary;
            if (rec.Question is not null) o["question"] = rec.Question;
            if (rec.Options is { Count: > 0 }) o["options"] = new JsonArray(rec.Options.Select(x => (JsonNode)x).ToArray());
            if (rec.Activity.Count > 0 && status is "running" or "needs_input" or "waiting_for_user")
                o["recent_activity"] = new JsonArray(rec.Activity.Select(x => (JsonNode)x).ToArray());
            int steps = rec.Steps; double? cost = rec.CostUsd; string? warn = null;
            if (!fromDisk && IsOpen(rec) && ReferenceEquals(rec, _active) && _host.Progress is { } live) { steps = live.Steps; cost = live.CostUsd; warn = live.Warning; }
            if (steps > 0) o["steps"] = steps;
            if (cost is { } cu) o["cost_usd"] = Math.Round(cu, 4);
            if (warn is not null) o["warning"] = warn;
            if (note is not null) o["note"] = note;
            o["next_step"] = status switch
            {
                "running" => "OpenFill is still working. Call openfill_task_status with this task_id to wait for the result (each call waits up to 30 seconds). Do not start another task. " +
                    "Do not cancel it just because it takes long: normal jobs take 10-30 steps (a simple form), 40-120 (a sign-up or a profile), 80-150 (a flight search with calendars); watch steps and recent_activity. " +
                    "Cancel only when recent_activity shows the same thing again and again. Cost is capped by OpenFill itself." +
                    (warn is null ? "" : " WARNING: " + warn + " Check recent_activity: if it repeats, cancel and tell the user; otherwise keep waiting."),
                "needs_input" => "OpenFill needs an answer to the question above. If you do not know it, ask the user, then give it with openfill_reply.",
                "waiting_for_user" => "OpenFill is waiting for the user to type something only they can give (see question), usually a verification code. " +
                    "Do NOT send it to OpenFill and do not call openfill_reply. If you can read it yourself (for example from the user's e-mail), show it to the user in this chat right now so they can enter it in the OpenFill window; otherwise tell the user to look at the OpenFill window. " +
                    "Then keep calling openfill_task_status with this task_id until the status changes.",
                "done" => "Finished. Tell the user the result from summary; outcome says whether everything was done (success) or only partly (partial).",
                "failed" => "The task failed. Tell the user why (summary). You may start a new task with more detail.",
                "blocked" => BlockedStep,
                "interrupted" => "The task was cut off before it finished. To pick it up, start a new task with continue_task_id=" + rec.Id + " (and the instructions for what is still to do): OpenFill reopens a tab at last_url, if known, and tells its browser agent what the earlier task was. Check what was already saved before repeating anything.",
                "cancelled" => "The task was cancelled.",
                _ => "Start a new task if the work is still needed."
            };
            return o;
        }
    }

    private static JsonObject Busy(McpTaskRecord open) => new()
    {
        ["status"] = "busy",
        ["open_task_id"] = open.Id,
        ["open_task"] = Preview(open.TaskText),
        ["open_task_status"] = open.Status,
        ["message"] = "OpenFill runs one task at a time and task " + open.Id + " is still open. Wait for it with openfill_task_status, answer its question with openfill_reply, or cancel it with openfill_cancel_task if it is no longer needed. Then start the new task."
    };

    private JsonObject NotFound(string? id)
    {
        string? open = null;
        lock (_lock) { if (_active is not null && IsOpen(_active)) open = _active.Id; }
        var o = new JsonObject
        {
            ["status"] = "not_found",
            ["message"] = string.IsNullOrWhiteSpace(id) ? "There is no task yet. Start one with openfill_start_task." : "There is no task with id " + id + "."
        };
        if (open is not null) o["open_task_id"] = open;
        return o;
    }

    private static JsonObject Error(string message) => new() { ["status"] = "error", ["message"] = message };
}
