// OpenFill - Metadata: wersja 0.22, data 2026-10-14 22:59
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenFill.Core.Agent;
using OpenFill.Core.Cdp;
using OpenFill.Core.Config;
using OpenFill.Core.Logging;
using OpenFill.Core.Mcp;
using OpenFill.Core.Model;

namespace OpenFill.Core.Hosting;

/// <summary>
/// Shared "brain" of the app, independent of the window: connects the panel (IPanelTransport) to the tasks (TaskRun).
/// Handles panel messages (run, stop, answer, consent, key, settings), sends the panel
/// a header with version and date, live events, state and results. Shows model questions (ask_user / confirm_irreversible)
/// in the panel as cards; when no panel is connected, it uses a fallback channel (e.g. the console).
///
/// Several tasks run side by side (up to MaxParallel, 5 at most), each in its own browser tab with its own log and meter.
/// Costs are counted together per web domain. Only one task at a time may work on a given web domain (DomainLocks).
/// The same AppHost works in the CLI and in the Windows app; MCP calls StartRun/TryStart.
/// </summary>
public sealed class AppHost : IUserInteraction, IRunHost, IAsyncDisposable
{
    /// <summary>Hard upper limit of tasks at the same time.</summary>
    public const int AbsoluteMaxParallel = 5;

    /// <summary>Everything about one running task.</summary>
    private sealed class RunState
    {
        public required string Key { get; init; }
        public required string HistoryId { get; init; }
        public required int Number { get; init; }
        public required string Source { get; init; }
        public required string Task { get; init; }
        public required DateTime StartedUtc { get; init; }
        public RunOptions? Options { get; init; }
        public CancellationTokenSource Cts { get; } = new();
        public Task<RunConclusion>? Completion { get; set; }
        public TaskRun? Run { get; set; }
        public string? RunId { get; set; }
        public volatile string State = "running";   // running | waiting
        public bool Stopped { get; set; }
        public HistoryRecord Record { get; set; } = null!;
        public DateTime LastUrlSave { get; set; } = DateTime.MinValue;
        public string? Site { get; set; }
        public string? ConfirmNote { get; set; }
        public string? Who => Options?.Requester;
    }

    private readonly IPanelTransport _panel;
    private readonly IUserInteraction _fallback;
    private readonly SecretStore _secrets;
    private readonly bool _mock;
    private readonly Func<IModelClient>? _mockFactory;
    private readonly CdpSession _defaultCdp;
    private readonly LinkedList<string> _replay = new();
    private const int ReplayCapacity = 600;
    private DateTime _maintUntilUtc;
    private string? _maintReason, _maintBy;
    private readonly object _lock = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pendingAsks = new();
    private readonly ConcurrentDictionary<string, string> _openCards = new();
    private readonly ConcurrentDictionary<string, string?> _cardRun = new();   // card id -> task key (null: not tied to a task)
    private readonly Dictionary<string, RunState> _runs = new();                // task key -> running task
    private readonly Dictionary<string, RunState> _byRunId = new();
    private readonly ConcurrentDictionary<string, RunProgress> _finishedProgress = new();
    private readonly List<JsonObject> _recentResults = new();
    private int _runCounter;
    private readonly HistoryStore _history;
    private DateTime? _lastFinishedUtc;      // when the most recent task ended (any task, also from an earlier session of the app)
    private string? _lastFinishedStatus;

    public AppPaths Paths { get; }
    public AppConfig Config { get; }
    public OpenFillSession Session { get; }

    /// <summary>Who works on which web domain right now (one task at a time per domain).</summary>
    public DomainLocks Locks { get; } = new();

    /// <summary>true + text when a card in the panel needs the person (question, permission); false when no card is open any more. The window plays a sound.</summary>
    public event Action<bool, string>? Attention;

    /// <summary>Host-dependent actions (e.g. opening a folder in Explorer). Default: only a log event.</summary>
    public Action<string>? OpenFolder { get; set; }

    /// <summary>
    /// Shows a native "open file" dialog and returns the chosen full path (or null if cancelled).
    /// Provided by the Windows app; called on a background thread, so the implementation must marshal to the UI thread.
    /// </summary>
    public Func<string?>? PickFile { get; set; }

    public bool IsRunning { get { lock (_lock) return _runs.Count > 0; } }
    public int ActiveTaskCount { get { lock (_lock) return _runs.Count; } }
    public int WaitingTaskCount { get { lock (_lock) return _runs.Values.Count(r => r.State == "waiting"); } }

    /// <summary>How many tasks may run at once: the setting (1-5), but only one when the host has no tabs of its own for them.</summary>
    public int MaxParallel => Tabs is { SupportsParallel: true } ? Math.Clamp(Config.MaxParallel, 1, AbsoluteMaxParallel) : 1;

    public AppHost(AppPaths paths, AppConfig config, SecretStore secrets, CdpSession cdp, IPanelTransport panel,
        IUserInteraction? fallback = null, Func<IModelClient>? mockModelFactory = null)
    {
        Paths = paths;
        Config = config;
        _secrets = secrets;
        _panel = panel;
        _defaultCdp = cdp;
        _fallback = fallback ?? new HeadlessInteraction();
        _mockFactory = mockModelFactory;
        _mock = mockModelFactory is not null;
        _history = new HistoryStore(Path.Combine(paths.Root, "history", "tasks.ndjson"));
        try
        {
            var last = _history.Load(300, Array.Empty<string>()).Where(r => r.EndedUtc is not null).OrderByDescending(r => r.EndedUtc).FirstOrDefault();
            if (last is not null) { _lastFinishedUtc = last.EndedUtc; _lastFinishedStatus = last.Status; }
        }
        catch { }

        IModelClient model = _mock
            ? new FreshPerRunModelClient(mockModelFactory!)
            : new KeyedModelClient(() => _secrets.GetApiKey(), () => Config.OpenAIBaseUrl);

        Session = new OpenFillSession(paths, config, cdp, model, this);
        Session.Log.Published += OnLogEvent;
        Session.Costs.Changed += () => { try { Send(CostsMessage()); } catch { } };
        _panel.Received += OnPanelMessage;
    }

    public Task InitAsync(CancellationToken ct = default) => Session.InitAsync(ct);

    // ------------------------------------------------------------------ to the panel

    private void Send(JsonObject o) => _panel.Send(o.ToJsonString(EventLog.Ndjson));

    private void SendReplayable(JsonObject o)
    {
        var json = o.ToJsonString(EventLog.Ndjson);
        lock (_lock)
        {
            _replay.AddLast(json);
            while (_replay.Count > ReplayCapacity) _replay.RemoveFirst();
        }
        _panel.Send(json);
    }

    private RunState? StateOfRunId(string? runId)
    {
        if (runId is null) return null;
        lock (_lock) return _byRunId.TryGetValue(runId, out var st) ? st : null;
    }

