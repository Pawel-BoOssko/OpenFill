// OpenFill - Metadata: wersja 0.17, data 2026-10-14 22:35
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using OpenFill.Core.Agent;
using OpenFill.Core.Logging;

namespace OpenFill.Core.Mcp;

/// <summary>One task handed to OpenFill through MCP. Persisted as JSON in the tasks folder.</summary>
public sealed class McpTaskRecord
{
    public string Id { get; set; } = "";
    public string TaskText { get; set; } = "";
    /// <summary>Optional stable external request identity for de-duplication.</summary>
    public string? IdempotencyKey { get; set; }
    /// <summary>running | needs_input | waiting_for_user | done | failed | blocked | cancelled</summary>
    public string Status { get; set; } = "running";
    /// <summary>Verdict of the inner model: success | partial | failed.</summary>
    public string? Outcome { get; set; }
    public string? Summary { get; set; }
    /// <summary>Generated temporary passwords explicitly passed through to the authorized calling model.</summary>
    public List<TemporaryCredential>? TemporaryCredentials { get; set; }
    /// <summary>Name the calling chat gave for itself when it handed over the task (for example Bishop, GPT, Papa).</summary>
    public string? Requester { get; set; }
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
    /// <summary>Counts changes of this record, so a waiting caller notices that something happened (not saved).</summary>
    [JsonIgnore] public int Ver { get; set; }
}

/// <summary>
/// Task life cycle for MCP callers. Several tasks may run at once (as many as the host allows), each on its own web site:
/// the host refuses a task whose site another task is using. A task has an ID that the caller uses to wait for the
/// result, answer the inner model's questions and cancel. Tasks are saved to disk (history only, no resume).
/// </summary>
public sealed class McpTaskManager
{
    public const int MaxWaitSeconds = 30;

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    /// <summary>A task that is running (or waiting for an answer): its record and the answer the inner model waits for.</summary>
    private sealed class Live
    {
        public required McpTaskRecord Rec { get; init; }
        public TaskCompletionSource<string>? Answer { get; set; }
    }

    private readonly IRunHost _host;
    private readonly string _dir;
    private readonly TimeSpan _answerTimeout;
    private readonly object _lock = new();
    private readonly Dictionary<string, Live> _open = new();
    private McpTaskRecord? _last;

    public McpTaskManager(IRunHost host, string tasksDir, TimeSpan answerTimeout)
    {
        _host = host;
        _dir = tasksDir;
        _answerTimeout = answerTimeout;
    }

    private static bool IsOpen(McpTaskRecord r) => r.Status is "running" or "needs_input" or "waiting_for_user";

    private List<McpTaskRecord> OpenRecords()
    {
        lock (_lock) return _open.Values.Select(l => l.Rec).Where(IsOpen).OrderBy(r => r.CreatedUtc).ToList();
    }

    // ------------------------------------------------------------------ public operations

    /// <summary>Screenshot of the browser tab of one task (the newest task's tab when no id is given). Returns the saved file name in "file".</summary>
    public async Task<JsonObject> ScreenshotAsync(string? taskId, CancellationToken ct)
    {
        var key = string.IsNullOrWhiteSpace(taskId) ? null : taskId.Trim();
        var file = await _host.CaptureScreenshotAsync(key, ct);
        if (file is null) return Error("OpenFill could not take a screenshot (the browser may not be ready, or there is no such task tab).");
        return new JsonObject { ["status"] = "ok", ["file"] = file };
    }

    public Task<JsonObject> ScreenshotAsync(CancellationToken ct) => ScreenshotAsync(null, ct);

    /// <summary>Sets, ends or reports the maintenance block (no new tasks, e.g. before a restart) and adds which MCP tasks are still open.</summary>
    public JsonObject Maintenance(int? minutes, string? reason, string? by)
    {
        var r = _host.Maintenance(minutes, reason, CleanWho(by));
        List<string> open;
        lock (_lock) open = _open.Values.Select(l => l.Rec).Where(IsOpen).Select(x => x.Id).ToList();
        r["open_mcp_tasks"] = new JsonArray(open.Select(x => (JsonNode)x).ToArray());
        var active = r["active"]?.GetValue<bool>() == true;
        var running = r["running_tasks"]?.GetValue<int>() ?? 0;
        r["status"] = "ok";
        r["next_step"] = !active
            ? "OpenFill accepts new tasks."
            : running == 0
                ? "No task is running. OpenFill can be restarted now; a restart ends the block (or end it with minutes=0)."
                : "Wait: " + running + " task(s) still running. Call openfill_maintenance again without arguments to check; when running_tasks is 0 it is safe to restart.";
        return r;
    }

