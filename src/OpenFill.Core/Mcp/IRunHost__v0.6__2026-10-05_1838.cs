// OpenFill - Metadata: wersja 0.6, data 2026-10-05 18:38
using OpenFill.Core.Agent;

namespace OpenFill.Core.Mcp;

/// <summary>Handler for questions the inner model asks (ask_user). During an MCP task the answer comes from the calling model.</summary>
public delegate Task<string> AskHandler(string question, IReadOnlyList<string>? options, CancellationToken ct);

/// <summary>Extra information that goes with a task: its identity (for the browser tab) and the session it continues.</summary>
/// <param name="TaskKey">Id of the task (the MCP task id); it keys the task's browser tab.</param>
/// <param name="ContinueFromKey">Id of an earlier task whose browser tab (or last address) this task picks up.</param>
/// <param name="PriorContext">Short description of the earlier task and its result, handed to the inner model.</param>
public sealed record RunOptions(string? TaskKey = null, string? ContinueFromKey = null, string? PriorContext = null);

/// <summary>
/// What the MCP layer needs from the application: start a task, stop it, route the inner model's questions
/// to the MCP caller and observe what the inner model does. AppHost implements it; tests use a fake.
/// </summary>
public interface IRunHost
{
    /// <summary>Starts a task in the background; null when it cannot be started (another task runs, no key).</summary>
    Task<RunConclusion>? StartRun(string task);

    /// <summary>Same, with the task's identity and the session it continues (a host without tabs ignores the options).</summary>
    Task<RunConclusion>? StartRun(string task, RunOptions? options) => StartRun(task);

    /// <summary>When the task text mentions a web domain that is blocked (cost limit), returns the message for the caller; otherwise null.</summary>
    string? CheckBlocked(string task) => null;

    /// <summary>Steps, cost and warning of the task that runs now (or ran last); null when the host does not measure.</summary>
    RunProgress? Progress => null;

    /// <summary>Last page address of the browser tab that belonged to a task (null when unknown).</summary>
    string? LastUrlOf(string taskKey) => null;

    void Stop();

    /// <summary>Takes a screenshot of the browser as it is now and saves it; returns the file name (null when it cannot be taken).</summary>
    Task<string?> CaptureScreenshotAsync(CancellationToken ct);

    /// <summary>When set, ask_user questions go here instead of the panel. Set only while an MCP task is open.</summary>
    AskHandler? AskInterceptor { get; set; }

    /// <summary>Short descriptions of the inner model's tool calls (for progress reports).</summary>
    event Action<string>? Activity;

    /// <summary>
    /// Raised with the question when the inner model starts waiting for the person at the computer (something only they can give,
    /// e.g. a verification code), and with null when that wait is over. The MCP caller cannot answer such a question.
    /// </summary>
    event Action<string?>? UserWaiting;
}
