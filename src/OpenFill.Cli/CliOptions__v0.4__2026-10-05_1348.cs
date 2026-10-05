// OpenFill - Metadata: wersja 0.4, data 2026-10-05 13:48
using OpenFill.Core.Config;

namespace OpenFill.Cli;

/// <summary>Parametry wiersza polecen CLI.</summary>
public sealed class CliOptions
{
    public string Instance = "dev";
    public string? RootOverride;
    public string? Task;
    public bool Headless = true;
    public bool Mock;
    public bool Unattended;
    public bool ShowHelp;
    public int PanelPort;
    public string? Model;
    public string? Effort;
    public string? HomeUrl;
    public string? SetKey;
    public bool Keep;
    public bool Mcp;

    public static CliOptions Parse(string[] args)
    {
        var o = new CliOptions();
        for (int i = 0; i < args.Length; i++)
        {
            string Next() => ++i < args.Length ? args[i] : "";
            switch (args[i])
            {
                case "-h" or "--help": o.ShowHelp = true; break;
                case "--instance": o.Instance = Next(); break;
                case "--root": o.RootOverride = Next(); break;
                case "--task": o.Task = Next(); break;
                case "--headful": o.Headless = false; break;
                case "--headless": o.Headless = true; break;
                case "--mock": o.Mock = true; break;
                case "--unattended": o.Unattended = true; break;
                case "--panel-port": int.TryParse(Next(), out o.PanelPort); break;
                case "--model": o.Model = Next(); break;
                case "--effort": o.Effort = Next(); break;
                case "--home": o.HomeUrl = Next(); break;
                case "--set-key": o.SetKey = Next(); break;
                case "--keep": o.Keep = true; break;
                case "--mcp": o.Mcp = true; break;
                default:
                    if (o.Task is null && !args[i].StartsWith('-')) o.Task = args[i];
                    break;
            }
        }
        return o;
    }

    public void ApplyTo(AppConfig c)
    {
        if (!string.IsNullOrEmpty(Model)) c.Model = Model;
        if (!string.IsNullOrEmpty(Effort)) c.ReasoningEffort = Effort;
        if (!string.IsNullOrEmpty(HomeUrl)) c.HomeUrl = HomeUrl!;
    }

    public static void PrintHelp()
    {
        Console.WriteLine("""
            OpenFill CLI - fills in web pages using a model.

            Usage:
              openfill [options]                   panel + browser, you enter tasks in the panel
              openfill --task "<task>" [options]  run one task and exit
              echo "<task>" | openfill [options]

            Options:
              --task <text>      the task to run
              --instance <name> instance (profile/log directory), default "dev"
              --root <path>      override the base data directory
              --headful          show the browser window (default: headless)
              --mock             use the scripted model (no network, for tests)
              --unattended       unattended mode (asks nothing, refuses irreversible steps)
              --panel-port <n>   fixed panel port
              --model <id>       OpenAI model (e.g. gpt-6-luna)
              --effort <level>   reasoning effort (low|medium|high|xhigh|max)
              --home <url>       start page
              --set-key <key>    save the OpenAI key for the instance and exit
              --keep             after a --task run, keep going (the panel stays up)
              --mcp              expose OpenFill through MCP (prints the address)
              -h, --help         this help
            """);
    }
}
