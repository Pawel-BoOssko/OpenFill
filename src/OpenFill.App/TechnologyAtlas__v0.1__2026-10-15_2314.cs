// OpenFill Technology Atlas; version 0.1, 2026-10-15 23:14 (Europe/Warsaw).
// Safe read-only technology reference for job interviews: no credentials, endpoint secrets or private account data.
using System.Text;
using OpenFill.Core;

namespace OpenFill.App;

internal static class TechnologyAtlas
{
    internal sealed record Topic(string Category, string Name, string Technology, string Component, string Explanation, string Interview);

    internal static readonly Topic[] Topics =
    {
        new("01  Languages / runtime", "C#", "C# / managed CLR", "OpenFill.App, OpenFill.Core, OpenFill.Cli", "The application is developed in C#. The compiler produces managed .NET assemblies.", "I built a Windows agent in C# and separated its domain core from the graphical user interface."),
        new("01  Languages / runtime", ".NET 10", ".NET 10 SDK and runtime", "net10.0; net10.0-windows", "Modern .NET hosts the agent, the MCP service and the Windows UI.", "I used .NET 10 for a native Windows automation agent with a reusable library and CLI."),
        new("01  Languages / runtime", "JavaScript", "JavaScript ECMAScript", "Assets/extractor.js and Assets/actions.js", "Scripts inspect the page DOM, infer form controls and perform browser actions.", "I used injected JavaScript for structured DOM extraction rather than screenshot-only automation."),
        new("01  Languages / runtime", "HTML / CSS", "Embedded HTML and CSS", "Assets/panel.html", "The live agent panel is rendered inside its own WebView2 control.", "I combined a native WinForms shell with a web-rendered diagnostics and task panel."),
        new("01  Languages / runtime", "PowerShell", "Windows PowerShell scripts", "tools/install, stage, deploy_safely", "Scripts support builds, packaging, controlled updates and rollback.", "I automated repeatable Windows release and diagnostics tasks with PowerShell."),
        new("01  Languages / runtime", "JSON / NDJSON", "System.Text.Json and newline-delimited JSON", "Config, history, notes, telemetry", "Configuration, task records and streaming logs use JSON formats.", "I persisted structured task state as JSON and event/telemetry streams as NDJSON."),
        new("01  Languages / runtime", "MSBuild / NuGet", "SDK-style projects, MSBuild, NuGet", "OpenFill.App.csproj, Core.csproj, Cli.csproj", "The build consists of three projects, with Microsoft.Web.WebView2 as a package dependency.", "I used MSBuild and dotnet CLI to compile, publish and test separate C# projects."),
        new("02  Desktop / browser", "Windows Forms", "System.Windows.Forms / WinForms", "MainForm; FirefoxPasswordPicker; ImportedAccountsDialog", "Native task tabs, address bar, dialogs and operational buttons run in an interactive Windows session.", "I chose WinForms to control a browser and provide native dialogs on the user's desktop."),
        new("02  Desktop / browser", "WebView2", "Microsoft Edge WebView2 runtime", "MainForm BrowserTab, WebView2CdpConnection", "Chromium-based browser surfaces keep the user's local authenticated sessions.", "I embedded Edge WebView2 so an agent could act on real authenticated websites."),
        new("02  Desktop / browser", "Chromium", "Edge/Chromium browser engine", "WebView2 Runtime; CLI Chrome or Edge", "A multi-process browser engine creates separate renderer, GPU and utility processes.", "I learned to profile the entire browser process tree, not just the main .NET process."),
        new("02  Desktop / browser", "CDP", "Chrome DevTools Protocol", "BrowserController; CdpSession", "The agent reads DOM state, dispatches input and inspects network events through CDP.", "I used CDP for precise, lower-cost DOM-based browser automation."),
        new("02  Desktop / browser", "CDP WebSocket", "WebSocket transport for CDP", "WebSocketCdpConnection / OpenFill.Cli", "The headless/CLI variant talks to a browser using a CDP WebSocket connection.", "The same browser-control core supports either embedded WebView2 or standalone CDP."),
        new("02  Desktop / browser", "Semantic page model", "DOM extraction / semantic controls", "extractor.js, PageModel, FrameLayer", "Forms, labels and interactive elements are transformed into a compact machine-readable view.", "I reduced LLM input cost by exposing logical controls instead of whole-page screenshots."),
        new("02  Desktop / browser", "Real input events", "CDP Input, JavaScript DOM actions", "actions.js; NativeInput", "Input may be semantic batch fill, autocomplete typing, clicks or native keyboard/mouse interaction.", "I designed fallback tiers for websites that reject ordinary DOM events."),
        new("02  Desktop / browser", "Pop-ups and SSO", "WebView2 NewWindowRequested / CDP", "SignInPopup, PopupRegistry, MainForm", "Login windows are associated with the task that opened them, then released when the task finishes.", "I handled real-world OAuth/SSO pop-ups and their browser lifecycle."),
        new("02  Desktop / browser", "Network and console", "CDP Network and Runtime domains", "NetworkMonitor, ConsoleMonitor", "Diagnostics capture request outcomes and console errors without exposing routine secrets.", "I used CDP telemetry to explain navigation and form submission failures."),
        new("03  AI / orchestration", "LLM integration", "OpenAI Responses API", "OpenAIClient and AgentRunner", "A remote language model chooses tools and responds to structured tool results.", "I integrated an LLM through the Responses API with a local executable tool registry."),
        new("03  AI / orchestration", "Agent loop", "C# asynchronous task orchestration", "AgentRunner, ToolRegistry", "The agent iterates observe / decide / execute / verify and enforces step limits.", "I implemented a bounded tool-calling loop with explicit success and partial-failure outcomes."),
        new("03  AI / orchestration", "Model hierarchy", "Supervisor / worker architecture", "MCP task request + local Luna agent", "ChatGPT or Claude determines the objective; the lower-cost local browser agent performs actions.", "I separated expensive high-level planning from inexpensive browser execution."),
        new("03  AI / orchestration", "Prompt control", "System prompt and domain notes", "AgentRunner, SiteNotesStore", "Stable instructions are combined with current task and verified domain-specific knowledge.", "I keep reusable website playbooks separate from the user's instructions and secrets."),
        new("03  AI / orchestration", "Token and cost limits", "CostGuard; usage accounting", "CostGuard, RunMeter, AppConfig", "The process counts tokens, estimates cost and enforces per-task and per-domain limits.", "I designed cost-aware autonomous workflows with bounded retries and domain budgets."),
        new("04  Connectivity / protocols", "MCP", "Model Context Protocol", "McpServer, McpService, McpTaskManager", "An external assistant can request a browser task, read its state and submit replies.", "I exposed a native browser agent through MCP so independent assistants could delegate work."),
        new("04  Connectivity / protocols", "JSON-RPC 2.0", "JSON-RPC over Streamable HTTP", "McpServer", "MCP calls are JSON-RPC messages sent over HTTP POST; callers poll for state.", "I implemented an HTTP JSON-RPC service with tool discovery and task status semantics."),
        new("04  Connectivity / protocols", "HTTPS tunnel", "Cloudflare Tunnel", "CloudflareTunnel; McpService", "A secure tunnel can make the locally hosted MCP available to authorized remote clients.", "I used a tunnel to connect remote agents to a Windows-hosted MCP service without exposing a direct inbound port."),
        new("04  Connectivity / protocols", "Network transport", "HTTP, HTTPS, WebSocket", "MCP; CDP; file-transfer", "HTTP/JSON-RPC handles tasks, WebSocket handles standalone CDP, HTTPS secures remote transport.", "I used different transports according to latency and communication requirements."),
        new("04  Connectivity / protocols", "File transfer", "HTTP PUT/GET + SHA-256", "Mcp/FileTransfer", "Short-lived upload and download grants move files outside large JSON responses.", "I designed a file-transfer endpoint with temporary authorization and integrity checking."),
        new("04  Connectivity / protocols", "Task idempotency", "Idempotency keys / persistent task IDs", "McpTaskManager", "A repeated registration request reuses its task identity instead of repeating an irreversible action.", "I protected externally invoked automation against duplicate submissions after retries."),
        new("05  Data / security", "Credential vault", "Windows Credential Manager / WinCred API", "ImportedCredentialVault; TemporaryCredentialVault", "Passwords are stored as Windows generic credentials, separate from model-readable page data.", "I integrated OS-level credentials to keep imported passwords out of normal LLM prompts."),
        new("05  Data / security", "DPAPI", "Windows Data Protection API", "SecretStore, DpapiSecretProtector", "Local API secrets can be encrypted for the Windows account running OpenFill.", "I used DPAPI for per-user secret protection in the native application."),
        new("05  Data / security", "Firefox NSS", "Mozilla NSS and Firefox logins.json", "FirefoxLogins, FirefoxNssSession", "Selected entries can be decrypted locally from the Firefox profile after unlocking a primary password.", "I integrated Firefox's native credential storage without exporting the entire password database."),
        new("05  Data / security", "Domain isolation", "Origin boundaries, site-family and domain locks", "ImportedCredentialVault, DomainLocks", "Task scheduling and credential selection respect host and origin boundaries.", "I implemented origin-aware credential selection and per-domain concurrency limits."),
        new("05  Data / security", "Persistent memory", "Site notes JSON with atomic updates", "SiteNotesStore", "Reusable per-domain procedural facts are merged and deduplicated instead of replaying every navigation.", "I built a domain knowledge store that learns validated workflows across independent sessions."),
        new("05  Data / security", "Redaction", "Log and output sanitization", "Redactor, OutputLimiter", "Diagnostic output limits and redaction reduce accidental secret exposure.", "I separated operational telemetry from credentials and authentication tokens."),
        new("06  Operations / quality", "Windows process tree", "Windows APIs and Process metrics", "ResourceTelemetry", "Tracks OpenFill.exe plus its child renderer processes, CPU, RAM, handles and GC over time.", "I instrumented native multi-process software to find sustained CPU and RAM overhead."),
        new("06  Operations / quality", "Lifecycle management", "Tabs, cancellation, maintenance gate", "TabManager, TaskRun, AppHost", "Tasks have explicit states; cleanup must not accidentally terminate another request.", "I designed browser tab ownership and coordinated task lifecycle for multiple independent clients."),
        new("06  Operations / quality", "Release and rollback", "PowerShell, interactive Windows session", "tools/deploy_safely; backups", "Safe updates require version verification, maintenance, no active tasks and a recoverable previous release.", "I designed controlled Windows deployments with health checks and rollback."),
        new("06  Operations / quality", "Unit and E2E tests", "Local C# test harness and static test pages", "tests/OpenFill.Tests", "Local form/login servers exercise the real credential store and browser interactions.", "I built repeatable integration tests for a browser agent without generating accounts on public websites."),
        new("06  Operations / quality", "Status and observability", "NDJSON events, run history, telemetry", "EventLog, HistoryStore, ResourceTelemetry", "Per-task logs and resource samples survive the graphical UI closing.", "I combined persistent task journals and telemetry so execution can be diagnosed after a crash."),
        new("07  Ecosystem", "ChatGPT / Claude", "MCP client applications", "McpTaskManager", "External assistants send complete task requests and read outcomes from OpenFill.", "I built an integration boundary so multiple AI assistants can use one browser execution service."),
        new("07  Ecosystem", "Networking Radar", "Task integration via MCP", "integration_networking_radar docs", "A discovered event can become an idempotent registration request using the browser agent.", "I connected a discovery application to an independent browser execution service."),
        new("07  Ecosystem", "PBOX1 hosting", "Local Windows desktop deployment", "OpenFill/app, data/stable, Task Scheduler", "The user-facing browser runs on a Windows laptop in the interactive user session.", "I deployed the agent near the user's authenticated browser context rather than to a remote headless server.")
    };