    private static string? CleanWho(string? s)
    {
        s = s?.Replace('·', ' ').Trim();
        if (string.IsNullOrEmpty(s)) return null;
        return s.Length > 40 ? s[..40] : s;
    }

    public async Task<JsonObject> StartAsync(string? task, int waitSeconds, CancellationToken ct, string? continueTaskId = null, string? title = null, string? requester = null, string? chat = null, string? idempotencyKey = null)
    {
        task = (task ?? "").Trim();
        if (task.Length == 0) return Error("The task text is empty. Describe what OpenFill should do in the browser, with all the data it needs.");
        var who = CleanWho(requester);
        var chatName = CleanWho(chat);
        if (who is null || chatName is null)
            return new JsonObject
            {
                ["status"] = "needs_identity",
                ["message"] = "Nothing was started. OpenFill needs two fields with every new task: requester (your own name, for example 'Claude Bishop', 'Claude Papa' or 'ChatGPT') and chat (the name of this chat, for example 'Skakanka'). " + (who is null ? "requester is missing. " : "") + (chatName is null ? "chat is missing. " : "") + "If your openfill_start_task definition has no such fields, refresh the tool definitions of the OpenFill connector (reload its tool list) and call again with both fields filled in.",
                ["next_step"] = "Refresh the tool definitions if requester and chat are not in your openfill_start_task, then call openfill_start_task again with requester and chat."
            };
        who = who + " · " + chatName;
        if (idempotencyKey is not null)
        {
            idempotencyKey = idempotencyKey.Trim();
            if (!Regex.IsMatch(idempotencyKey, @"^[A-Za-z0-9][A-Za-z0-9._:-]{0,127}$"))
                return Error("Invalid idempotency_key: use 1-128 letters, digits, dot, dash, underscore or colon.");
            if (!string.IsNullOrWhiteSpace(continueTaskId))
                return Error("idempotency_key and continue_task_id cannot be used together.");
            var previousRequest = FindByIdempotencyKey(who, idempotencyKey, out var previousFromDisk);
            if (previousRequest is not null)
            {
                if (!string.Equals(previousRequest.TaskText, task, StringComparison.Ordinal))
                    return KeyConflict(idempotencyKey, previousRequest.Id);
                return Snapshot(previousRequest, "The request with this idempotency_key already exists; no duplicate was started.", previousFromDisk);
            }
        }
        if (_host.CheckBlocked(task) is { } why) return new JsonObject { ["status"] = "blocked", ["message"] = why, ["next_step"] = BlockedStep };

        // The same job again while it is still running: hand back the running one instead of starting a copy.
        McpTaskRecord? same = null;
        if (idempotencyKey is null) lock (_lock) same = _open.Values.Select(l => l.Rec).FirstOrDefault(r => IsOpen(r) && Similar(task, r.TaskText));
        if (same is not null)
        {
            int v0; lock (_lock) v0 = same.Ver;
            await WaitAsync(same, v0, waitSeconds, ct);
            return Snapshot(same, "This matches the task that is already open, so no new task was started.");
        }

        // Going back to an earlier session: by id, or - when the new text is about the same thing as a recent finished task - by content.
        McpTaskRecord? prior;
        if (!string.IsNullOrWhiteSpace(continueTaskId))
        {
            var cid = continueTaskId.Trim();
            bool stillRunning;
            lock (_lock) stillRunning = _open.TryGetValue(cid, out var l) && IsOpen(l.Rec);
            if (stillRunning) return Error("Task " + cid + " is still running, so it cannot be continued yet. Wait for it with openfill_task_status.");
            prior = Find(cid, out _);
            if (prior is null) return Error("There is no earlier task with id " + cid + ", so there is nothing to continue. Start the task without continue_task_id.");
        }
        else prior = idempotencyKey is null ? FindSimilarFinished(task) : null;

        var max = Math.Max(1, _host.MaxParallel);
        Live live;
        lock (_lock)
        {
            if (idempotencyKey is not null)
            {
                var racing = _open.Values.Select(l => l.Rec)
                    .FirstOrDefault(r => r.Requester == who && r.IdempotencyKey == idempotencyKey);
                if (racing is not null)
                    return string.Equals(racing.TaskText, task, StringComparison.Ordinal)
                        ? Snapshot(racing, "Existing request already running; no duplicate was started.")
                        : KeyConflict(idempotencyKey, racing.Id);
                if (_last?.Requester == who && _last.IdempotencyKey == idempotencyKey)
                    return string.Equals(_last.TaskText, task, StringComparison.Ordinal)
                        ? Snapshot(_last, "Existing request already completed; no duplicate was started.")
                        : KeyConflict(idempotencyKey, _last.Id);
            }
            var open = _open.Values.Select(l => l.Rec).Where(IsOpen).OrderBy(r => r.CreatedUtc).ToList();
            if (open.Count >= max) return Busy(open, max);
            var now = DateTime.UtcNow;
            var rec = new McpTaskRecord
            {
                Id = $"OF-{DateTime.Now:yyyyMMdd-HHmm}-{Guid.NewGuid().ToString("N")[..4]}",
                TaskText = task,
                IdempotencyKey = idempotencyKey,
                Status = "running",
                ContinuedFrom = prior?.Id,
                AppVersion = BuildInfo.Describe(),
                Requester = who,
                CreatedUtc = now,
                UpdatedUtc = now
            };
            live = new Live { Rec = rec };
            _open[rec.Id] = live;
        }
        var recNew = live.Rec;

        var options = new RunOptions(recNew.Id, prior?.Id, prior is null ? null : DescribeEarlier(prior),
            Ask: (q, o, c) => OnAskAsync(live, q, o, c),
            Activity: m => OnActivity(live, m),
            UserWaiting: q => OnUserWaiting(live, q),
            Title: string.IsNullOrWhiteSpace(title) ? null : title.Trim(),
            Requester: who);

        StartResult started;
        try { started = _host.TryStart(task, options); }
        catch (Exception ex) { started = new StartResult(null, new RunRefusal("other", "OpenFill could not start the task: " + ex.Message)); }

        if (started.Run is null)
        {
            var refusal = started.Refusal ?? new RunRefusal("other", "OpenFill could not start the task.");
            lock (_lock) _open.Remove(recNew.Id);
            switch (refusal.Kind)
            {
                case "domain_busy":
                    // The task was not started: no record is kept.
                    return new JsonObject { ["status"] = "domain_busy", ["message"] = refusal.Message };
                case "maintenance":
                    return new JsonObject { ["status"] = "maintenance", ["message"] = refusal.Message + " Nothing was started. Do not retry in a loop: try again after the time given, or tell the person." };
                case "limit":
                    return new JsonObject { ["status"] = "busy", ["message"] = refusal.Message + " Wait for one of the open tasks with openfill_task_status, or cancel one with openfill_cancel_task, then start the new task." };
                default:
                    lock (_lock)
                    {
                        recNew.Status = "failed";
                        recNew.Summary = refusal.Kind == "other"
                            ? "OpenFill could not start the task: another task is running in the OpenFill window, or the OpenAI key is missing."
                            : refusal.Message;
                        _last = recNew;
                        Touch(recNew);
                    }
                    return Snapshot(recNew, null);
            }
        }

        lock (_lock) { _last = recNew; Touch(recNew); }

        var run = started.Run;
        _ = Task.Run(async () =>
        {
            RunConclusion c;
            try { c = await run; }
            catch (Exception ex) { c = new RunConclusion("failed", "Unexpected error: " + ex.Message, null); }
            Complete(live, c);
        });

        int ver; lock (_lock) ver = recNew.Ver;
        await WaitAsync(recNew, ver, waitSeconds, ct);
        return Snapshot(recNew, null);
    }

