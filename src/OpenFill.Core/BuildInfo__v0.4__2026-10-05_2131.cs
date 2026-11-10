// OpenFill - Metadata: wersja 0.4, data 2026-10-05 21:31
using System.Globalization;
using System.Reflection;

namespace OpenFill.Core;

/// <summary>Code version, version date and build time. Shown at the very top of the app.</summary>
public static class BuildInfo
{
    public static string Version { get; }
    public static DateTime VersionDateLocal { get; }
    public static DateTime BuildTimeLocal { get; }

    static BuildInfo()
    {
        var asm = typeof(BuildInfo).Assembly;
        string? Meta(string key) => asm.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == key)?.Value;

        Version = Meta("OpenFillVersion") ?? "0.0";
        // The version date is stored as the package author's local time (Europe/Warsaw), without a time zone.
        VersionDateLocal = DateTime.TryParseExact(Meta("OpenFillVersionDate"), "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var vd)
            ? vd : DateTime.MinValue;
        BuildTimeLocal = DateTime.TryParse(Meta("OpenFillBuildUtc"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var bt)
            ? bt.ToLocalTime() : DateTime.MinValue;
    }

    /// <summary>One line for logs and MCP replies: "1.17 (version 2026-10-05 20:58, build 2026-10-05 21:01)".</summary>
    public static string Describe() => $"{Version} (version {Format(VersionDateLocal)}, build {Format(BuildTimeLocal)})";

    public static string Format(DateTime when) => when == DateTime.MinValue ? "?" : when.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Window title text: "2026-10-04 18:10 · v0.1".</summary>
    public static string Headline() => $"{Format(VersionDateLocal)} · v{Version}";
}