    private void OnLogEvent(LogEvent ev)
    {
        var st = StateOfRunId(ev.RunId);
        if (st is not null && ev.Source == "tool" && ev.EventType == "call")
        {
            try { st.Options?.Activity?.Invoke(ev.Message); } catch { }
        }
        if (st is not null && ev.Source == "tool" && ev.EventType == "result" && Tabs is { } liveTabs
            && (DateTime.UtcNow - st.LastUrlSave).TotalSeconds >= 3)
        {
            st.LastUrlSave = DateTime.UtcNow;
            _ = liveTabs.RememberUrlAsync(st.Key);
        }
        var node = JsonSerializer.SerializeToNode(ev, EventLog.Ndjson);
        var o = new JsonObject { ["kind"] = "event", ["ev"] = node };
        if (st is not null) { o["n"] = st.Number; if (st.Who is { } who) o["who"] = who; }
        SendReplayable(o);
    }

    public JsonObject HeadlineMessage() => new()
    {
        ["kind"] = "headline",
        ["date"] = BuildInfo.Format(BuildInfo.VersionDateLocal),
        ["version"] = BuildInfo.Version,
        ["build"] = BuildInfo.Format(BuildInfo.BuildTimeLocal),
        ["instance"] = Paths.Instance
    };

    /// <summary>What the openfill_info tool shows about this app: settings, costs, tabs, shared folder, recent problems. No secrets, no page content.</summary>
    public JsonObject? Diagnostics()
    {
        var o = new JsonObject
        {
            ["instance"] = Paths.Instance,
            ["model"] = Config.Model,
            ["reasoning_effort"] = Config.ReasoningEffort,
            ["openai_key_present"] = !string.IsNullOrEmpty(_secrets.GetApiKey()),
            ["confirm_irreversible"] = Config.ConfirmIrreversible,
            ["limits"] = new JsonObject
            {
                ["max_parallel_tasks"] = MaxParallel,
                ["max_steps"] = Config.MaxSteps,
                ["step_extensions"] = Config.MaxStepExtensions,
                ["task_timeout_min"] = Config.TaskTimeoutMinutes,
                ["tool_timeout_sec"] = Config.ToolTimeoutSeconds,
                ["user_answer_wait_min"] = Config.UserAnswerTimeoutMinutes,
                ["caller_answer_wait_min"] = Config.CallerAnswerTimeoutMinutes,
                ["cost_notice_usd"] = Config.CostInfoUsd,
                ["cost_question_usd"] = Config.CostWarnUsd,
                ["domain_limit_usd"] = Config.DomainLimitUsd,
                ["domain_window_limit_usd"] = Config.DomainLifetimeLimitUsd,
                ["domain_window_days"] = Config.CostWindowDays
            }
        };

        var running = new JsonArray();
        foreach (var r in RunsSnapshot())
            running.Add(new JsonObject
            {
                ["task_id"] = r.Key,
                ["number"] = r.Number,
                ["source"] = r.Source,
                ["state"] = r.State,
                ["since"] = r.StartedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                ["sites"] = new JsonArray(Locks.DomainsOf(r.Key).Select(d => (JsonNode)d).ToArray())
            });
        o["running_tasks"] = running;
        lock (_lock)
        {
            if (_lastFinishedUtc is { } fin)
                o["last_task_finished"] = new JsonObject
                {
                    ["at"] = fin.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                    ["minutes_ago"] = (int)Math.Max(0, (DateTime.UtcNow - fin).TotalMinutes),
                    ["status"] = _lastFinishedStatus
                };
        }

        var sites = new JsonArray();
        foreach (var d in Session.Costs.List(Config.DomainLifetimeLimitUsd))
            sites.Add(new JsonObject
            {
                ["site"] = d.Domain,
                ["spent_usd"] = Math.Round(d.Spent, 4),
                ["window_usd"] = Math.Round(d.Lifetime, 4),
                ["blocked"] = d.Blocked,
                ["locked_until_window_passes"] = d.Locked
            });
        o["costs_per_site"] = sites;

        var tabs = new JsonArray();
        foreach (var t in Tabs?.Open() ?? Array.Empty<TabInfo>())
        {
            string? host = null;
            try { if (t.LastUrl is { Length: > 0 } lu && Uri.TryCreate(lu, UriKind.Absolute, out var tu)) host = tu.Host; } catch { }
            tabs.Add(new JsonObject { ["task"] = t.TaskKey, ["state"] = t.State, ["site"] = host });
        }
        o["tabs"] = new JsonObject { ["open"] = tabs.Count, ["limit"] = Config.MaxTabs, ["items"] = tabs };

        var shared = Paths.SharedFor(Config.SharedFolder);
        var sf = new JsonObject { ["path"] = shared, ["exists"] = Directory.Exists(shared) };
        try { if (Directory.Exists(shared)) sf["files"] = Directory.GetFiles(shared).Length; } catch { }
        o["shared_folder"] = sf;

        var data = new JsonObject { ["folder"] = Paths.Root, ["logs"] = Paths.Runs };
        try { data["free_disk_gb"] = Math.Round(new DriveInfo(Path.GetPathRoot(Paths.Root)!).AvailableFreeSpace / 1e9, 1); } catch { }
        o["data"] = data;
        o["resource_telemetry"] = new JsonObject
        {
            ["enabled"] = Config.ResourceTelemetryEnabled,
            ["interval_sec"] = Config.ResourceTelemetrySeconds,
            ["retention_days"] = Config.ResourceTelemetryRetentionDays,
            ["max_mb"] = Config.ResourceTelemetryMaxMb,
            ["folder"] = Paths.Telemetry,
            ["current_file"] = Path.Combine(Paths.Telemetry, "resources_" + DateTime.Now.ToString("yyyyMMdd") + ".ndjson")
        };
        o["maintenance"] = MaintenanceInfo();

        var mcp = new JsonObject
        {
            ["enabled"] = Config.McpEnabled,
            ["quick_tunnel"] = Config.McpTunnel,
            ["fixed_port"] = Config.McpPort,
            ["own_public_address_set"] = !string.IsNullOrWhiteSpace(Config.McpPublicBaseUrl)
        };
        o["mcp_settings"] = mcp;

        o["recent_problems"] = RecentProblems(8);
        return o;
    }

