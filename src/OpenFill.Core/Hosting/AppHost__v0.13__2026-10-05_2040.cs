// OpenFill - Metadata: wersja 0.13, data 2026-10-05 20:40
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
/// Shared "brain" of the app, independent of the window: connects the panel (IPanelTransport) to the session (OpenFillSession).
/// Handles panel messages (run, stop, answer, consent, key, settings), sends the panel
/// a header with version and date, live events, state and result. Shows model questions (ask_user / confirm_irreversible)
/// in the panel as cards; when no panel is connected, it uses a fallback channel (e.g. the console).
///
/// The same AppHost works in the CLI and in the Windows app; later MCP simply calls FillAsync.
/// </summary>
public sealed class AppHost : IUserInteraction, IRunHost, IAsyncDisposable
{
    private readonly IPanelTransport _panel;
    private readonly IUserInteraction _fallback;
    private readonly SecretStore _secrets;
    private readonly bool _mock;
    private readonly Func<IModelClient>? _mockFactory;
    private readonly LinkedList<string> _replay = new();
    private const int ReplayCapacity = 600;
    private readonly object _lock = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pendingAsks = new();
    private readonly ConcurrentDictionary<string, string> _openCards = new();
    private CancellationTokenSource? _runCts;
    private Task<RunConclusion>? _runTask;
    private RunConclusion? _lastResult;
    private readonly HistoryStore _history;
    private HistoryRecord? _current;

    public AppPaths Paths { get; }
    public AppConfig Config { get; }
    public OpenFillSession Session { get; }

    /// <summary>While set (an MCP task is open), ask_user questions go to the MCP caller instead of the panel.</summary>
    public AskHandler? AskInterceptor { get; set; }

    /// <summary>Short descriptions of the inner model's tool calls, for MCP progress reports.</summary>
    public event Action<string>? Activity;

    /// <summary>Question (non-null) while the inner model waits for a question only the person at the computer can answer; null when that is over.</summary>
    public event Action<string?>? UserWaiting;

    /// <summary>true + text when a card in the panel needs the person (question, permission); false when no card is open any more. The window plays a sound.</summary>
    public event Action<bool, string>? Attention;

    /// <summary>Host-dependent actions (e.g. opening a folder in Explorer). Default: only a log event.</summary>
    public Action<string>? OpenFolder { get; set; }

    /// <summary>
    /// Shows a native "open file" dialog and returns the chosen full path (or null if cancelled).
    /// Provided by the Windows app; called on a background thread, so the implementation must marshal to the UI thread.
    /// </summary>
    public Func<string?>? PickFile { get; set; }

    public bool IsRunning { get { lock (_lock) return _runTask is { IsCompleted: false }; } }

