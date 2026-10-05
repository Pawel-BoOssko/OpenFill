// OpenFill - Metadata: wersja 0.3, data 2026-10-05 18:38
using System.Windows.Forms;
using OpenFill.Core.Config;

namespace OpenFill.App;

/// <summary>
/// Entry point of the Windows app. Parameters:
///   --instance dev     separate profile, logs and settings (development does not touch the "stable" instance)
///   --set-key KEY      store the OpenAI key (DPAPI-encrypted) and exit
///   --import-env-key   store the key from the OPENAI_API_KEY variable (DPAPI-encrypted) and exit - used by the installer
///   --minimized        start with the window minimized (used by the autostart task at Windows logon)
/// Only one copy runs per instance: a second start exits at once.
/// </summary>
public static class Program
{
    /// <summary>Set by --minimized: the main window starts minimized (autostart).</summary>
    public static bool StartMinimized { get; private set; }

    [STAThread]
    public static int Main(string[] args)
    {
        var instance = "stable";
        string? setKey = null;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--instance" && i + 1 < args.Length) instance = args[++i];
            else if (args[i] == "--set-key" && i + 1 < args.Length) setKey = args[++i];
            else if (args[i] == "--minimized") StartMinimized = true;
            else if (args[i] == "--import-env-key")
            {
                setKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
                if (string.IsNullOrWhiteSpace(setKey)) return 3;
            }
        }

        var paths = new AppPaths(instance);
        paths.EnsureCreated();
        var config = AppConfig.Load(paths.ConfigFile);
        if (!File.Exists(paths.ConfigFile)) config.Save(paths.ConfigFile); // a visible settings file with the defaults
        var secrets = new SecretStore(paths.SecretFile, new DpapiSecretProtector());

        if (setKey is not null)
        {
            secrets.SetApiKey(setKey);
            return 0;
        }

        // One copy per instance (autostart, a restart after a crash and a manual start must not fight over the port and the profile).
        using var single = new Mutex(true, "Local\\OpenFill-" + instance, out var firstCopy);
        if (!firstCopy) return 0;

#if !OPENFILL_COMPILECHECK
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
#endif
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm(paths, config, secrets));
        return 0;
    }
}