    private JsonArray RecentProblems(int max)
    {
        var arr = new JsonArray();
        try
        {
            var files = new DirectoryInfo(Paths.Runs).GetFiles("run_*.ndjson")
                .Where(f => !f.Name.EndsWith("_network.ndjson", StringComparison.Ordinal))
                .OrderByDescending(f => f.LastWriteTimeUtc).Take(3);
            foreach (var f in files)
            {
                var lines = File.ReadAllLines(f.FullName);
                for (var i = lines.Length - 1; i >= 0 && arr.Count < max; i--)
                {
                    var line = lines[i];
                    if (!line.Contains("\"status\":\"error\"") && !line.Contains("\"status\":\"warn\"")) continue;
                    JsonNode? n;
                    try { n = JsonNode.Parse(line); } catch { continue; }
                    var msg = n?["message"]?.GetValue<string>() ?? "";
                    arr.Add(new JsonObject
                    {
                        ["time"] = n?["tsUtc"]?.GetValue<string>(),
                        ["run"] = n?["runId"]?.GetValue<string>(),
                        ["level"] = n?["status"]?.GetValue<string>(),
                        ["source"] = n?["source"]?.GetValue<string>(),
                        ["message"] = msg.Length > 220 ? msg[..220] + "..." : msg
                    });
                }
                if (arr.Count >= max) break;
            }
        }
        catch { }
        return arr;
    }

    private JsonObject SettingsMessage() => new()
    {
        ["kind"] = "settings",
        ["model"] = Config.Model,
        ["effort"] = Config.ReasoningEffort,
        ["confirmIrreversible"] = Config.ConfirmIrreversible,
        ["maxTabs"] = Config.MaxTabs,
        ["maxParallel"] = Config.MaxParallel,
        ["sharedFolder"] = Config.SharedFolder,
        ["sharedFolderEffective"] = Paths.SharedFor(Config.SharedFolder),
        ["costInfo"] = Config.CostInfoUsd,
        ["costWarn"] = Config.CostWarnUsd,
        ["domainLimit"] = Config.DomainLimitUsd,
        ["domainLifetime"] = Config.DomainLifetimeLimitUsd,
        ["costWindowDays"] = Config.CostWindowDays,
        ["maxSteps"] = Config.MaxSteps,
        ["maxStepExtensions"] = Config.MaxStepExtensions,
        ["taskTimeoutMin"] = Config.TaskTimeoutMinutes,
        ["toolTimeoutSec"] = Config.ToolTimeoutSeconds,
        ["userAnswerMin"] = Config.UserAnswerTimeoutMinutes,
        ["callerAnswerMin"] = Config.CallerAnswerTimeoutMinutes,
        ["homeUrl"] = Config.HomeUrl,
        ["mcpEnabled"] = Config.McpEnabled,
        ["mcpTunnel"] = Config.McpTunnel,
        ["mcpPort"] = Config.McpPort,
        ["mcpPublicUrl"] = Config.McpPublicBaseUrl,
        ["keyMask"] = SecretStore.Mask(_secrets.GetApiKey()),
        ["keySource"] = _secrets.KeySource(),
        ["mock"] = _mock,
        ["paths"] = new JsonObject
        {
            ["logs"] = Paths.Logs,
            ["notes"] = Paths.Notes,
            ["downloads"] = Paths.Downloads
        }
    };

    private List<RunState> RunsSnapshot()
    {
        lock (_lock) return _runs.Values.OrderBy(r => r.Number).ToList();
    }

    /// <summary>All running tasks (for the panel's task list) and whether any is running.</summary>
    private JsonObject StateMessage()
    {
        var runs = new JsonArray();
        var list = RunsSnapshot();
        foreach (var r in list)
            runs.Add(new JsonObject
            {
                ["key"] = r.Key,
                ["n"] = r.Number,
                ["source"] = r.Source,
                ["task"] = OutputLimiter.Head(r.Task, 400),
                ["startedUtc"] = r.StartedUtc.ToString("o"),
                ["state"] = r.State,
                ["runId"] = r.RunId,
                ["who"] = r.Who,
                ["sites"] = new JsonArray(Locks.DomainsOf(r.Key).Select(d => (JsonNode)d).ToArray())
            });
        DateTime? lastEnd; string? lastStatus;
        lock (_lock) { lastEnd = _lastFinishedUtc; lastStatus = _lastFinishedStatus; }
        return new JsonObject
        {
            ["kind"] = "state",
            ["running"] = list.Count > 0,
            ["max"] = MaxParallel,
            ["runs"] = runs,
            ["lastFinishedUtc"] = lastEnd?.ToString("o"),
            ["lastFinishedStatus"] = lastStatus,
            ["maintenance"] = MaintenanceNode()
        };
    }

    private void SendState() => Send(StateMessage());

    private void SendHistory()
    {
        HashSet<string> current;
        lock (_lock) current = _runs.Values.Select(r => r.HistoryId).ToHashSet();
        Send(new JsonObject
        {
            ["kind"] = "history",
            ["items"] = JsonSerializer.SerializeToNode(_history.Load(200, current), EventLog.Ndjson)
        });
    }

    private void SendHistoryEvents(string runId)
    {
        var (events, truncated) = HistoryStore.LoadEvents(Paths.Runs, runId, 800);
        Send(new JsonObject
        {
            ["kind"] = "historyEvents",
            ["runId"] = runId,
            ["events"] = JsonSerializer.SerializeToNode(events, EventLog.Ndjson),
            ["truncated"] = truncated
        });
    }

    private void Notice(string text, string level = "info") =>
        Send(new JsonObject { ["kind"] = "notice", ["text"] = text, ["level"] = level });

    /// <summary>Full state for a newly connected (or reloaded) panel.</summary>
    private void SendHello()
    {
        Send(HeadlineMessage());
        Send(SettingsMessage());
        Send(CostsMessage());
        List<string> replay;
        lock (_lock) replay = _replay.ToList();
        foreach (var line in replay) _panel.Send(line);
        foreach (var card in _openCards.Values) _panel.Send(card);
        SendHistory();
        SendState();
        SendActiveTask();
        List<JsonObject> results;
        lock (_lock) results = _recentResults.ToList();
        foreach (var r in results) Send(r);
    }

    private JsonObject ResultMessage(RunState st, RunConclusion r, string finalStatus)
    {
        return new JsonObject
        {
            ["kind"] = "result",
            ["key"] = st.Key,
            ["n"] = st.Number,
            ["status"] = r.Status,
            ["summary"] = r.Summary,
            ["stopped"] = finalStatus == "stopped",
            ["source"] = st.Source,
            ["who"] = st.Who,
            ["task"] = OutputLimiter.Head(st.Task, 400),
            ["startedUtc"] = st.StartedUtc.ToString("o"),
            ["endedUtc"] = DateTime.UtcNow.ToString("o")
        };
    }

    // ------------------------------------------------------------------ from the panel

