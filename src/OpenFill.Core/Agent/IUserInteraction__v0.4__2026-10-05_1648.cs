// OpenFill - Metadata: wersja 0.4, data 2026-10-05 16:48
namespace OpenFill.Core.Agent;

/// <summary>
/// Channel to the user for model questions (ask_user) and requests for consent before an irreversible step.
/// In the CLI the console answers; in the windowed app, a card in the panel.
/// In unattended mode (no human) an implementation may automatically refuse irreversible steps.
/// </summary>
public interface IUserInteraction
{
    Task<string> AskAsync(string question, IReadOnlyList<string>? options, CancellationToken ct);
    Task<bool> ConfirmAsync(string what, CancellationToken ct);

    /// <summary>
    /// A question only the person at the computer can answer (e.g. a one-time code from their e-mail). It is never passed on to a
    /// calling model. The default is the same as AskAsync.
    /// </summary>
    Task<string> AskHumanAsync(string question, IReadOnlyList<string>? options, CancellationToken ct) => AskAsync(question, options, ct);
}

/// <summary>Default unattended implementation: answers questions with an empty reply and rejects irreversible steps.</summary>
public sealed class HeadlessInteraction : IUserInteraction
{
    public Task<string> AskAsync(string question, IReadOnlyList<string>? options, CancellationToken ct)
        => Task.FromResult("(no answer from the user — carry on on your own if that is safe)");
    public Task<bool> ConfirmAsync(string what, CancellationToken ct) => Task.FromResult(false);
}
