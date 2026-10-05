// OpenFill - Metadata: wersja 0.3, data 2026-10-05 15:15
using OpenFill.Core.Agent;

namespace OpenFill.Cli;

/// <summary>Model questions in the CLI: if the console is interactive, it asks; in unattended mode it refuses irreversible steps.</summary>
public sealed class ConsoleInteraction(bool unattended) : IUserInteraction
{
    public Task<string> AskAsync(string question, IReadOnlyList<string>? options, CancellationToken ct)
    {
        if (unattended || Console.IsInputRedirected)
            return Task.FromResult("(unattended mode — no answer; carry on on your own if that is safe)");
        Console.WriteLine("\n[pytanie modelu] " + question);
        if (options is { Count: > 0 }) Console.WriteLine("  opcje: " + string.Join(" | ", options));
        Console.Write("> ");
        return Task.FromResult(Console.ReadLine() ?? "");
    }

    public Task<bool> ConfirmAsync(string what, CancellationToken ct)
    {
        if (unattended || Console.IsInputRedirected)
        {
            Console.WriteLine($"[irreversible step refused in unattended mode] {what}");
            return Task.FromResult(false);
        }
        Console.Write($"\n[potwierdzenie] {what}\nWykonac? [t/N] ");
        var ans = Console.ReadLine();
        return Task.FromResult(ans?.Trim().ToLowerInvariant() is "t" or "tak" or "y" or "yes");
    }
}