    private void OnPanelMessage(string json)
    {
        JsonObject? msg;
        try { msg = JsonNode.Parse(json) as JsonObject; } catch { return; }
        if (msg is null) return;
        var kind = msg["kind"]?.GetValue<string>() ?? "";
        try
        {
            switch (kind)
            {
                case "hello":
                    SendHello();
                    break;
                case "run":
                    var task = msg["task"]?.GetValue<string>() ?? "";
                    _ = TryStart(task, null);
                    break;
                case "stop":
                    Stop(msg["key"]?.GetValue<string>());
                    break;
                case "historyEvents":
                    var histRun = msg["runId"]?.GetValue<string>() ?? "";
                    _ = Task.Run(() => SendHistoryEvents(histRun));
                    break;
                case "answer":
                    ResolveAsk(msg["id"]?.GetValue<string>(), msg["text"]?.GetValue<string>() ?? "");
                    break;
                case "confirmReply":
                    var cnote = msg["note"]?.GetValue<string>();
                    ResolveAsk(msg["id"]?.GetValue<string>(), (msg["ok"]?.GetValue<bool>() == true ? "yes" : "no") + (string.IsNullOrWhiteSpace(cnote) ? "" : "\n" + cnote.Trim()));
                    break;
                case "maintenance":
                    Maintenance(msg["minutes"]?.GetValue<int>() ?? 0, null, "panel");
                    break;
                case "focusTask":
                    var focusKey = msg["key"]?.GetValue<string>();
                    if (!string.IsNullOrEmpty(focusKey) && Tabs is { } focusTabs) _ = focusTabs.ShowTaskAsync(focusKey);
                    break;
                case "pickFile":
                    var pickId = msg["id"]?.GetValue<string>() ?? "";
                    if (PickFile is null) { Notice("The file picker is available in the Windows app only. Type the path instead.", "warn"); break; }
                    _ = Task.Run(() =>
                    {
                        string? picked = null;
                        try { picked = PickFile(); } catch (Exception ex) { Notice("File dialog failed: " + ex.Message, "error"); }
                        Send(new JsonObject { ["kind"] = "filePicked", ["id"] = pickId, ["path"] = picked });
                    });
                    break;
                case "setKey":
                    var key = msg["key"]?.GetValue<string>() ?? "";
                    if (string.IsNullOrWhiteSpace(key)) { Notice("Empty key - nothing was saved.", "warn"); break; }
                    _secrets.SetApiKey(key);
                    Session.Log.Write("app", "settings", $"OpenAI key saved ({SecretStore.Mask(key)})");
                    Send(SettingsMessage());
                    Notice("Key saved.", "ok");
                    break;
                case "setSettings":
                    ApplySettings(msg);
                    break;
                case "unblock":
                    var unDomain = msg["domain"]?.GetValue<string>() ?? "";
                    if (Session.Costs.Unblock(unDomain, Config.DomainLifetimeLimitUsd))
                    {
                        Session.Log.Write("app", "cost-unblock", $"The user lifted the block on {unDomain}; its cost counts from zero again", status: "warn");
                        Notice($"{unDomain} is unblocked. Its cost counts from zero again.", "ok");
                    }
                    else
                        Notice($"{unDomain} stays blocked: it reached the cost ceiling of ${Config.DomainLifetimeLimitUsd:0.00} (counted over the last {Config.CostWindowDays} days). The oldest costs drop out of the window by themselves, or raise the ceiling in Settings.", "warn");
                    break;
                case "openFolder":
                    var which = msg["which"]?.GetValue<string>() ?? "logs";
                    var path = which switch
                    {
                        "notes" => Paths.Notes,
                        "downloads" => Paths.Downloads,
                        "shared" => Paths.SharedFor(Config.SharedFolder),
                        "runs" => Paths.Runs,
                        _ => Paths.Logs
                    };
                    if (which == "shared") { try { Directory.CreateDirectory(path); } catch { } }
                    if (OpenFolder is not null) OpenFolder(path);
                    else Notice("Folder: " + path);
                    break;
                case "navigate":
                    var url = msg["url"]?.GetValue<string>() ?? "";
                    if (ActiveTabBusy()) { Notice("A task is working in the tab that is open - navigation is blocked there. Pick another tab or wait.", "warn"); break; }
                    if (!string.IsNullOrWhiteSpace(url)) _ = Task.Run(() => Session.Browser.NavigateAsync(NormalizeUrl(url)));
                    break;
            }
        }
        catch (Exception ex)
        {
            Session.Log.Write("app", "error", $"Error handling message '{kind}': {ex.Message}", status: "error");
        }
    }

    private void ApplySettings(JsonObject msg)
    {
        if (msg["model"]?.GetValue<string>() is { Length: > 0 } m) Config.Model = m.Trim();
        if (msg["effort"]?.GetValue<string>() is { Length: > 0 } e) Config.ReasoningEffort = e.Trim();
        if (msg["confirmIrreversible"] is JsonValue ci) Config.ConfirmIrreversible = ci.GetValue<bool>();
        if (msg["maxTabs"] is JsonValue mt && mt.TryGetValue<int>(out var mtv)) Config.MaxTabs = Math.Clamp(mtv, 1, 12);
        if (Tabs is not null) Tabs.Limit = Config.MaxTabs;
        if (msg["maxParallel"] is JsonValue mp && mp.TryGetValue<int>(out var mpv)) Config.MaxParallel = Math.Clamp(mpv, 1, AbsoluteMaxParallel);
        if (msg["sharedFolder"] is JsonValue sfv && sfv.TryGetValue<string>(out var sharedValue)) Config.SharedFolder = sharedValue.Trim();
        if (msg["costInfo"] is JsonValue cinf && cinf.TryGetValue<double>(out var cinfV) && cinfV > 0) Config.CostInfoUsd = cinfV;
        if (msg["costWarn"] is JsonValue cwrn && cwrn.TryGetValue<double>(out var cwrnV) && cwrnV > 0) Config.CostWarnUsd = cwrnV;
        if (msg["domainLifetime"] is JsonValue dlife && dlife.TryGetValue<double>(out var dlifeV) && dlifeV > 0) Config.DomainLifetimeLimitUsd = dlifeV;
        if (msg["domainLimit"] is JsonValue dlim && dlim.TryGetValue<double>(out var dlimV) && dlimV > 0) Config.DomainLimitUsd = dlimV;
        if (msg["costWindowDays"] is JsonValue cwd && cwd.TryGetValue<int>(out var cwdV))
        {
            Config.CostWindowDays = Math.Clamp(cwdV, 1, 365);
            Session.Costs.WindowDays = Config.CostWindowDays;
        }
        if (msg["maxSteps"] is JsonValue msv && msv.TryGetValue<int>(out var msvV)) Config.MaxSteps = Math.Clamp(msvV, 5, 1000);
        if (msg["maxStepExtensions"] is JsonValue mse && mse.TryGetValue<int>(out var mseV)) Config.MaxStepExtensions = Math.Clamp(mseV, 0, 20);
        if (msg["taskTimeoutMin"] is JsonValue ttm && ttm.TryGetValue<int>(out var ttmV)) Config.TaskTimeoutMinutes = Math.Clamp(ttmV, 1, 600);
        if (msg["toolTimeoutSec"] is JsonValue tts && tts.TryGetValue<int>(out var ttsV)) Config.ToolTimeoutSeconds = Math.Clamp(ttsV, 5, 600);
        if (msg["userAnswerMin"] is JsonValue uam && uam.TryGetValue<int>(out var uamV)) Config.UserAnswerTimeoutMinutes = Math.Clamp(uamV, 1, 240);
        if (msg["callerAnswerMin"] is JsonValue cam && cam.TryGetValue<int>(out var camV)) Config.CallerAnswerTimeoutMinutes = Math.Clamp(camV, 1, 60);
        if (msg["homeUrl"] is JsonValue hu && hu.TryGetValue<string>(out var huV)) Config.HomeUrl = string.IsNullOrWhiteSpace(huV) ? "about:blank" : huV.Trim();
        if (msg["mcpEnabled"] is JsonValue mce) Config.McpEnabled = mce.GetValue<bool>();
        if (msg["mcpTunnel"] is JsonValue mct) Config.McpTunnel = mct.GetValue<bool>();
        if (msg["mcpPort"] is JsonValue mcp && mcp.TryGetValue<int>(out var mcpV)) Config.McpPort = Math.Clamp(mcpV, 0, 65535);
        if (msg["mcpPublicUrl"] is JsonValue mcu && mcu.TryGetValue<string>(out var mcuV)) Config.McpPublicBaseUrl = mcuV.Trim();
        Config.Save(Paths.ConfigFile);
        Session.Log.Write("app", "settings", $"Settings: model={Config.Model}, effort={Config.ReasoningEffort}, confirmations={(Config.ConfirmIrreversible ? "on" : "off")}, max tabs={Config.MaxTabs}, max parallel={Config.MaxParallel}, cost window={Config.CostWindowDays} d");
        Send(SettingsMessage());
        SendState();
        Notice("Settings saved.", "ok");
    }

