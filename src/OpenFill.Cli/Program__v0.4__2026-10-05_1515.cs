// OpenFill - Metadata: wersja 0.4, data 2026-10-05 15:15
using OpenFill.Core;
using OpenFill.Core.Config;
using OpenFill.Core.Cdp;
using OpenFill.Core.Hosting;
using OpenFill.Core.Mcp;
using OpenFill.Core.Model;
using OpenFill.Cli;

// ------------------------------------------------------------------------------------------------
// OpenFill CLI - the core host without a window: starts Chromium/Edge through CDP, serves the live panel
// (the same as in the Windows app) and takes tasks from the panel or from the --task parameter.
// ------------------------------------------------------------------------------------------------

var opts = CliOptions.Parse(args);
if (opts.ShowHelp) { CliOptions.PrintHelp(); return 0; }

Console.WriteLine($"OpenFill — {BuildInfo.Headline()}");

var paths = new AppPaths(opts.Instance, opts.RootOverride);
paths.EnsureCreated();
var config = AppConfig.Load(paths.ConfigFile);
opts.ApplyTo(config);
var secrets = new SecretStore(paths.SecretFile);

if (opts.SetKey is not null)
{
    secrets.SetApiKey(opts.SetKey);
    Console.WriteLine($"OpenAI key saved ({SecretStore.Mask(opts.SetKey)}) to {paths.SecretFile}");
    return 0;
}

Func<IModelClient>? mock = opts.Mock ? () => new MockModelClient() : null;
Console.WriteLine(opts.Mock ? "Model: MOCK (scripted, no network)." : $"Model: {config.Model} (key: {secrets.KeySource()}).");

await using var panel = new PanelServer(opts.PanelPort);
panel.Start();
Console.WriteLine($"Panel: {panel.Url}");

using var chromium = new ChromiumLauncher();
Console.WriteLine("Uruchamiam przegladarke…");
var wsUrl = await chromium.LaunchAsync(paths.BrowserProfile, headless: opts.Headless);
var conn = new WebSocketCdpConnection();
await conn.ConnectAsync(wsUrl);
var cdp = await CdpBootstrap.AttachPageAsync(conn, config.HomeUrl);

var console = new ConsoleInteraction(opts.Unattended);
await using var host = new AppHost(paths, config, secrets, cdp, panel, console, mock);
host.OpenFolder = p => Console.WriteLine("Folder: " + p);
await host.InitAsync();

McpService? mcp = null;
if (opts.Mcp || config.McpEnabled)
    mcp = await McpService.StartAsync(host, paths, config, host.Session.Log, url => Console.WriteLine("MCP address: " + url));

// One-off task: from --task or from STDIN (when redirected).
string? task = opts.Task ?? (Console.IsInputRedirected ? await Console.In.ReadToEndAsync() : null);
if (!string.IsNullOrWhiteSpace(task))
{
    var run = host.StartRun(task);
    if (run is null) { Console.Error.WriteLine("No task was started (no key? use --set-key or --mock)."); return 2; }
    var conclusion = await run;
    Console.WriteLine("\n==================== WYNIK ====================");
    Console.WriteLine($"Status: {conclusion.Status}");
    Console.WriteLine(conclusion.Summary);
    Console.WriteLine("==============================================");
    Console.WriteLine($"Logi: {paths.Runs}");
    if (!opts.Keep) return conclusion.Status == "failed" ? 1 : 0;
}

// Panel mode: tasks are typed in the panel; Ctrl+C ends.
Console.WriteLine("\nOpen the panel in a browser and enter a task. Ctrl+C quits.");
var done = new TaskCompletionSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; done.TrySetResult(); };
await done.Task;
if (mcp is not null) await mcp.DisposeAsync();
Console.WriteLine("Koncze…");
return 0;
