// OpenFill - Metadata: wersja 0.5, data 2026-10-05 19:05
using OpenFill.Core.Agent;
using OpenFill.Core.Browser;
using OpenFill.Core.Cdp;
using OpenFill.Core.Config;
using OpenFill.Core.Logging;
using OpenFill.Core.Model;

namespace OpenFill.Core.Hosting;

/// <summary>
/// Ties everything together: log, browser, notes, tools and the model loop.
/// This is the shared core used by the CLI, the Windows app and (later) MCP.
/// One public entry point: FillAsync(task) - the equivalent of the single tool exposed through MCP.
/// </summary>
public sealed class OpenFillSession : IAsyncDisposable
{
    public AppPaths Paths { get; }
    public AppConfig Config { get; }
    public EventLog Log { get; }
    public BrowserController Browser { get; }
    /// <summary>Cumulative cost per web domain, with the hard limit and the blocks.</summary>
    public CostGuard Costs { get; }
    /// <summary>Live numbers of the task that runs now (or ran last).</summary>
    public RunMeter? Meter { get; private set; }

    private readonly CdpSession _cdp;
    private readonly IModelClient _model;
    private readonly SiteNotesStore _notes;
    private readonly IUserInteraction _ui;
    private readonly GapRecorder _gaps;
    private readonly OutputLimiter _limiter;

    public OpenFillSession(AppPaths paths, AppConfig config, CdpSession cdp, IModelClient model, IUserInteraction ui)
    {
        Paths = paths;
        Config = config;
        _cdp = cdp;
        _model = model;
        _ui = ui;
        paths.EnsureCreated();
        Log = new EventLog(paths.Logs, paths.Runs);
        Costs = new CostGuard(Path.Combine(paths.Root, "costs.json"));
        Browser = new BrowserController(cdp, Log);
        _notes = new SiteNotesStore(paths.Notes, Log);
        _gaps = new GapRecorder(paths.GapsFile, Log);
        _limiter = new OutputLimiter(paths.Overflow, config.ToolOutputLimitChars);
    }

    public Task InitAsync(CancellationToken ct = default) => Browser.InitAsync(ct);

    /// <summary>The only task entry point. Returns the conclusion from the model's work.</summary>
    public async Task<RunConclusion> FillAsync(string task, CancellationToken ct = default)
    {
        var runId = Log.BeginRun();
        Log.Write("session", "start", "New task", new { runId, preview = OutputLimiter.Head(task, 200) });
        AgentRunner? runner = null;
        try
        {
            Meter = new RunMeter();
            var registry = new ToolRegistry(Browser, Log, Config, _limiter, _notes, _ui, _gaps, Costs, new SharedFiles(Paths.SharedFor(Config.SharedFolder), Paths.Downloads));
            runner = new AgentRunner(_model, registry, Config, Log, Costs, Meter);
            var conclusion = await runner.RunAsync(task, ct);

            string host = "";
            try { host = new Uri(await Browser.CurrentUrlAsync(ct)).Host; } catch { }
            if (!string.IsNullOrEmpty(host))
                _notes.RecordOutcome(host, conclusion.Status == "success", conclusion.SiteKnowledge);

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
                    model = Config.Model,
                    effort = Config.ReasoningEffort,
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

    public async ValueTask DisposeAsync()
    {
        Log.Dispose();
        await _cdp.Connection.DisposeAsync();
    }
}