    private JsonObject CostsMessage()
    {
        var items = new JsonArray();
        foreach (var d in Session.Costs.List(Config.DomainLifetimeLimitUsd))
            items.Add(new JsonObject { ["domain"] = d.Domain, ["spent"] = Math.Round(d.Spent, 4), ["lifetime"] = Math.Round(d.Lifetime, 4), ["blocked"] = d.Blocked, ["locked"] = d.Locked });
        return new JsonObject { ["kind"] = "costs", ["limit"] = Config.DomainLimitUsd, ["items"] = items };
    }

    public string? CheckBlocked(string task)
    {
        var d = Session.Costs.BlockedIn(task);
        return d is null ? null
            : $"The site {d} has reached its hard cost limit (${Config.DomainLimitUsd:0.00}) and is blocked in OpenFill. Only the user can lift this, in the OpenFill window. Tell the user; do not retry and do not start tasks on this site.";
    }

    public RunProgress? ProgressOf(string taskKey)
    {
        RunState? st;
        lock (_lock) _runs.TryGetValue(taskKey, out st);
        if (st?.Run is { } run) return run.Meter.Snapshot();
        return _finishedProgress.TryGetValue(taskKey, out var p) ? p : null;
    }

    public static string NormalizeUrl(string url)
    {
        url = url.Trim();
        if (url.Contains("://") || url.StartsWith("about:")) return url;
        return "https://" + url;
    }

    // ------------------------------------------------------------------ tasks

    /// <summary>Browser tabs (one per task). Set by the Windows app; without it the host works on the single page it was given.</summary>
    public TabManager? Tabs
    {
        get => _tabs;
        set
        {
            if (_tabs is not null) _tabs.ActiveTaskChanged -= OnActiveTaskChanged;
            _tabs = value;
            if (_tabs is not null) _tabs.ActiveTaskChanged += OnActiveTaskChanged;
        }
    }
    private TabManager? _tabs;

    // The panel shows the log of the task whose tab is in front: it is told which task (its number) that is.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> _numberByKey = new();
    private string? _activeTaskKey;

    private void OnActiveTaskChanged(string? taskKey)
    {
        lock (_lock) _activeTaskKey = taskKey;
        SendActiveTask();
    }

    private void SendActiveTask()
    {
        string? key; lock (_lock) key = _activeTaskKey;
        var o = new JsonObject { ["kind"] = "activeTask" };
        if (key is not null && _numberByKey.TryGetValue(key, out var n)) o["n"] = n;
        Send(o);
    }

    private bool ActiveTabBusy() => Tabs is { } t ? t.ActiveTabBusy() : IsRunning;

    public string? LastUrlOf(string taskKey) => Tabs?.Find(taskKey)?.LastUrl;

    /// <summary>Short title for the tab: the caller's title, otherwise the start of the task text (without the start-page line).</summary>
    private static string ShortLabel(string task, string? title)
    {
        var t = string.IsNullOrWhiteSpace(title)
            ? System.Text.RegularExpressions.Regex.Replace(task, @"^\s*Start from this page:\s*\S+\s*", "")
            : title;
        t = System.Text.RegularExpressions.Regex.Replace(t, @"\s+", " ").Trim();
        if (t.Length <= 32) return t;
        var cut = t.LastIndexOf(' ', 31);
        return (cut >= 12 ? t[..cut] : t[..31]) + "...";
    }

    /// <summary>Tab name: the site first (so tabs are easy to tell apart), then the short title.</summary>
    private static string FullLabel(string? site, string shortLabel)
    {
        if (string.IsNullOrEmpty(site)) return shortLabel;
        return (site.StartsWith("www.") ? site[4..] : site) + " - " + shortLabel;
    }

    /// <summary>The text the inner model gets: for a task that continues an earlier one, a short account of that task and of the tab.</summary>
    public static string BuildPrompt(string task, RunOptions? options, TabStart start)
    {
        if (options?.ContinueFromKey is null && options?.PriorContext is null) return task;
        var note = start.Reopened
            ? $"The earlier tab had been closed, so a new tab was opened at {start.Url}; anything typed into forms there is gone, but you are still logged in."
            : start.Continued
                ? "The browser tab of that task is still open, exactly as it was left."
                : "The earlier tab is no longer known; this is a new tab.";
        var prior = string.IsNullOrWhiteSpace(options?.PriorContext) ? "" : options!.PriorContext!.Trim() + " ";
        return "Context: this task continues an earlier one. " + prior + note + " Look at the page first (get_page) before you act.\n\nTask: " + task;
    }

    public Task<RunConclusion>? StartRun(string task) => StartRun(task, null);

    public Task<RunConclusion>? StartRun(string task, RunOptions? options) => TryStart(task, options).Run;

    /// <summary>
    /// Starts a task in the background. Refuses (with the reason) when the text is empty, the key is missing, the limit of parallel tasks
    /// is reached, or another task is working on one of the web sites the task is about.
    /// </summary>
    /// <summary>True while the host takes no new tasks (maintenance, for example before an update). It expires by itself; a restart clears it.</summary>
    private bool MaintenanceOn(out TimeSpan left)
    {
        lock (_lock) { left = _maintUntilUtc - DateTime.UtcNow; return left > TimeSpan.Zero; }
    }

