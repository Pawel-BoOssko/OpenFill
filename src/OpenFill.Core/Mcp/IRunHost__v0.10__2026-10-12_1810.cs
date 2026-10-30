// SOURCE_MCP_CAPTURE_20261030
// OpenFill - Metadata: wersja 0.10, data 2026-10-12 18:10
using System.Text.Json.Nodes;
using OpenFill.Core.Agent;

namespace OpenFill.Core.Mcp;

/// <summary>Handler for questions the inner model asks (ask_user). During an MCP task the answer comes from the calling model.</summary>
public delegate Task<string> AskHandler(string question, IReadOnlyList<string>? options, CancellationToken ct);

/// <summary>Extra information that goes with a task: its identity (for the browser tab), the session it continues, and the hooks of whoever started it.</summary>
/// <param name="TaskKey">Id of the task (the MCP task id); it keys the task's browser tab.</param>
/// <param name="ContinueFromKey">Id of an earlier task whose browser tab (or last address) this task picks up.</param>
/// <param name="PriorContext">Short description of the earlier task and its result, handed to the inner model.</param>
/// <param name="Ask">Receives the inner model's ask_user questions of THIS task (an MCP task routes them to the calling model). Null = the panel asks the person.</param>
/// <param name="Activity">Short descriptions of what the inner model does (for progress reports).</param>
/// <param name="UserWaiting">The question (non-null) while the inner model waits for the person at the computer (only they can answer), null when that is over.</param>
/// <param name="Requester">Name the calling chat gave for itself (for example Bishop, GPT, Papa); shown with a colour on tabs, cards, events and history.</param>
/// <param name="Title">Short name of the task given by the caller; shown on the browser tab and on the cards in the window.</param>
public sealed record VerifiedAction(string Recipient, int Total, string Evidence, string Url, DateTime ConfirmedUtc);

public sealed record RunOptions(string? TaskKey = null, string? ContinueFromKey = null, string? PriorContext = null,
    AskHandler? Ask = null, Action<string>? Activity = null, Action<string?>? UserWaiting = null, string? Title = null, string? Requester = null, Func<VerifiedAction, int>? Verified = null,
    bool AutomaticContinuation = false);

/// <summary>Why a task was not started. Kind: limit (too many tasks at once), domain_busy (another task works on the site), no_key, empty, other.</summary>
public sealed record RunRefusal(string Kind, string Message);

/// <summary>Either a running task (Run) or the reason it did not start (Refusal).</summary>
public sealed record StartResult(Task<RunConclusion>? Run, RunRefusal? Refusal);

/// <summary>
/// What the MCP layer needs from the application: start a task, stop it, route the inner model's questions
/// to the MCP caller and observe what the inner model does. AppHost implements it; tests use a fake.
/// Several tasks may run at once (up to MaxParallel); everything that belongs to one task is addressed by its task key.
/// </summary>
public interface IRunHost
{
    /// <summary>Starts a task in the background; null when it cannot be started (see <see cref="TryStart"/> for the reason).</summary>
    Task<RunConclusion>? StartRun(string task);

    /// <summary>Same, with the task's identity, the session it continues and the hooks of its caller (a host without tabs ignores the identity).</summary>
    Task<RunConclusion>? StartRun(string task, RunOptions? options) => StartRun(task);

    /// <summary>Same, and says why when it did not start.</summary>
    StartResult TryStart(string task, RunOptions? options)
    {
        var run = StartRun(task, options);
        return new StartResult(run, run is null ? new RunRefusal("other", "OpenFill could not start the task.") : null);
    }

    /// <summary>How many tasks may run at the same time.</summary>
    int MaxParallel => 1;

    /// <summary>When the task text mentions a web domain that is blocked (cost limit), returns the message for the caller; otherwise null.</summary>
    string? CheckBlocked(string task) => null;

    /// <summary>
    /// Maintenance block: with minutes &gt; 0 the host takes no NEW tasks for that long (tasks already running go on), with 0 the block ends,
    /// with null it only reports. Returns the state (active, minutes_left, reason, running_tasks).
    /// </summary>
    JsonObject Maintenance(int? minutes, string? reason, string? by) => new() { ["active"] = false, ["running_tasks"] = 0 };

    /// <summary>Steps, cost and warning of a task (running or just ended); null when the host does not measure.</summary>
    RunProgress? ProgressOf(string taskKey) => null;

    /// <summary>Last page address of the browser tab that belonged to a task (null when unknown).</summary>
    string? LastUrlOf(string taskKey) => null;

    /// <summary>Settings, limits, costs per site, tabs and recent problems for the openfill_info tool (no secrets). Null when the host has nothing to add.</summary>
    JsonObject? Diagnostics() => null;

    /// <summary>Details of this task's own browser tab, without exposing another chat's context.</summary>
    JsonObject? TabDetailsOf(string taskKey) => null;

    /// <summary>Captures one existing, physically open tab without model inference or navigation.</summary>
    Task<JsonObject> CapturePageAsync(string taskKey, string? expectedUrl, string mode,
        string selector, string regex, string? directory, int maxMatches, CancellationToken ct)
        => Task.FromResult(new JsonObject { ["status"] = "error", ["message"] = "Direct page capture not supported by this host." });

    /// <summary>Stops one task (or every task when the key is null).</summary>
    void Stop(string? taskKey);

    /// <summary>Takes a screenshot of the task's browser tab (the newest task's tab when the key is null) and saves it; returns the file name (null when it cannot be taken).</summary>
    Task<string?> CaptureScreenshotAsync(string? taskKey, CancellationToken ct);
}
