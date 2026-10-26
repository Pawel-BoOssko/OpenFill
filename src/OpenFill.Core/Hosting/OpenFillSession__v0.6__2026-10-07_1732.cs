// OpenFill - Metadata: wersja 0.6, data 2026-10-07 17:32
using OpenFill.Core.Agent;
using OpenFill.Core.Browser;
using OpenFill.Core.Cdp;
using OpenFill.Core.Config;
using OpenFill.Core.Logging;
using OpenFill.Core.Model;

namespace OpenFill.Core.Hosting;

/// <summary>
/// What all tasks share: paths, settings, the root log, the cost guard, and (for single-task use) one browser page.
/// This is the shared core used by the CLI, the Windows app and MCP.
/// One task = one <see cref="TaskRun"/> (own log, own browser connection, own meter); several run side by side.
/// <see cref="FillAsync"/> is the simple entry for one task on the default page (CLI, tests).
/// </summary>
public sealed class OpenFillSession : IAsyncDisposable
{
    public AppPaths Paths { get; }
    public AppConfig Config { get; }
    /// <summary>The root log: app events and the events of every task (each task also has its own run file).</summary>
    public EventLog Log { get; }
    /// <summary>The default page (used by <see cref="FillAsync"/> and by manual navigation from the panel).</summary>
    public BrowserController Browser { get; }
    /// <summary>Cumulative cost per web domain (a sliding window), with the hard limit and the blocks. Shared by all tasks.</summary>
    public CostGuard Costs { get; }
    /// <summary>Live numbers of the last task started through <see cref="FillAsync"/>.</summary>
    public RunMeter? Meter { get; private set; }

    private readonly CdpSession _cdp;
    private readonly IModelClient _model;
    private readonly IUserInteraction _ui;

    public OpenFillSession(AppPaths paths, AppConfig config, CdpSession cdp, IModelClient model, IUserInteraction ui)
    {
        Paths = paths;
        Config = config;
        _cdp = cdp;
        _model = model;
        _ui = ui;
        paths.EnsureCreated();
        Log = new EventLog(paths.Logs, paths.Runs);
        Costs = new CostGuard(Path.Combine(paths.Root, "costs.json"), config.CostWindowDays);
        Browser = new BrowserController(cdp, Log);
    }

    public Task InitAsync(CancellationToken ct = default) => Browser.InitAsync(ct);

    /// <summary>
    /// A task of its own: its own log (run id and files), its own browser connection (a tab), its own meter.
    /// The cost guard, site notes, settings and the shared files are common.
    /// </summary>
    public TaskRun CreateRun(CdpSession cdp, IUserInteraction ui, IDomainGate? gate = null, IModelClient? model = null)
    {
        var log = Log.Fork();
        return new TaskRun(this, log, new BrowserController(cdp, log), ui, gate, model ?? _model);
    }

    /// <summary>The simple entry point: one task on the default page. Returns the conclusion from the model's work.</summary>
    public Task<RunConclusion> FillAsync(string task, CancellationToken ct = default)
    {
        var run = new TaskRun(this, Log, Browser, _ui, null, _model);
        Meter = run.Meter;
        return run.FillAsync(task, ct);
    }

    public async ValueTask DisposeAsync()
    {
        Log.Dispose();
        await _cdp.Connection.DisposeAsync();
    }
}
