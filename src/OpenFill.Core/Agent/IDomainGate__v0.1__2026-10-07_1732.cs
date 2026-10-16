// OpenFill - Metadata: wersja 0.1, data 2026-10-07 17:32
namespace OpenFill.Core.Agent;

/// <summary>
/// Lets a task go onto a web domain. With several tasks running at once, only one may work on a given domain.
/// The host hands the task's registry and runner a gate; the gate takes the domain for the task or refuses.
/// </summary>
public interface IDomainGate
{
    /// <summary>Null = the task may work on the domain (and now holds it). Otherwise the text of the refusal (who is there, doing what, since when).</summary>
    string? TryEnter(string domain);
}
