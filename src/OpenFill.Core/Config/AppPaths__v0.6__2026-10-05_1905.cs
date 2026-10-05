// OpenFill - Metadata: wersja 0.6, data 2026-10-05 19:05
namespace OpenFill.Core.Config;

/// <summary>
/// Separate directories per instance (stable / dev): browser profile, logs, downloaded files,
/// site notes, configuration and working files. Development work does not touch the stable instance.
/// </summary>
public sealed class AppPaths
{
    public string Instance { get; }
    public string Root { get; }
    public string BrowserProfile => Path.Combine(Root, "profile");
    public string Logs => Path.Combine(Root, "logs");
    public string Runs => Path.Combine(Root, "logs", "runs");
    public string Overflow => Path.Combine(Root, "logs", "overflow");
    /// <summary>Screenshots taken on request through MCP (served by the MCP server as private links).</summary>
    public string Screenshots => Path.Combine(Root, "screenshots");
    public string Downloads => Path.Combine(Root, "downloads");
    public string Notes => Path.Combine(Root, "notes");
    public string Runtime => Path.Combine(Root, "runtime");
    public string Tasks => Path.Combine(Root, "tasks");
    public string ConfigFile => Path.Combine(Root, "config.json");
    public string SecretFile => Path.Combine(Root, "openai.key");
    public string GapsFile => Path.Combine(Root, "logs", "gaps.ndjson");

    public AppPaths(string instance, string? rootOverride = null)
    {
        Instance = string.IsNullOrWhiteSpace(instance) ? "stable" : instance.Trim().ToLowerInvariant();
        var baseDir = rootOverride ?? DefaultBase();
        Root = Path.Combine(baseDir, Instance);
    }

    /// <summary>
    /// %USERPROFILE%\OpenFill\data when the installer made it (outside AppData on purpose: a host such as the Claude desktop app redirects
    /// AppData writes into a private copy that Windows itself - autostart, shortcuts - cannot see); otherwise %LOCALAPPDATA%\OpenFill.
    /// </summary>
    private static string DefaultBase()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(profile))
        {
            var portable = Path.Combine(profile, "OpenFill", "data");
            if (Directory.Exists(portable)) return portable;
        }
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create),
            "OpenFill");
    }

    /// <summary>The shared folder: the configured one, or %USERPROFILE%\OpenFill\shared.</summary>
    public string SharedFor(string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured)) return Environment.ExpandEnvironmentVariables(configured.Trim());
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(string.IsNullOrEmpty(profile) ? Root : profile, "OpenFill", "shared");
    }

    public void EnsureCreated()
    {
        foreach (var d in new[] { Root, BrowserProfile, Logs, Runs, Overflow, Downloads, Notes, Runtime, Tasks, Screenshots })
            Directory.CreateDirectory(d);
    }
}