    public AppHost(AppPaths paths, AppConfig config, SecretStore secrets, CdpSession cdp, IPanelTransport panel,
        IUserInteraction? fallback = null, Func<IModelClient>? mockModelFactory = null)
    {
        Paths = paths;
        Config = config;
        _secrets = secrets;
        _panel = panel;
        _fallback = fallback ?? new HeadlessInteraction();
        _mockFactory = mockModelFactory;
        _mock = mockModelFactory is not null;
        _history = new HistoryStore(Path.Combine(paths.Root, "history", "tasks.ndjson"));

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

    private void OnLogEvent(LogEvent ev)
    {
        if (ev.Source == "tool" && ev.EventType == "call") Activity?.Invoke(ev.Message);
        if (ev.Source == "tool" && ev.EventType == "result" && _tabKey is { } liveKey && Tabs is { } liveTabs
            && (DateTime.UtcNow - _lastUrlSave).TotalSeconds >= 3)
        {
            _lastUrlSave = DateTime.UtcNow;
            _ = liveTabs.RememberUrlAsync(liveKey);
        }
        if (ev.RunId is not null)
        {
            // The current task learns its run id from the first event of its run (links the history entry to the run file).
            HistoryRecord? updated = null;
            lock (_lock)
            {
                if (_current is { RunId: null, EndedUtc: null } cur) { updated = cur with { RunId = ev.RunId }; _current = updated; }
            }
            if (updated is not null) _history.Append(updated);
        }
        var node = JsonSerializer.SerializeToNode(ev, EventLog.Ndjson);
        SendReplayable(new JsonObject { ["kind"] = "event", ["ev"] = node });
    }

    public JsonObject HeadlineMessage() => new()
    {
        ["kind"] = "headline",
        ["date"] = BuildInfo.Format(BuildInfo.VersionDateLocal),
        ["version"] = BuildInfo.Version,
        ["build"] = BuildInfo.Format(BuildInfo.BuildTimeLocal),
        ["instance"] = Paths.Instance
    };

    private JsonObject SettingsMessage() => new()
    {
        ["kind"] = "settings",
        ["model"] = Config.Model,
        ["effort"] = Config.ReasoningEffort,
        ["confirmIrreversible"] = Config.ConfirmIrreversible,
        ["maxTabs"] = Config.MaxTabs,
        ["sharedFolder"] = Config.SharedFolder,
        ["sharedFolderEffective"] = Paths.SharedFor(Config.SharedFolder),
        ["costInfo"] = Config.CostInfoUsd,
        ["costWarn"] = Config.CostWarnUsd,
        ["domainLimit"] = Config.DomainLimitUsd,
        ["domainLifetime"] = Config.DomainLifetimeLimitUsd,
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

    private JsonObject StateMessage(bool? running = null)
    {
        var isRunning = running ?? IsRunning;
        var msg = new JsonObject
        {
            ["kind"] = "state",
            ["running"] = isRunning,
            ["runId"] = Session.Log.CurrentRunId
        };
        if (isRunning && _current is { } c)
            msg["current"] = new JsonObject
            {
                ["id"] = c.Id,
                ["source"] = c.Source,
                ["task"] = OutputLimiter.Head(c.Task, 400),
                ["startedUtc"] = c.StartedUtc.ToString("o")
            };
        return msg;
    }

    private void SendHistory() =>
        Send(new JsonObject
        {
            ["kind"] = "history",
            ["items"] = JsonSerializer.SerializeToNode(_history.Load(200, _current?.Id), EventLog.Ndjson)
        });

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
        Send(StateMessage());
        if (_lastResult is { } r && !IsRunning) Send(ResultMessage(r));
    }

    private JsonObject ResultMessage(RunConclusion r)
    {
        var c = _current;
        return new JsonObject
        {
            ["kind"] = "result",
            ["status"] = r.Status,
            ["summary"] = r.Summary,
            ["stopped"] = c?.Status == "stopped",
            ["source"] = c?.Source,
            ["task"] = c is null ? null : OutputLimiter.Head(c.Task, 400),
            ["startedUtc"] = c?.StartedUtc.ToString("o"),
            ["endedUtc"] = c?.EndedUtc?.ToString("o")
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
                    _ = StartRun(task);
                    break;
                case "stop":
                    Stop();
                    break;
                case "historyEvents":
                    var histRun = msg["runId"]?.GetValue<string>() ?? "";
                    _ = Task.Run(() => SendHistoryEvents(histRun));
                    break;
                case "answer":
                    ResolveAsk(msg["id"]?.GetValue<string>(), msg["text"]?.GetValue<string>() ?? "");
                    break;
                case "confirmReply":
                    ResolveAsk(msg["id"]?.GetValue<string>(), msg["ok"]?.GetValue<bool>() == true ? "yes" : "no");
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
                    if (msg["model"]?.GetValue<string>() is { Length: > 0 } m) Config.Model = m.Trim();
                    if (msg["effort"]?.GetValue<string>() is { Length: > 0 } e) Config.ReasoningEffort = e.Trim();
                    if (msg["confirmIrreversible"] is JsonValue ci) Config.ConfirmIrreversible = ci.GetValue<bool>();
                    if (msg["maxTabs"] is JsonValue mt && mt.TryGetValue<int>(out var mtv)) Config.MaxTabs = Math.Clamp(mtv, 1, 12);
                    if (Tabs is not null) Tabs.Limit = Config.MaxTabs;
                    if (msg["sharedFolder"] is JsonValue sfv && sfv.TryGetValue<string>(out var sharedValue)) Config.SharedFolder = sharedValue.Trim();
                    if (msg["costInfo"] is JsonValue cinf && cinf.TryGetValue<double>(out var cinfV) && cinfV > 0) Config.CostInfoUsd = cinfV;
                    if (msg["costWarn"] is JsonValue cwrn && cwrn.TryGetValue<double>(out var cwrnV) && cwrnV > 0) Config.CostWarnUsd = cwrnV;
                    if (msg["domainLifetime"] is JsonValue dlife && dlife.TryGetValue<double>(out var dlifeV) && dlifeV > 0) Config.DomainLifetimeLimitUsd = dlifeV;
                    if (msg["domainLimit"] is JsonValue dlim && dlim.TryGetValue<double>(out var dlimV) && dlimV > 0) Config.DomainLimitUsd = dlimV;
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
                    Session.Log.Write("app", "settings", $"Settings: model={Config.Model}, effort={Config.ReasoningEffort}, confirmations={(Config.ConfirmIrreversible ? "on" : "off")}, max tabs={Config.MaxTabs}");
                    Send(SettingsMessage());
                    Notice("Settings saved.", "ok");
                    break;
                case "unblock":
                    var unDomain = msg["domain"]?.GetValue<string>() ?? "";
                    if (Session.Costs.Unblock(unDomain, Config.DomainLifetimeLimitUsd))
                    {
                        Session.Log.Write("app", "cost-unblock", $"The user lifted the block on {unDomain}; its cost counts from zero again", status: "warn");
                        Notice($"{unDomain} is unblocked. Its cost counts from zero again.", "ok");
                    }
                    else
                        Notice($"{unDomain} stays blocked: it reached the lifetime limit of ${Config.DomainLifetimeLimitUsd:0.00}. Raise the lifetime limit in Settings if you really want to go on.", "warn");
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
                    if (IsRunning) { Notice("A task is running - navigation is blocked.", "warn"); break; }
                    if (!string.IsNullOrWhiteSpace(url)) _ = Task.Run(() => Session.Browser.NavigateAsync(NormalizeUrl(url)));
                    break;
            }
        }
        catch (Exception ex)
        {
            Session.Log.Write("app", "error", $"Error handling message '{kind}': {ex.Message}", status: "error");
        }
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

    public RunProgress? Progress => Session.Meter?.Snapshot();

    public static string NormalizeUrl(string url)
    {
        url = url.Trim();
        if (url.Contains("://") || url.StartsWith("about:")) return url;
        return "https://" + url;
    }

    // ------------------------------------------------------------------ tasks

    /// <summary>Browser tabs (one per task). Set by the Windows app; without it the host works on the single page it was given.</summary>
    public TabManager? Tabs { get; set; }

    private volatile string? _tabKey;
    private DateTime _lastUrlSave = DateTime.MinValue;

    public string? LastUrlOf(string taskKey) => Tabs?.Find(taskKey)?.LastUrl;

    private static string TabLabel(string task)
    {
        var t = System.Text.RegularExpressions.Regex.Replace(task, @"\s+", " ").Trim();
        return t.Length <= 28 ? t : t[..27] + "…";
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

    /// <summary>Starts a task in the background. Returns null when another task is already running or the key is missing.</summary>
    public Task<RunConclusion>? StartRun(string task, RunOptions? options)
    {
        task = task.Trim();
        if (task.Length == 0) { Notice("Enter a task.", "warn"); return null; }
        if (!_mock && string.IsNullOrWhiteSpace(_secrets.GetApiKey()))
        {
            Notice("No OpenAI key. Open Settings in the panel and paste your key.", "error");
            return null;
        }
        lock (_lock)
        {
            if (_runTask is { IsCompleted: false }) { Notice("A task is already running. Stop it or wait.", "warn"); return null; }
            _runCts?.Dispose();
            _runCts = new CancellationTokenSource();
            var ct = _runCts.Token;
            _lastResult = null;
            // An MCP task has set the interceptor before it starts; anything else is typed in the panel.
            var started = new HistoryRecord(
                "H" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..4],
                AskInterceptor is null ? "panel" : "mcp",
                OutputLimiter.Head(Redactor.Text(task), 4000),
                DateTime.UtcNow, null, "running", null, null);
            _current = started;
            _history.Append(started);
            Send(StateMessage(true));
            SendHistory();
            var tabs = Tabs;
            var taskKey = options?.TaskKey ?? started.Id;
            _runTask = Task.Run(async () =>
            {
                RunConclusion result;
                var tabBegun = false;
                try
                {
                    var prompt = task;
                    if (tabs is not null)
                    {
                        var start = await tabs.BeginAsync(taskKey, TabLabel(task), options?.ContinueFromKey, ct);
                        tabBegun = true;
                        _tabKey = taskKey;
                        // Point the core at the tab (events subscribed once, per-tab enables and scripts).
                        await Session.Browser.InitAsync(ct);
                        if (start.Reopened) { try { await Session.Browser.WaitStableAsync(ct: ct); } catch (OperationCanceledException) { throw; } catch { } }
                        Session.Log.Write("app", "tab",
                            start.Reopened ? $"Task tab: reopened at {start.Url}" : start.Continued ? "Task tab: the earlier tab, as it was left" : "Task tab: a new tab",
                            new { taskKey, start.TabId });
                        prompt = BuildPrompt(task, options, start);
                    }
                    result = await Session.FillAsync(prompt, ct);
                }
                catch (Exception ex)
                {
                    Session.Log.Write("app", "error", "Unexpected error: " + ex.Message, status: "error");
                    result = new RunConclusion("failed", "Unexpected error: " + ex.Message, null);
                }
                finally
                {
                    CancelOpenCards();
                    _tabKey = null;
                    if (tabBegun && tabs is not null) { try { await tabs.EndAsync(taskKey); } catch { } }
                }
                lock (_lock)
                {
                    var finished = (_current ?? started) with
                    {
                        EndedUtc = DateTime.UtcNow,
                        Status = ct.IsCancellationRequested ? "stopped" : result.Status,
                        Summary = OutputLimiter.Head(Redactor.Text(result.Summary ?? ""), 4000)
                    };
                    _current = finished;
                    _history.Append(finished);
                }
                _lastResult = result;
                Send(StateMessage(false));
                Send(ResultMessage(result));
                SendHistory();
                return result;
            });
            return _runTask;
        }
    }

    /// <summary>Single task entry point, same as later via MCP: task in, result out.</summary>
    public async Task<RunConclusion> FillAsync(string task)
    {
        var t = StartRun(task) ?? throw new InvalidOperationException("Cannot start a task now (another one is running, or no API key).");
        return await t;
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (_runTask is { IsCompleted: false })
            {
                Session.Log.Write("app", "stop", "Stopped at the user's request", status: "warn");
                _runCts?.Cancel();
            }
        }
        CancelOpenCards();
    }