    internal static string OneLine => $"OpenFill {BuildInfo.Version}   LIVE PID {Environment.ProcessId}   C#   .NET 10   WinForms   Edge WebView2   Chromium / CDP   OpenAI Responses   MCP + JSON-RPC   WinCred / DPAPI   Windows PBOX1";

    internal static Control CreateBanner(Action open, Action requestClose)
    {
        var banner = new TableLayoutPanel
        {
            Dock = DockStyle.Top, Height = 44, ColumnCount = 4, RowCount = 1,
            BackColor = Color.FromArgb(31, 41, 55), Padding = new Padding(7, 5, 7, 5), Margin = Padding.Empty
        };
        banner.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115));
        banner.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        banner.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155));
        banner.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        var heading = new Label { Text = "TECH ATLAS", ForeColor = Color.FromArgb(134, 239, 172), Font = new Font("Segoe UI", 9, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill };
        var summary = new Label { Text = OneLine, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, Dock = DockStyle.Fill, Cursor = Cursors.Hand };
        var explore = new Button { Text = "Explore architecture  >",
            ForeColor = Color.White, BackColor = Color.FromArgb(55, 65, 81),
            Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat };
        var quit = new Button { Text = "Close", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(89, 52, 52) };
        quit.FlatAppearance.BorderSize = 0;
        quit.Click += (_, _) => requestClose();
        explore.FlatAppearance.BorderSize = 0;
        explore.Click += (_, _) => open();
        summary.Click += (_, _) => open();
        var toolTip = new ToolTip();
        toolTip.SetToolTip(summary, "Select to explore technologies, architectural flows and interview explanations.");
        banner.Controls.Add(heading, 0, 0);
        banner.Controls.Add(summary, 1, 0);
        banner.Controls.Add(explore, 2, 0);
        banner.Controls.Add(quit, 3, 0);
        return banner;
    }
}