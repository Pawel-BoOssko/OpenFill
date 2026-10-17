// OpenFill - Metadata: wersja 0.1, data 2026-10-07 14:32
namespace OpenFill.Core.Config;

/// <summary>
/// Values compiled into the build. In the repository the key is ALWAYS empty (a test enforces it).
/// Only the tester package builder (tools/pack-tester) writes a key here, in the exported copy, never in the repository.
/// The key is a last-resort fallback: the environment variable and the key saved in the panel take priority.
/// </summary>
public static class EmbeddedDefaults
{
    public const string ApiKey = "";
}