    public async Task<JsonObject> StatusAsync(string? id, int waitSeconds, CancellationToken ct)
    {
        var rec = Resolve(id, out var fromDisk, out var problem);
        if (problem is not null) return problem;
        if (rec is null) return NotFound(id);
        if (!fromDisk)
        {
            int v; lock (_lock) v = rec.Ver;
            await WaitAsync(rec, v, waitSeconds, ct);
        }
        return Snapshot(rec, null, fromDisk);
    }

    public async Task<JsonObject> ReplyAsync(string? id, string? answer, int waitSeconds, CancellationToken ct)
    {
        var rec = Resolve(id, out var fromDisk, out var problem);
        if (problem is not null) return problem;
        if (rec is null) return NotFound(id);
        if (fromDisk) return Snapshot(rec, "This task is not active any more, so nothing is waiting for an answer.", true);
        if (string.IsNullOrWhiteSpace(answer)) return Error("The answer is empty.");

        TaskCompletionSource<string>? tcs;
        int ver;
        lock (_lock)
        {
            tcs = _open.TryGetValue(rec.Id, out var live) ? live.Answer : null;
            if (rec.Status == "waiting_for_user")
                return Snapshot(rec, "This question can only be answered by the user, in the OpenFill window. Do not send an answer here.");
            if (rec.Status != "needs_input" || tcs is null)
                return Snapshot(rec, "No question is waiting for an answer in this task. Use openfill_task_status to see where it stands.");
            rec.Status = "running";
            rec.Question = null;
            rec.Options = null;
            Touch(rec);
            ver = rec.Ver;
        }
        tcs.TrySetResult(answer);
        await WaitAsync(rec, ver, waitSeconds, ct);
        return Snapshot(rec, null);
    }