    private JsonNode? MaintenanceNode()
    {
        lock (_lock)
        {
            if (_maintUntilUtc <= DateTime.UtcNow) return null;
            return new JsonObject { ["untilUtc"] = _maintUntilUtc.ToString("o"), ["reason"] = _maintReason, ["by"] = _maintBy };
        }
    }

    private JsonObject MaintenanceInfo()
    {
        var on = MaintenanceOn(out var left);
        int running; lock (_lock) running = _runs.Count;
        var o = new JsonObject { ["active"] = on, ["running_tasks"] = running };
        if (on)
        {
            o["minutes_left"] = (int)Math.Ceiling(left.TotalMinutes);
            o["until_local"] = DateTime.Now.Add(left).ToString("HH:mm");
            o["reason"] = _maintReason;
            o["set_by"] = _maintBy;
        }
        return o;
    }

    /// <summary>Sets (minutes &gt; 0), ends (0) or reports (null) the maintenance block: no new tasks, running ones finish.</summary>
    public JsonObject Maintenance(int? minutes, string? reason, string? by)
    {
        if (minutes is { } m)
        {
            lock (_lock)
            {
                if (m <= 0) { _maintUntilUtc = default; _maintReason = null; _maintBy = null; }
                else
                {
                    _maintUntilUtc = DateTime.UtcNow.AddMinutes(Math.Clamp(m, 1, 120));
                    _maintReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
                    _maintBy = string.IsNullOrWhiteSpace(by) ? null : by.Trim();
                }
            }
            Session.Log.Write("app", "maintenance", m <= 0 ? "Maintenance ended: new tasks are accepted again"
                : $"Maintenance for {Math.Clamp(m, 1, 120)} min: no new tasks" + (string.IsNullOrWhiteSpace(reason) ? "" : " (" + reason.Trim() + ")"), status: "warn");
            SendState();
        }
        return MaintenanceInfo();
    }

