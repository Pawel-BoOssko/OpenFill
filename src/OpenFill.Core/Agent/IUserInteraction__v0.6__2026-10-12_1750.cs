// OpenFill - Metadata: wersja 0.6, data 2026-10-12 17:50
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

    /// <summary>A message the person wrote next to their last yes/no answer (null when there was none). Reading it clears it.</summary>
    string? TakeConfirmNote() => null;

    /// <summary>
    /// A question reserved for the person at the computer when the calling model cannot provide the information
    /// or direct human action is required. It is never passed to the calling model. The default is the same as AskAsync.
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