    public JsonObject Cancel(string? id)
    {
        var rec = Resolve(id, out var fromDisk, out var problem);
        if (problem is not null) return problem;
        if (rec is null) return NotFound(id);
        if (fromDisk || !IsOpen(rec)) return Snapshot(rec, "The task was not running, nothing to cancel.", fromDisk);
        TaskCompletionSource<string>? tcs;
        lock (_lock)
        {
            rec.Status = "cancelled";
            rec.Question = null;
            rec.Options = null;
            tcs = _open.TryGetValue(rec.Id, out var live) ? live.Answer : null;
            Touch(rec);
        }
        tcs?.TrySetCanceled();
        _host.Stop(rec.Id);
        return Snapshot(rec, "The task was cancelled.");
    }

    /// <summary>Gracefully notify polling clients before the host closes.
    /// MCP Streamable HTTP does not proactively deliver messages to disconnected chats:
    /// clients see interrupted in any pending/new status request, and can continue with continue_task_id.
    /// </summary>
    public int PrepareShutdown(string reason = "OpenFill is closing for maintenance")
    {
        _host.Maintenance(15, reason, "OpenFill window");
        List<(string Id, TaskCompletionSource<string>? Answer)> affected;
        lock (_lock)
        {
            affected = new List<(string, TaskCompletionSource<string>?)>();
            foreach (var live in _open.Values.Where(l => IsOpen(l.Rec)))
            {
                var rec = live.Rec;
                rec.Status = "interrupted";
                rec.Outcome = "partial";
                rec.Summary = reason + ". The last action may or may not have completed. " +
                    "After OpenFill restarts, query openfill_task_status, verify saved changes and " +
                    "continue only what is missing with continue_task_id=" + rec.Id + ".";
                rec.Question = null;
                rec.Options = null;
                Touch(rec);
                affected.Add((rec.Id, live.Answer));
            }
        }
        foreach (var (id, answer) in affected)
        {
            answer?.TrySetCanceled();
            _host.Stop(id);
        }
        return affected.Count;
    }
    // ------------------------------------------------------------------ inner model hooks (one set per task)

