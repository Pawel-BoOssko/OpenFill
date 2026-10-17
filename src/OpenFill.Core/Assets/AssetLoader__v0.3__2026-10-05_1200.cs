// OpenFill - Metadata: wersja 0.3, data 2026-10-05 12:00
using System.Reflection;

namespace OpenFill.Core.Assets;

/// <summary>
/// Loads embedded resources (JS, HTML) by logical name without a version, e.g. "extractor.js".
/// Files on disk have versions in their names; LogicalName in the csproj maps them to the unversioned name.
/// </summary>
public static class AssetLoader
{
    private static readonly Assembly Asm = typeof(AssetLoader).Assembly;

    public static string Load(string logicalName)
    {
        var full = "OpenFill.Assets." + logicalName;
        using var s = Asm.GetManifestResourceStream(full)
            ?? throw new FileNotFoundException($"Embedded resource missing: {full}. Available: {string.Join(", ", Asm.GetManifestResourceNames())}");
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }

    public static bool TryLoad(string logicalName, out string content)
    {
        try { content = Load(logicalName); return true; }
        catch { content = ""; return false; }
    }
}
