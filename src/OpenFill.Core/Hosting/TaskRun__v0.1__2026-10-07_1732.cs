// CAPTURE_TASK_OUTCOME_20261030
// OpenFill - Metadata: wersja 0.1, data 2026-10-07 17:32
using OpenFill.Core.Agent;
using OpenFill.Core.Browser;
using OpenFill.Core.Logging;
using OpenFill.Core.Model;
using OpenFill.Core.Mcp;

namespace OpenFill.Core.Hosting;

/// <summary>
/// One task: its log (run id, run files), its browser connection, its meter, its tools and model loop.
/// Created by <see cref="OpenFillSession.CreateRun"/>. Runs side by side with other TaskRuns.
/// </summary>
public sealed class TaskRun
{
    private readonly OpenFillSession _s;
    private readonly IUserInteraction _ui;
    private readonly IDomainGate? _gate;
    private readonly IModelClient _model;

    public EventLog Log { get; }
    public BrowserController Browser { get; }
    public RunMeter Meter { get; } = new();

    /// <summary>The run id of this task's log files (known from <see cref="Begin"/> on).</summary>
    public string? RunId => Log.CurrentRunId;

    internal TaskRun(OpenFillSession s, EventLog log, BrowserController browser, IUserInteraction ui, IDomainGate? gate, IModelClient model)
    {
        _s = s; Log = log; Browser = browser; _ui = ui; _gate = gate; _model = model;
    }

    /// <summary>Opens the run files now (so events from the preparation of the tab already belong to this task) and returns the run id.</summary>
    public string Begin() => Log.CurrentRunId ?? Log.BeginRun();

    /// <summary>Stops listening to the browser tab (the task is over; the tab stays open for the person or for a continued task).</summary>
    public void Detach() => Browser.Detach();

    /// <summary>Runs the task: the model loop until it finishes, is stopped or hits a limit.</summary>
    public async Task<RunConclusion> FillAsync(string task, CancellationToken ct = default, Func<VerifiedAction, int>? verified = null)
    {
        var runId = Begin();
        Log.Write("session", "start", "New task", new { runId, preview = OutputLimiter.Head(task, 200) });
        AgentRunner? runner = null;
        var config = _s.Config;
        var paths = _s.Paths;
        try
        {
            var notes = new SiteNotesStore(paths.Notes, Log);
            var gaps = new GapRecorder(paths.GapsFile, Log);
            var limiter = new OutputLimiter(paths.Overflow, config.ToolOutputLimitChars);
            var registry = new ToolRegistry(Browser, Log, config, limiter, notes, _ui, gaps, _s.Costs,
                new SharedFiles(paths.SharedFor(config.SharedFolder), paths.Downloads), _gate, verified);
            runner = new AgentRunner(_model, registry, config, Log, _s.Costs, Meter, _gate);
            var conclusion = await runner.RunAsync(task, ct);
            registry.RecordCaptureTaskOutcome(conclusion.Status);

            string host = "";
            try { host = new Uri(await Browser.CurrentUrlAsync(ct)).Host; } catch { }
            if (!string.IsNullOrEmpty(host))
                notes.RecordOutcome(host, conclusion.Status == "success", conclusion.SiteKnowledge);

            Log.Write("session", "end", $"Finished: {conclusion.Status}", new { conclusion.Status, conclusion.Summary },
                status: conclusion.Status == "success" ? "ok" : conclusion.Status == "failed" ? "error" : "warn");
            return conclusion;
        }
        catch (OperationCanceledException)
        {
            Log.Write("session", "cancel", "Task stopped / timed out", status: "error");
            return new RunConclusion("failed", "The task was stopped or exceeded the time limit.", null);
        }
        finally
        {
            if (runner is { Usage.Calls: > 0 })
            {
                var u = runner.Usage;
                var cost = u.CostUsd is { } c ? $", ~${c:0.####}" : "";
                Log.WriteUsage(new
                {
                    ts = DateTime.UtcNow,
                    runId,
                    kind = "task",
                    model = config.Model,
                    effort = config.ReasoningEffort,
                    calls = u.Calls,
                    inputTokens = u.InputTokens,
                    cachedTokens = u.CachedTokens,
                    outputTokens = u.OutputTokens,
                    reasoningTokens = u.ReasoningTokens,
                    modelMs = u.ModelMs,
                    costUsd = u.CostUsd,
                    task = OutputLimiter.Head(task.ReplaceLineEndings(" "), 120)
                });
                Log.Write("session", "usage",
                    $"Model usage: {u.Calls} calls, {u.InputTokens} in ({u.CachedTokens} cached) / {u.OutputTokens} out, {u.ModelMs / 1000.0:0.0} s{cost}",
                    new { totals = u }, status: "info");
            }
            Log.EndRun();
        }
    }
}