    private async Task<string> OnAskAsync(Live live, string question, IReadOnlyList<string>? options, CancellationToken ct)
    {
        var rec = live.Rec;
        TaskCompletionSource<string> tcs;
        lock (_lock)
        {
            if (!IsOpen(rec))
                return "(nobody is available to answer - carry on on your own if that is safe)";
            tcs = live.Answer = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            rec.Status = "needs_input";
            rec.Question = question;
            rec.Options = options?.ToList();
            Touch(rec);
        }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(_answerTimeout);
        using var reg = timeout.Token.Register(() => tcs.TrySetCanceled());
        try { return await tcs.Task; }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return "(the calling model did not answer before the timeout. Do not guess. If human input can resolve this, call ask_user again with user_only=true so the person in the OpenFill window can answer, then continue the original task. Finish as partial only if neither channel can provide the required information.)";
        }
        finally
        {
            lock (_lock)
            {
                if (rec.Status == "needs_input")
                {
                    rec.Status = "running";
                    rec.Question = null;
                    rec.Options = null;
                    Touch(rec);
                }
                live.Answer = null;
            }
        }
    }

    /// <summary>The inner model waits for the person at the computer (e.g. a code from their e-mail): status waiting_for_user.</summary>
    private void OnUserWaiting(Live live, string? question)
    {
        var rec = live.Rec;
        lock (_lock)
        {
            if (!IsOpen(rec)) return;
            if (question is not null)
            {
                rec.Status = "waiting_for_user";
                rec.Question = question;
                rec.Options = null;
                Touch(rec);
            }
            else if (rec.Status == "waiting_for_user")
            {
                rec.Status = "running";
                rec.Question = null;
                Touch(rec);
            }
        }
    }

    private void OnActivity(Live live, string message)
    {
        var rec = live.Rec;
        lock (_lock)
        {
            if (!IsOpen(rec)) return;
            rec.Activity.Add(message.Length > 160 ? message[..160] + "..." : message);
            while (rec.Activity.Count > 8) rec.Activity.RemoveAt(0);
        }
    }

    private void Complete(Live live, RunConclusion c)
    {
        var rec = live.Rec;
        lock (_lock)
        {
            if (rec.Status == "interrupted")
            {
                _open.Remove(rec.Id);
                _last = rec;
                Touch(rec);
                return;
            }
            if (rec.Status is not ("cancelled" or "interrupted")) rec.Status = c.Status == "failed" ? "failed" : c.Status == "blocked" ? "blocked" : c.Status == "stopped" ? "stopped_by_user" : "done";
            if (_host.ProgressOf(rec.Id) is { } prog) { rec.Steps = prog.Steps; rec.CostUsd = Math.Round(prog.CostUsd, 4); }
            rec.Outcome = c.Status;
            rec.Summary = c.Summary;
            rec.TemporaryCredentials = c.TemporaryCredentials?.ToList();
            rec.Question = null;
            rec.Options = null;
            _open.Remove(rec.Id);
            _last = rec;
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
            lock (_lock) { if ((rec.Status != "running" && rec.Status != "waiting_for_user") || rec.Ver != startVersion) return; }
            try { await Task.Delay(250, ct); }
            catch (OperationCanceledException) { return; }
        }
    }

    private void Touch(McpTaskRecord rec)
    {
        rec.Ver++;
        rec.UpdatedUtc = DateTime.UtcNow;
        Save(rec);
    }

    private void Save(McpTaskRecord rec)
    {
        try
        {
            Directory.CreateDirectory(_dir);
            var copy = JsonSerializer.Deserialize<McpTaskRecord>(JsonSerializer.Serialize(rec))!;
            File.WriteAllText(Path.Combine(_dir, rec.Id + ".json"), JsonSerializer.Serialize(copy, JsonOpts));
        }
        catch { /* history is best effort */ }
    }

    /// <summary>Finds a task by id. Without an id: the only open task, or - when none is open - the latest one. Several open tasks and no id is a problem to report.</summary>
    private McpTaskRecord? Resolve(string? id, out bool fromDisk, out JsonObject? problem)
    {
        problem = null;
        if (string.IsNullOrWhiteSpace(id))
        {
            fromDisk = false;
            var open = OpenRecords();
            if (open.Count > 1)
            {
                problem = new JsonObject
                {
                    ["status"] = "error",
                    ["message"] = "Several tasks are open (" + string.Join(", ", open.Select(r => r.Id)) + "). Give the task_id of the one you mean.",
                    ["open_task_ids"] = new JsonArray(open.Select(r => (JsonNode)r.Id).ToArray())
                };
                return null;
            }
            if (open.Count == 1) return open[0];
            lock (_lock) return _last;
        }
        return Find(id, out fromDisk);
    }

    private static JsonObject KeyConflict(string key, string taskId) => new()
    {
        ["status"] = "idempotency_conflict",
        ["idempotency_key"] = key,
        ["task_id"] = taskId,
        ["message"] = "This idempotency_key belongs to a different task. No new task was started."
    };

    private McpTaskRecord? FindByIdempotencyKey(string requester, string key, out bool fromDisk)
    {
        fromDisk = false;
        lock (_lock)
        {
            var active = _open.Values.Select(l => l.Rec)
                .FirstOrDefault(r => r.Requester == requester && r.IdempotencyKey == key);
            if (active is not null) return active;
            if (_last?.Requester == requester && _last.IdempotencyKey == key) return _last;
        }
        try
        {
            if (Directory.Exists(_dir))
                foreach (var f in Directory.EnumerateFiles(_dir, "OF-*.json"))
                {
                    McpTaskRecord? r;
                    try { r = JsonSerializer.Deserialize<McpTaskRecord>(File.ReadAllText(f)); }
                    catch { continue; }
                    if (r?.Requester == requester && r.IdempotencyKey == key)
                    {
                        fromDisk = true;
                        return r;
                    }
                }
        }
        catch { }
        return null;
    }
    private McpTaskRecord? Find(string? id, out bool fromDisk)
    {
        fromDisk = false;
        if (string.IsNullOrWhiteSpace(id)) return null;
        lock (_lock)
        {
            if (_open.TryGetValue(id.Trim(), out var live)) return live.Rec;
            if (_last is not null && _last.Id == id.Trim()) return _last;
        }
        var safe = new string(id.Trim().Where(ch => char.IsLetterOrDigit(ch) || ch == '-').ToArray());
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

    /// <summary>One answer with everything that helps to find out what is wrong: version, runtime, open and recent tasks, and what the host knows.</summary>
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
            ["caller_answer_timeout_minutes"] = _answerTimeout.TotalMinutes,
            ["max_parallel_tasks"] = Math.Max(1, _host.MaxParallel)
        };

        var open = OpenRecords();
        var actives = new JsonArray();
        foreach (var r in open) actives.Add(Snapshot(r, null));
        o["active_tasks"] = actives;
        o["active_task"] = open.Count == 0 ? null : Snapshot(open[^1], null);

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
                ["idempotency_key"] = rec.IdempotencyKey,
                ["started"] = rec.CreatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                ["updated"] = rec.UpdatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
            };
            if (rec.ContinuedFrom is not null) o["continued_from"] = rec.ContinuedFrom;
            if (o0Url is not null) o["last_url"] = o0Url;
            if (rec.Outcome is not null) o["outcome"] = rec.Outcome;
            if (rec.Summary is not null) o["summary"] = rec.Summary;
            if (rec.TemporaryCredentials is { Count: > 0 })
                o["temporary_credentials"] = new JsonArray(rec.TemporaryCredentials.Select(c => (JsonNode)new JsonObject
                {
                    ["service"] = c.Service, ["login"] = c.Login, ["password"] = c.Password,
                    ["temporary"] = true, ["vault_saved"] = c.VaultSaved, ["change_immediately"] = true
                }).ToArray());
            if (rec.Question is not null) o["question"] = rec.Question;
            if (rec.Options is { Count: > 0 }) o["options"] = new JsonArray(rec.Options.Select(x => (JsonNode)x).ToArray());
            if (rec.Activity.Count > 0 && status is "running" or "needs_input" or "waiting_for_user")
                o["recent_activity"] = new JsonArray(rec.Activity.Select(x => (JsonNode)x).ToArray());
            int steps = rec.Steps; double? cost = rec.CostUsd; string? warn = null;
            if (!fromDisk && IsOpen(rec) && _host.ProgressOf(rec.Id) is { } live) { steps = live.Steps; cost = live.CostUsd; warn = live.Warning; }
            if (steps > 0) o["steps"] = steps;
            if (cost is { } cu) o["cost_usd"] = Math.Round(cu, 4);
            if (warn is not null) o["warning"] = warn;
            if (note is not null) o["note"] = note;
            var max = Math.Max(1, _host.MaxParallel);
            o["next_step"] = status switch
            {
                "running" => "OpenFill is still working. Call openfill_task_status with this task_id to wait for the result (each call waits up to 30 seconds). " +
                    (max > 1 ? $"Other tasks may run at the same time on other web sites (up to {max}); a site that another task is using cannot be used by a second one. " : "Do not start another task. ") +
                    "Do not cancel it just because it takes long: normal jobs take 10-30 steps (a simple form), 40-120 (a sign-up or a profile), 80-150 (a flight search with calendars); watch steps and recent_activity. " +
                    "Cancel only when recent_activity shows the same thing again and again. Cost is capped by OpenFill itself." +
                    (warn is null ? "" : " WARNING: " + warn + " Check recent_activity: if it repeats, cancel and tell the user; otherwise keep waiting."),
                "needs_input" => "OpenFill is asking its calling model for missing information. Use what you already know and answer the question using openfill_reply with this task_id. Authorized credentials, verification codes and login links may be supplied when permitted by your operating environment. If you cannot answer, say so clearly: OpenFill may then ask the person in its own window without restarting the task.",
                "waiting_for_user" => "OpenFill is waiting for the person at the computer to answer or complete an interactive authentication step in the OpenFill window; see question. " +
                    "This status is not answerable with openfill_reply; use that tool only if the status changes to needs_input. Do not ask the person to report back in chat or restart the task. " +
                    "Inform the person if attention is required, then monitor the existing task with openfill_task_status until its status changes.",
                "done" => "Finished. Tell the user the result from summary; outcome says whether everything was done (success) or only partly (partial).",
                "failed" => "The task failed. Tell the user why (summary). You may start a new task with more detail.",
                "blocked" => BlockedStep,
                "interrupted" => "The task was cut off before it finished. To pick it up, start a new task with continue_task_id=" + rec.Id + " (and the instructions for what is still to do): OpenFill reopens a tab at last_url, if known, and tells its browser agent what the earlier task was. Check what was already saved before repeating anything.",
                "stopped_by_user" or "stopped" => "The person at the OpenFill window stopped this task on purpose. Do NOT start it again, do NOT continue it with continue_task_id and do not repeat its work. Tell the person it was stopped and wait for their instruction in this chat. Only if they explicitly ask in this chat may you continue it. This does not affect other tasks, and a new, different task (also on the same site) may be started.",
                "cancelled" => "The task was cancelled.",
                _ => "Start a new task if the work is still needed."
            };
            return o;
        }
    }

    private static JsonObject Busy(IReadOnlyList<McpTaskRecord> open, int max)
    {
        var list = new JsonArray();
        foreach (var r in open) list.Add(new JsonObject { ["task_id"] = r.Id, ["task"] = Preview(r.TaskText), ["status"] = r.Status });
        return new JsonObject
        {
            ["status"] = "busy",
            ["open_task_id"] = open[0].Id,
            ["open_task"] = Preview(open[0].TaskText),
            ["open_task_status"] = open[0].Status,
            ["open_tasks"] = list,
            ["message"] = (max == 1
                ? "OpenFill runs one task at a time and task " + open[0].Id + " is still open."
                : $"OpenFill is already running {open.Count} tasks, the most it may run at once ({max}).") +
                " Wait for one with openfill_task_status, answer its question with openfill_reply, or cancel one with openfill_cancel_task if it is no longer needed. Then start the new task."
        };
    }

    private JsonObject NotFound(string? id)
    {
        var open = OpenRecords();
        var o = new JsonObject
        {
            ["status"] = "not_found",
            ["message"] = string.IsNullOrWhiteSpace(id) ? "There is no task yet. Start one with openfill_start_task." : "There is no task with id " + id + "."
        };
        if (open.Count > 0)
        {
            o["open_task_id"] = open[0].Id;
            o["open_task_ids"] = new JsonArray(open.Select(r => (JsonNode)r.Id).ToArray());
        }
        return o;
    }

    private static JsonObject Error(string message) => new() { ["status"] = "error", ["message"] = message };
}