    /// <summary>Screenshot of the browser right now, saved under screenshots\ (MCP serves it as a private link). Keeps the newest 60 files.</summary>
    public async Task<string?> CaptureScreenshotAsync(CancellationToken ct)
    {
        var b64 = await Session.Browser.ScreenshotJpegBase64Async(75, ct);
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

    // ------------------------------------------------------------------ IUserInteraction via the panel

    public async Task<string> AskAsync(string question, IReadOnlyList<string>? options, CancellationToken ct)
    {
        if (AskInterceptor is { } routed)
        {
            Session.Log.Write("app", "ask", "Question for the caller (MCP): " + question, status: "warn");
            return await routed(question, options, ct);
        }
        if (!_panel.HasClients) return await _fallback.AskAsync(question, options, ct);
        var card = new JsonObject
        {
            ["kind"] = "ask",
            ["id"] = NewId(),
            ["question"] = question,
            ["options"] = new JsonArray((options ?? Array.Empty<string>()).Select(o => (JsonNode)o).ToArray())
        };
        Session.Log.Write("app", "ask", "Question for the user: " + question, status: "warn");
        var ans = await WaitForCard(card, ct);
        return ans ?? "(no answer from the user within the time limit - carry on on your own if that is safe)";
    }

    /// <summary>
    /// A question only the person at the computer can answer (e.g. a code from their e-mail): never routed to the MCP caller.
    /// The MCP side only learns that the task waits for the user (status waiting_for_user).
    /// </summary>
    public async Task<string> AskHumanAsync(string question, IReadOnlyList<string>? options, CancellationToken ct)
    {
        if (!_panel.HasClients) return await _fallback.AskHumanAsync(question, options, ct);
        var card = new JsonObject
        {
            ["kind"] = "ask",
            ["id"] = NewId(),
            ["human"] = true,
            ["question"] = question,
            ["options"] = new JsonArray((options ?? Array.Empty<string>()).Select(o => (JsonNode)o).ToArray())
        };
        Session.Log.Write("app", "ask", "Question only the user can answer: " + question, status: "warn");
        UserWaiting?.Invoke(question);
        try
        {
            var ans = await WaitForCard(card, ct);
            return ans ?? "(no answer from the user within the time limit - carry on on your own if that is safe)";
        }
        finally { UserWaiting?.Invoke(null); }
    }

    public async Task<bool> ConfirmAsync(string what, CancellationToken ct)
    {
        if (!_panel.HasClients) return await _fallback.ConfirmAsync(what, ct);
        var card = new JsonObject { ["kind"] = "confirm", ["id"] = NewId(), ["what"] = what };
        Session.Log.Write("app", "confirm", "Permission requested: " + what, status: "warn");
        var ans = await WaitForCard(card, ct);
        var ok = ans == "yes";
        Session.Log.Write("app", "confirm", ok ? "User confirmed" : "Not confirmed", status: ok ? "ok" : "warn");
        return ok;
    }

    private async Task<string?> WaitForCard(JsonObject card, CancellationToken ct)
    {
        var id = card["id"]!.GetValue<string>();
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingAsks[id] = tcs;
        var json = card.ToJsonString(EventLog.Ndjson);
        _openCards[id] = json;
        _panel.Send(json);
        if (_tabKey is { } waitingKey) Tabs?.SetState(waitingKey, "waiting");
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
            Send(new JsonObject { ["kind"] = "closeCard", ["id"] = id });
            if (_pendingAsks.IsEmpty)
            {
                if (_tabKey is { } runningKey) Tabs?.SetState(runningKey, "running");
                try { Attention?.Invoke(false, ""); } catch { }
            }
        }
    }

    private void ResolveAsk(string? id, string answer)
    {
        if (id is not null && _pendingAsks.TryGetValue(id, out var tcs)) tcs.TrySetResult(answer);
    }

    private void CancelOpenCards()
    {
        foreach (var kv in _pendingAsks) kv.Value.TrySetCanceled();
    }

    private static string NewId() => "q" + Guid.NewGuid().ToString("N")[..8];

    public async ValueTask DisposeAsync()
    {
        Stop();
        try { if (_runTask is not null) await Task.WhenAny(_runTask, Task.Delay(3000)); } catch { }
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