    public StartResult TryStart(string task, RunOptions? options)
    {
        task = task.Trim();
        if (task.Length == 0)
        {
            Notice("Enter a task.", "warn");
            return new StartResult(null, new RunRefusal("empty", "The task text is empty."));
        }
        if (MaintenanceOn(out var maintLeft))
        {
            var maintText = $"OpenFill is not accepting new tasks right now ({_maintReason ?? "maintenance"}), about {Math.Ceiling(maintLeft.TotalMinutes)} more minute(s). Existing tasks may continue until the local operator closes OpenFill for an upgrade; at that point their status will become interrupted and they may be safely recovered after reconnecting.";
            Notice(maintText, "warn");
            return new StartResult(null, new RunRefusal("maintenance", maintText));
        }
        if (!_mock && string.IsNullOrWhiteSpace(_secrets.GetApiKey()))
        {
            Notice("No OpenAI key. Open Settings in the panel and paste your key.", "error");
            return new StartResult(null, new RunRefusal("no_key", "OpenFill has no OpenAI key. The user has to add it in the OpenFill window (Settings)."));
        }

        RunState st;
        lock (_lock)
        {
            var max = MaxParallel;
            if (_runs.Count >= max)
            {
                var msg = max == 1
                    ? "A task is already running. Stop it or wait."
                    : $"{_runs.Count} tasks are running, which is the maximum ({max}). Wait for one to finish or stop one.";
                Notice(msg, "warn");
                return new StartResult(null, new RunRefusal("limit", msg));
            }

            var historyId = "H" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..4];
            var key = options?.TaskKey ?? historyId;
            if (_runs.ContainsKey(key))
                return new StartResult(null, new RunRefusal("other", "A task with this id is already running."));
            var source = options?.Ask is null ? "panel" : "mcp";
            var shownTask = task;

            // The web sites the task certainly works on at the start: the page it is told to start from (the panel's address field),
            // or where the task it continues left off. A site only mentioned in the text (something to type, a name in a sentence)
            // does NOT lock anything: the lock is taken when the task really goes there (navigate, redirect), see IDomainGate.
            var domains = new List<string>();
            if (System.Text.RegularExpressions.Regex.Match(task, @"^\s*Start from this page:\s*(\S+)") is { Success: true } startPage
                && CostGuard.DomainOf(startPage.Groups[1].Value) is { Length: > 0 } startDomain)
                domains.Add(startDomain);
            if (options?.ContinueFromKey is { } cont && Tabs?.Find(cont)?.LastUrl is { } lastUrl && CostGuard.DomainOf(lastUrl) is { Length: > 0 } cd && !domains.Contains(cd))
                domains.Add(cd);
            if (Locks.TryAcquire(key, source, shownTask, domains) is { } holder)
            {
                var text = DomainLocks.RefusalText(holder);
                Notice(text, "warn");
                Session.Log.Write("app", "domain-busy", $"Task refused: {holder.Domain} is in use by task {holder.TaskKey}", status: "warn");
                return new StartResult(null, new RunRefusal("domain_busy", text));
            }

            st = new RunState
            {
                Key = key,
                HistoryId = historyId,
                Number = ++_runCounter,
                Source = source,
                Task = shownTask,
                StartedUtc = DateTime.UtcNow,
                Options = options
            };
            st.Site = domains.FirstOrDefault();
            st.Record = new HistoryRecord(historyId, source, shownTask, st.StartedUtc, null, "running", null, null, st.Who);
            _runs[key] = st;
            _numberByKey[key] = st.Number;
            _finishedProgress.TryRemove(key, out _);
            _history.Append(st.Record);
            st.Completion = Task.Run(() => RunAsync(st, task, options));
        }
        SendState();
        SendHistory();
        return new StartResult(st.Completion, null);
    }

    private async Task<RunConclusion> RunAsync(RunState st, string task, RunOptions? options)
    {
        var ct = st.Cts.Token;
        RunConclusion result;
        var tabBegun = false;
        var tabs = Tabs;
        TaskRun? run = null;
        try
        {
            var prompt = task;
            var cdp = _defaultCdp;
            TabStart? start = null;
            if (tabs is not null)
            {
                var shortLabel = ShortLabel(task, options?.Title);
                start = await tabs.BeginAsync(st.Key, FullLabel(st.Site, shortLabel), options?.ContinueFromKey, ct, shortLabel, st.Who);
                tabBegun = true;
                if (tabs.ConnectionOf(start.TabId) is { } conn) cdp = new CdpSession(conn);
            }

            run = Session.CreateRun(cdp, new RunInteraction(this, st), new Gate(this, st), _mock ? _mockFactory!() : null);
            st.Run = run;
            var runId = run.Begin();
            lock (_lock)
            {
                st.RunId = runId;
                _byRunId[runId] = st;
                st.Record = st.Record with { RunId = runId };
            }
            _history.Append(st.Record);
            SendState();

            if (start is not null)
            {
                // Point the core at the tab (events subscribed per task, per-tab enables and scripts).
                await run.Browser.InitAsync(ct);
                if (start.Reopened) { try { await run.Browser.WaitStableAsync(ct: ct); } catch (OperationCanceledException) { throw; } catch { } }
                run.Log.Write("app", "tab",
                    start.Reopened ? $"Task tab: reopened at {start.Url}" : start.Continued ? "Task tab: the earlier tab, as it was left" : "Task tab: a new tab",
                    new { taskKey = st.Key, start.TabId });
                prompt = BuildPrompt(task, options, start);
            }
            else
                await run.Browser.InitAsync(ct);
            result = await run.FillAsync(prompt, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            result = new RunConclusion("failed", "The task was stopped.", null);
        }
        catch (Exception ex)
        {
            Session.Log.Write("app", "error", "Unexpected error: " + ex.Message, status: "error");
            result = new RunConclusion("failed", "Unexpected error: " + ex.Message, null);
        }
        finally
        {
            CancelOpenCards(st.Key);
            Locks.ReleaseAll(st.Key);
            try { run?.Detach(); } catch { }
            if (tabBegun && tabs is not null) { try { await tabs.EndAsync(st.Key); } catch { } }
        }

        // A stop by the person at the OpenFill window is final: it is reported as "stopped", not as a failure,
        // so a caller (ChatGPT) does not start the task again on its own.
        if (st.Stopped && ct.IsCancellationRequested)
            result = new RunConclusion("stopped", "The task was stopped by the person at the OpenFill window, on purpose.", result.SiteKnowledge);
        var finalStatus = ct.IsCancellationRequested ? "stopped" : result.Status;
        if (run is not null) _finishedProgress[st.Key] = run.Meter.Snapshot();
        TrimFinishedProgress();
        var resultMsg = ResultMessage(st, result, finalStatus);
        lock (_lock)
        {
            st.Record = st.Record with
            {
                EndedUtc = DateTime.UtcNow,
                Status = finalStatus,
                Summary = result.Summary ?? ""
            };
            _history.Append(st.Record);
            _lastFinishedUtc = st.Record.EndedUtc;
            _lastFinishedStatus = finalStatus;
            _runs.Remove(st.Key);
            if (st.RunId is not null) _byRunId.Remove(st.RunId);
            _recentResults.Add(resultMsg);
            while (_recentResults.Count > 5) _recentResults.RemoveAt(0);
        }
        st.Cts.Dispose();
        SendState();
        Send(resultMsg);
        SendHistory();
        return result;
    }

    private void TrimFinishedProgress()
    {
        if (_finishedProgress.Count <= 60) return;
        foreach (var k in _finishedProgress.Keys.Take(_finishedProgress.Count - 40)) _finishedProgress.TryRemove(k, out _);
    }

    /// <summary>Single task entry point (CLI, tests): task in, result out.</summary>
    public async Task<RunConclusion> FillAsync(string task)
    {
        var t = StartRun(task) ?? throw new InvalidOperationException("Cannot start a task now (another one is running, or no API key).");
        return await t;
    }

    /// <summary>Stops one task (by its key), or all of them when the key is null.</summary>
    public void Stop(string? taskKey)
    {
        List<RunState> targets;
        lock (_lock)
        {
            targets = taskKey is null
                ? _runs.Values.ToList()
                : _runs.TryGetValue(taskKey, out var one) ? new List<RunState> { one } : _runs.Values.Where(r => r.HistoryId == taskKey).ToList();
        }
        foreach (var st in targets)
        {
            st.Stopped = true;
            (st.Run?.Log ?? Session.Log).Write("app", "stop", "Stopped at the user's request", status: "warn");
            try { st.Cts.Cancel(); } catch { }
            CancelOpenCards(st.Key);
        }
    }

    public void Stop() => Stop((string?)null);

    /// <summary>Screenshot of a task's browser tab (the newest running task's, or the open tab) saved under screenshots\ (MCP serves it as a private link). Keeps the newest 60 files.</summary>
    public async Task<string?> CaptureScreenshotAsync(string? taskKey, CancellationToken ct)
    {
        Browser.BrowserController? b = null;
        RunState? st;
        lock (_lock)
        {
            st = taskKey is not null
                ? (_runs.TryGetValue(taskKey, out var one) ? one : null)
                : _runs.Values.OrderByDescending(r => r.Number).FirstOrDefault();
        }
        if (st?.Run is { } run) b = run.Browser;
        else if (taskKey is not null && Tabs?.Find(taskKey)?.TabId is { } tabId && Tabs.ConnectionOf(tabId) is { } conn)
            b = new Browser.BrowserController(new CdpSession(conn), Session.Log);
        b ??= Session.Browser;
        var b64 = await b.ScreenshotJpegBase64Async(75, ct);
        if (b64 is null) return null;
        var name = $"shot-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..8]}.jpg";
        Directory.CreateDirectory(Paths.Screenshots);
        await File.WriteAllBytesAsync(Path.Combine(Paths.Screenshots, name), Convert.FromBase64String(b64), ct);
        try
        {
            foreach (var old in new DirectoryInfo(Paths.Screenshots).GetFiles("shot-*.jpg").OrderByDescending(f => f.LastWriteTimeUtc).Skip(60))
                old.Delete();
        }
        catch { }
        Session.Log.Write("app", "screenshot", $"Screenshot saved for the MCP caller ({name})", status: "info");
        return name;
    }

    // ------------------------------------------------------------------ what a task gets

    /// <summary>The task's view of the person and of its caller: questions go to its own caller (MCP) or to a card that names the task.</summary>
    private sealed class RunInteraction(AppHost host, RunState st) : IUserInteraction
    {
        public Task<string> AskAsync(string question, IReadOnlyList<string>? options, CancellationToken ct) => host.AskForAsync(st, question, options, ct, human: false);
        public Task<string> AskHumanAsync(string question, IReadOnlyList<string>? options, CancellationToken ct) => host.AskForAsync(st, question, options, ct, human: true);
        public Task<bool> ConfirmAsync(string what, CancellationToken ct) => host.ConfirmForAsync(st, what, ct);
        public string? TakeConfirmNote() { var n = st.ConfirmNote; st.ConfirmNote = null; return n; }
    }

    /// <summary>Lets the task onto a web site, or says who is there.</summary>
    private sealed class Gate(AppHost host, RunState st) : IDomainGate
    {
        public string? TryEnter(string domain)
        {
            if (string.IsNullOrEmpty(domain)) return null;
            var holder = host.Locks.TryAcquireOne(st.Key, st.Source, st.Task, domain);
            if (holder is null) return null;
            host.Session.Log.Write("app", "domain-busy", $"{domain} is in use by task {holder.TaskKey}; task {st.Key} may not go there", status: "warn");
            return DomainLocks.RefusalText(holder, midTask: true);
        }
    }

    // ------------------------------------------------------------------ IUserInteraction (outside any task) and cards

    public Task<string> AskAsync(string question, IReadOnlyList<string>? options, CancellationToken ct) => AskForAsync(null, question, options, ct, human: false);
    public Task<string> AskHumanAsync(string question, IReadOnlyList<string>? options, CancellationToken ct) => AskForAsync(null, question, options, ct, human: true);
    public Task<bool> ConfirmAsync(string what, CancellationToken ct) => ConfirmForAsync(null, what, ct);

    private JsonObject TaskTag(JsonObject card, RunState? st)
    {
        if (st is not null)
        {
            card["n"] = st.Number;
            card["key"] = st.Key;
            if (st.Who is { } cardWho) card["who"] = cardWho;
            card["taskLabel"] = OutputLimiter.Head(st.Options?.Title is { Length: > 0 } tt ? tt : st.Task.ReplaceLineEndings(" "), 90);
        }
        return card;
    }

    private async Task<string> AskForAsync(RunState? st, string question, IReadOnlyList<string>? options, CancellationToken ct, bool human)
    {
        var log = st?.Run?.Log ?? Session.Log;
        if (!human && st?.Options?.Ask is { } routed)
        {
            log.Write("app", "ask", "Question for the caller (MCP): " + question, status: "warn");
            return await routed(question, options, ct);
        }
        if (!_panel.HasClients)
            return human ? await _fallback.AskHumanAsync(question, options, ct) : await _fallback.AskAsync(question, options, ct);
        var card = TaskTag(new JsonObject
        {
            ["kind"] = "ask",
            ["id"] = NewId(),
            ["question"] = question,
            ["options"] = new JsonArray((options ?? Array.Empty<string>()).Select(o => (JsonNode)o).ToArray())
        }, st);
        if (human) card["human"] = true;
        log.Write("app", "ask", (human ? "Question only the user can answer: " : "Question for the user: ") + question, status: "warn");
        if (human) { try { st?.Options?.UserWaiting?.Invoke(question); } catch { } }
        try
        {
            var ans = await WaitForCard(card, st, ct);
            return ans ?? "(no answer from the user within the time limit - carry on on your own if that is safe)";
        }
        finally
        {
            if (human) { try { st?.Options?.UserWaiting?.Invoke(null); } catch { } }
        }
    }

    private async Task<bool> ConfirmForAsync(RunState? st, string what, CancellationToken ct)
    {
        var log = st?.Run?.Log ?? Session.Log;
        if (!_panel.HasClients) return await _fallback.ConfirmAsync(what, ct);
        var card = TaskTag(new JsonObject { ["kind"] = "confirm", ["id"] = NewId(), ["what"] = what }, st);
        log.Write("app", "confirm", "Permission requested: " + what, status: "warn");
        var ans = await WaitForCard(card, st, ct);
        var ok = ans is not null && (ans == "yes" || ans.StartsWith("yes\n", StringComparison.Ordinal));
        if (st is not null && ans is not null && ans.IndexOf('\n') is var noteAt and >= 0) st.ConfirmNote = ans[(noteAt + 1)..].Trim();
        log.Write("app", "confirm", ok ? "User confirmed" : "Not confirmed", status: ok ? "ok" : "warn");
        return ok;
    }

    private async Task<string?> WaitForCard(JsonObject card, RunState? st, CancellationToken ct)
    {
        var id = card["id"]!.GetValue<string>();
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingAsks[id] = tcs;
        _cardRun[id] = st?.Key;
        var json = card.ToJsonString(EventLog.Ndjson);
        _openCards[id] = json;
        _panel.Send(json);
        if (st is not null) { st.State = "waiting"; Tabs?.SetState(st.Key, "waiting"); SendState(); }
        try { Attention?.Invoke(true, card["question"]?.GetValue<string>() ?? card["what"]?.GetValue<string>() ?? ""); } catch { }
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromMinutes(Config.UserAnswerTimeoutMinutes));
            using var reg = timeout.Token.Register(() => tcs.TrySetCanceled());
            try { return await tcs.Task; }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return null; }
        }
        finally
        {
            _pendingAsks.TryRemove(id, out _);
            _openCards.TryRemove(id, out _);
            _cardRun.TryRemove(id, out _);
            Send(new JsonObject { ["kind"] = "closeCard", ["id"] = id });
            if (st is not null && !_cardRun.Values.Any(k => k == st.Key))
            {
                st.State = "running";
                Tabs?.SetState(st.Key, "running");
                SendState();
            }
            if (_pendingAsks.IsEmpty)
            {
                try { Attention?.Invoke(false, ""); } catch { }
            }
        }
    }

    private void ResolveAsk(string? id, string answer)
    {
        var accepted = id is not null && _pendingAsks.TryGetValue(id, out var tcs) && tcs.TrySetResult(answer);
        // Acknowledge delivery only. It does NOT mean the agent successfully performed the request.
        Send(new JsonObject { ["kind"] = "answerReceived", ["id"] = id, ["accepted"] = accepted });
        Session.Log.Write("panel", accepted ? "user-message-received" : "user-message-rejected",
            accepted ? "The user's message was delivered to the current task." :
                       "The task was no longer accepting user input; the message was not delivered.",
            status: accepted ? "ok" : "warn");
    }

    /// <summary>Closes the open cards of one task (or all cards when the key is null).</summary>
    private void CancelOpenCards(string? taskKey = null)
    {
        foreach (var kv in _pendingAsks)
        {
            if (taskKey is not null && !(_cardRun.TryGetValue(kv.Key, out var owner) && owner == taskKey)) continue;
            kv.Value.TrySetCanceled();
        }
    }

    private static string NewId() => "q" + Guid.NewGuid().ToString("N")[..8];

    public async ValueTask DisposeAsync()
    {
        Stop((string?)null);
        try
        {
            List<Task> all;
            lock (_lock) all = _runs.Values.Select(r => (Task?)r.Completion).Where(t => t is not null).Select(t => t!).ToList();
            if (all.Count > 0) await Task.WhenAny(Task.WhenAll(all), Task.Delay(3000));
        }
        catch { }
        _panel.Received -= OnPanelMessage;
        await Session.DisposeAsync();
    }

    /// <summary>The mock has state (a phase), so each task gets a fresh instance.</summary>
    private sealed class FreshPerRunModelClient(Func<IModelClient> factory) : IModelClient
    {
        private IModelClient _current = factory();
        private bool _sawFirst;

        public Task<JsonObject> CreateResponseAsync(JsonObject request, CancellationToken ct = default)
        {
            // New task = input with a single item (the user message).
            var input = request["input"] as JsonArray;
            if (_sawFirst && input is { Count: 1 }) _current = factory();
            _sawFirst = true;
            return _current.CreateResponseAsync(request, ct);
        }
    }
}
