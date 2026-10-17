# OpenFill - package INDEX

## Metadata
- Document version: 1.52
- Date: 2026-10-14 22:59
- Package and app version: 1.52 (version date: 2026-10-14 22:59)
- Files: 143 (including this INDEX)
- Changed in this release: 23

Every file has a version in its name and in the Metadata section of its content. Exceptions marked (N) have a name required by tools; (B) are binary files - the version is in the name only. Audit: `pwsh tools/release*.ps1 -Check`.

| File | Version | Date | Changed | Role |
|---|---|---|---|---|
| `.gitignore` (N) | 0.1 | 2026-10-04 19:11 |  | files ignored by git |
| `Directory.Build.props` (N) | 0.2 | 2026-10-05 15:15 |  | shared build settings; app version and version date |
| `Directory.Build.props.before-v1.50__2026-10-14_0416__v0.2__2026-10-14_2235.bak` | 0.2 | 2026-10-14 22:35 |  |  |
| `docs/deployment__v1.50__2026-10-14_2235.md` | 1.50 | 2026-10-14 22:35 |  |  |
| `docs/handoff__v1.48__2026-10-14_2235.md` | 1.48 | 2026-10-14 22:35 |  |  |
| `docs/handoff__v1.49__2026-10-14_2259.md` | 1.49 | 2026-10-14 22:59 | yes |  |
| `docs/handoff__v1.49__2026-10-14_2259.md` | 1.49 | 2026-10-14 22:59 | yes |  |
| `docs/handoff__v1.49__2026-10-14_2259.md` | 1.49 | 2026-10-14 22:59 | yes |  |
| `docs/handoff_complete__v1.0__2026-10-14_2235.md` | 1.0 | 2026-10-14 22:35 |  |  |
| `docs/handoff_complete__v1.1__2026-10-14_2259.md` | 1.1 | 2026-10-14 22:59 | yes |  |
| `docs/integration_networking_radar__v0.2__2026-10-14_2235.md` | 0.2 | 2026-10-14 22:35 |  |  |
| `docs/integration_networking_radar__v0.3__2026-10-14_2259.md` | 0.3 | 2026-10-14 22:59 | yes |  |
| `docs/panel-preview__v0.1__2026-10-04_1911.png` (B) | 0.1 | 2026-10-04 19:11 |  | panel screenshot from a test (visual check) |
| `INSTALL__v0.1__2026-10-05_1123.cmd` | 0.1 | 2026-10-05 11:23 |  | double-click: install / update on Windows |
| `LICENSE__v0.2__2026-10-05_2212.txt` | 0.2 | 2026-10-05 22:12 |  |  |
| `OpenFill__v0.1__2026-10-04_1911.sln` | 0.1 | 2026-10-04 19:11 |  | solution file (Visual Studio) |
| `README__v0.17__2026-10-12_2126.md` | 0.17 | 2026-10-12 21:26 |  | app description, installation, usage, data, settings, architecture, development |
| `src/OpenFill.App/Assets/OpenFill__v1.0__2026-10-12_2034.ico` (B) | 1.0 | 2026-10-12 20:34 |  | solution file (Visual Studio) |
| `src/OpenFill.App/DpapiSecretProtector__v0.2__2026-10-05_1515.cs` | 0.2 | 2026-10-05 15:15 |  | key encryption with DPAPI |
| `src/OpenFill.App/FirefoxLogins__v0.1__2026-10-14_2235.cs` | 0.1 | 2026-10-14 22:35 |  |  |
| `src/OpenFill.App/FirefoxPasswordPicker__v0.1__2026-10-14_2235.cs` | 0.1 | 2026-10-14 22:35 |  |  |
| `src/OpenFill.App/MainForm__v0.22__2026-10-13_1035.cs.before-firefox__v0.22__2026-10-14_2235.bak` | 0.22 | 2026-10-14 22:35 |  |  |
| `src/OpenFill.App/MainForm__v0.24__2026-10-14_2259.cs` | 0.24 | 2026-10-14 22:59 | yes | app window: browser + panel |
| `src/OpenFill.App/NativeInput__v0.1__2026-10-13_1035.cs` | 0.1 | 2026-10-13 10:35 |  |  |
| `src/OpenFill.App/OpenFill.App__v0.3__2026-10-12_2034.csproj` | 0.3 | 2026-10-12 20:34 |  | project file OpenFill.App |
| `src/OpenFill.App/Program__v0.3__2026-10-05_1838.cs` | 0.3 | 2026-10-05 18:38 |  | entry point: OpenFill.App |
| `src/OpenFill.App/WebView2CdpConnection__v0.5__2026-10-13_1035.cs` | 0.5 | 2026-10-13 10:35 |  | CDP through WebView2 (UI thread) |
| `src/OpenFill.App/WebViewPanelTransport__v0.2__2026-10-05_1515.cs` | 0.2 | 2026-10-05 15:15 |  | panel channel through WebView2 |
| `src/OpenFill.Cli/CdpBootstrap__v0.3__2026-10-05_1515.cs` | 0.3 | 2026-10-05 15:15 |  | creating a tab and a CDP session |
| `src/OpenFill.Cli/ChromiumLauncher__v0.3__2026-10-05_1515.cs` | 0.3 | 2026-10-05 15:15 |  | starting Chromium/Edge with CDP |
| `src/OpenFill.Cli/CliOptions__v0.4__2026-10-05_1348.cs` | 0.4 | 2026-10-05 13:48 |  | CLI parameters |
| `src/OpenFill.Cli/ConsoleInteraction__v0.3__2026-10-05_1515.cs` | 0.3 | 2026-10-05 15:15 |  | model questions in the console |
| `src/OpenFill.Cli/MockModelClient__v0.3__2026-10-05_1515.cs` | 0.3 | 2026-10-05 15:15 |  | scripted model for tests (no network) |
| `src/OpenFill.Cli/OpenFill.Cli__v0.1__2026-10-04_1911.csproj` | 0.1 | 2026-10-04 19:11 |  | project file OpenFill.Cli |
| `src/OpenFill.Cli/PanelServer__v0.2__2026-10-05_1515.cs` | 0.2 | 2026-10-05 15:15 |  | panel server for the CLI (HTTP + WebSocket) |
| `src/OpenFill.Cli/Program__v0.4__2026-10-05_1515.cs` | 0.4 | 2026-10-05 15:15 |  | entry point: OpenFill.Cli |
| `src/OpenFill.Core/Agent/AgentRunner__v0.25__2026-10-14_2259.cs` | 0.25 | 2026-10-14 22:59 | yes | model loop (Responses API), limits, history compaction, images |
| `src/OpenFill.Core/Agent/CostGuard__v0.3__2026-10-07_1732.cs` | 0.3 | 2026-10-07 17:32 |  |  |
| `src/OpenFill.Core/Agent/GapRecorder__v0.5__2026-10-07_1732.cs` | 0.5 | 2026-10-07 17:32 |  | gap reports from the model |
| `src/OpenFill.Core/Agent/IDomainGate__v0.1__2026-10-07_1732.cs` | 0.1 | 2026-10-07 17:32 |  |  |
| `src/OpenFill.Core/Agent/ImportedCredentialVault__v0.1__2026-10-14_2235.cs` | 0.1 | 2026-10-14 22:35 |  |  |
| `src/OpenFill.Core/Agent/ImportedPasswordFiller__v0.1__2026-10-14_2235.cs` | 0.1 | 2026-10-14 22:35 |  |  |
| `src/OpenFill.Core/Agent/IUserInteraction__v0.6__2026-10-12_1750.cs` | 0.6 | 2026-10-12 17:50 |  | questions and consent (interface, unattended variant) |
| `src/OpenFill.Core/Agent/SiteNotesStore__v0.5__2026-10-14_2259.cs` | 0.5 | 2026-10-14 22:59 | yes | notes about sites (local; a central database later) |
| `src/OpenFill.Core/Agent/TemporaryCredentialVault__v0.1__2026-10-14_2235.cs` | 0.1 | 2026-10-14 22:35 |  |  |
| `src/OpenFill.Core/Agent/Tool__v0.2__2026-10-05_1200.cs` | 0.2 | 2026-10-05 12:00 |  | tool and result definition |
| `src/OpenFill.Core/Agent/ToolRegistry__v0.20__2026-10-14_2259.cs` | 0.20 | 2026-10-14 22:59 | yes | the model's internal tools |
| `src/OpenFill.Core/Assets/actions__v0.3__2026-10-05_1515.js` | 0.3 | 2026-10-05 15:15 |  | JS: page actions (set/type/click/select...) |
| `src/OpenFill.Core/Assets/AssetLoader__v0.3__2026-10-05_1200.cs` | 0.3 | 2026-10-05 12:00 |  | loading embedded JS/HTML resources |
| `src/OpenFill.Core/Assets/extractor__v0.5__2026-10-13_0958.js` | 0.5 | 2026-10-13 09:58 |  | JS: logical page structure (forms, fields, buttons, options, dialogs, messages) |
| `src/OpenFill.Core/Assets/panel__v0.18__2026-10-12_2034.html` | 0.18 | 2026-10-12 20:34 |  | live panel (HTML) - shared by the app and the CLI |
| `src/OpenFill.Core/Browser/BrowserController__v0.9__2026-10-14_2235.cs` | 0.9 | 2026-10-14 22:35 |  | page control through CDP: navigation, extraction, actions, real events, files, screenshots |
| `src/OpenFill.Core/Browser/ConsoleMonitor__v0.4__2026-10-07_1732.cs` | 0.4 | 2026-10-07 17:32 |  | page console and JavaScript errors |
| `src/OpenFill.Core/Browser/FrameLayer__v0.1__2026-10-12_2126.cs` | 0.1 | 2026-10-12 21:26 |  |  |
| `src/OpenFill.Core/Browser/NetworkMonitor__v0.6__2026-10-12_1905.cs` | 0.6 | 2026-10-12 19:05 |  | network traffic framed by requestId, response bodies, panel entries |
| `src/OpenFill.Core/Browser/PageModel__v0.3__2026-10-05_1515.cs` | 0.3 | 2026-10-05 15:15 |  | text picture of the page for the model and differences after an action |
| `src/OpenFill.Core/Browser/PopupRegistry__v0.1__2026-10-13_0600.cs` | 0.1 | 2026-10-13 06:00 |  |  |
| `src/OpenFill.Core/Browser/SignInPopup__v0.1__2026-10-12_2200.cs` | 0.1 | 2026-10-12 22:00 |  |  |
| `src/OpenFill.Core/BuildInfo__v0.4__2026-10-05_2131.cs` | 0.4 | 2026-10-05 21:31 |  | version, version date, build time |
| `src/OpenFill.Core/Cdp/CdpSession__v0.2__2026-10-05_1515.cs` | 0.2 | 2026-10-05 15:15 |  | CDP calls on a tab (evaluate, call) |
| `src/OpenFill.Core/Cdp/ICdpConnection__v0.2__2026-10-05_1515.cs` | 0.2 | 2026-10-05 15:15 |  | CDP channel abstraction |
| `src/OpenFill.Core/Cdp/SwitchableCdpConnection__v0.3__2026-10-13_1035.cs` | 0.3 | 2026-10-13 10:35 |  |  |
| `src/OpenFill.Core/Cdp/WebSocketCdpConnection__v0.2__2026-10-05_1515.cs` | 0.2 | 2026-10-05 15:15 |  | CDP over WebSocket (CLI) |
| `src/OpenFill.Core/Config/AppConfig__v0.14__2026-10-14_2259.cs` | 0.14 | 2026-10-14 22:59 | yes | settings (config.json) |
| `src/OpenFill.Core/Config/AppPaths__v0.7__2026-10-14_2259.cs` | 0.7 | 2026-10-14 22:59 | yes | instance directories (stable/dev) |
| `src/OpenFill.Core/Config/EmbeddedDefaults__v0.1__2026-10-07_1432.cs` | 0.1 | 2026-10-07 14:32 |  | values compiled into the build (key: always empty in the repository) |
| `src/OpenFill.Core/Config/SecretStore__v0.4__2026-10-07_1432.cs` | 0.4 | 2026-10-07 14:32 |  | OpenAI key (variable, encrypted file or built-in key) |
| `src/OpenFill.Core/Hosting/AppHost__v0.22__2026-10-14_2259.cs` | 0.22 | 2026-10-14 22:59 | yes | shared core of the app: panel <-> session, question and consent cards, tasks |
| `src/OpenFill.Core/Hosting/DomainLocks__v0.1__2026-10-07_1732.cs` | 0.1 | 2026-10-07 17:32 |  |  |
| `src/OpenFill.Core/Hosting/HistoryStore__v0.4__2026-10-12_1750.cs` | 0.4 | 2026-10-12 17:50 |  | task history: saved list of tasks (panel and MCP) with results, and a reader for the steps of one task |
| `src/OpenFill.Core/Hosting/IPanelTransport__v0.2__2026-10-05_1200.cs` | 0.2 | 2026-10-05 12:00 |  | panel <-> core channel (WebSocket or WebView2) |
| `src/OpenFill.Core/Hosting/OpenFillSession__v0.6__2026-10-07_1732.cs` | 0.6 | 2026-10-07 17:32 |  | session: log, browser, notes, task start |
| `src/OpenFill.Core/Hosting/RequesterColors__v0.2__2026-10-12_1917.cs` | 0.2 | 2026-10-12 19:17 |  |  |
| `src/OpenFill.Core/Hosting/TabManager__v0.6__2026-10-12_2200.cs` | 0.6 | 2026-10-12 22:00 |  |  |
| `src/OpenFill.Core/Hosting/TaskRun__v0.1__2026-10-07_1732.cs` | 0.1 | 2026-10-07 17:32 |  |  |
| `src/OpenFill.Core/Logging/EventLog__v0.5__2026-10-07_1732.cs` | 0.5 | 2026-10-07 17:32 |  | NDJSON log (global, per task, network) and the stream to the panel |
| `src/OpenFill.Core/Logging/OutputLimiter__v0.2__2026-10-05_1200.cs` | 0.2 | 2026-10-05 12:00 |  | tool output limit and overflow to a file |
| `src/OpenFill.Core/Logging/Redactor__v0.2__2026-10-05_1200.cs` | 0.2 | 2026-10-05 12:00 |  | removing secrets from logs and notes |
| `src/OpenFill.Core/Logging/ResourceTelemetry__v0.1__2026-10-14_2259.cs` | 0.1 | 2026-10-14 22:59 | yes |  |
| `src/OpenFill.Core/Mcp/CloudflareTunnel__v0.1__2026-10-05_1348.cs` | 0.1 | 2026-10-05 13:48 |  | temporary public HTTPS address for MCP (Cloudflare quick tunnel) |
| `src/OpenFill.Core/Mcp/FileTransfer__v0.1__2026-10-14_2235.cs` | 0.1 | 2026-10-14 22:35 |  |  |
| `src/OpenFill.Core/Mcp/IRunHost__v0.10__2026-10-12_1810.cs` | 0.10 | 2026-10-12 18:10 |  | interface between the MCP layer and the app: start/stop a task, route questions to the MCP caller |
| `src/OpenFill.Core/Mcp/McpServer__v0.22__2026-10-14_2235.cs` | 0.22 | 2026-10-14 22:35 |  | MCP server over HTTP (hand-written JSON-RPC, strict tool schemas, secret path) |
| `src/OpenFill.Core/Mcp/McpService__v0.4__2026-10-14_2235.cs` | 0.4 | 2026-10-14 22:35 |  | starts MCP: secret address, server, tunnel, self-test |
| `src/OpenFill.Core/Mcp/McpTaskManager__v0.17__2026-10-14_2235.cs` | 0.17 | 2026-10-14 22:35 |  | MCP tasks: one task at a time, task ids, status/reply/cancel, saved to disk |
| `src/OpenFill.Core/Model/KeyedModelClient__v0.3__2026-10-05_1200.cs` | 0.3 | 2026-10-05 12:00 |  | model client that reads the key at call time |
| `src/OpenFill.Core/Model/OpenAIClient__v0.2__2026-10-05_1200.cs` | 0.2 | 2026-10-05 12:00 |  | OpenAI Responses API client |
| `src/OpenFill.Core/OpenFill.Core__v0.2__2026-10-05_1515.csproj` | 0.2 | 2026-10-05 15:15 |  | project file OpenFill.Core |
| `tests/OpenFill.Tests/CostTests__v0.7__2026-10-12_1924.cs` | 0.7 | 2026-10-12 19:24 |  |  |
| `tests/OpenFill.Tests/DefaultsTests__v0.2__2026-10-07_1450.cs` | 0.2 | 2026-10-07 14:50 |  | tests: built-in prices, no key in the repository |
| `tests/OpenFill.Tests/FakeModel__v0.2__2026-10-05_1200.cs` | 0.2 | 2026-10-05 12:00 |  |  |
| `tests/OpenFill.Tests/FileTransferTests__v0.1__2026-10-14_2235.cs` | 0.1 | 2026-10-14 22:35 |  |  |
| `tests/OpenFill.Tests/McpTests__v0.12__2026-10-14_2235.cs` | 0.12 | 2026-10-14 22:35 |  | tests of the MCP layer (task life cycle and HTTP server) |
| `tests/OpenFill.Tests/OpenFill.Tests__v0.3__2026-10-14_2259.csproj` | 0.3 | 2026-10-14 22:59 | yes | project file OpenFill.Tests |
| `tests/OpenFill.Tests/ParallelTests__v0.4__2026-10-12_1924.cs` | 0.4 | 2026-10-12 19:24 |  |  |
| `tests/OpenFill.Tests/Program__v0.21__2026-10-14_2259.cs` | 0.21 | 2026-10-14 22:59 | yes | entry point: OpenFill.Tests |
| `tests/OpenFill.Tests/SharedTests__v0.1__2026-10-05_1905.cs` | 0.1 | 2026-10-05 19:05 |  |  |
| `tests/OpenFill.Tests/StaticSite__v0.5__2026-10-14_2235.cs` | 0.5 | 2026-10-14 22:35 |  | server of test pages with a simple API |
| `tests/OpenFill.Tests/TabTests__v0.6__2026-10-13_1035.cs` | 0.6 | 2026-10-13 10:35 |  |  |
| `tests/OpenFill.Tests/TestHarness__v0.2__2026-10-05_1200.cs` | 0.2 | 2026-10-05 12:00 |  | minimal test harness |
| `tests/OpenFill.Tests/testsite/profile__v0.1__2026-10-04_1911.html` | 0.1 | 2026-10-04 19:11 |  | test page: a hard form (modal, React, autocomplete, custom list, file) |
| `tests/OpenFill.Tests/testsite/xing__v0.3__2026-10-05_2131.html` | 0.3 | 2026-10-05 21:31 |  | test page: a simple profile form |
| `tools/backup__v0.1__2026-10-13_1955/AgentRunner__v0.23__2026-10-14_2235.cs` | 0.23 | 2026-10-14 22:35 |  | model loop (Responses API), limits, history compaction, images |
| `tools/backup__v0.1__2026-10-13_1955/McpServer__v0.21__2026-10-14_2235.cs` | 0.21 | 2026-10-14 22:35 |  | MCP server over HTTP (hand-written JSON-RPC, strict tool schemas, secret path) |
| `tools/backup__v0.1__2026-10-13_1955/McpService__v0.3__2026-10-14_2235.cs` | 0.3 | 2026-10-14 22:35 |  | starts MCP: secret address, server, tunnel, self-test |
| `tools/backup__v0.1__2026-10-13_1955/ToolRegistry__v0.18__2026-10-14_2235.cs` | 0.18 | 2026-10-14 22:35 |  | the model's internal tools |
| `tools/backup__v1.47__2026-10-13_2136/McpServer__v0.21__2026-10-14_2235.cs` | 0.21 | 2026-10-14 22:35 |  | MCP server over HTTP (hand-written JSON-RPC, strict tool schemas, secret path) |
| `tools/backup__v1.47__2026-10-13_2136/McpTaskManager__v0.16__2026-10-14_2235.cs` | 0.16 | 2026-10-14 22:35 |  | MCP tasks: one task at a time, task ids, status/reply/cancel, saved to disk |
| `tools/backup__v1.47__2026-10-13_2136/McpTests__v0.11__2026-10-14_2235.cs` | 0.11 | 2026-10-14 22:35 |  | tests of the MCP layer (task life cycle and HTTP server) |
| `tools/backup__v1.47__2026-10-13_2136/ToolRegistry__v0.18__2026-10-14_2235.cs` | 0.18 | 2026-10-14 22:35 |  | the model's internal tools |
| `tools/backup__v1.48__2026-10-13_2216/Directory.Build.props` (N) | 0.2 | 2026-10-14 22:35 |  | shared build settings; app version and version date |
| `tools/backup__v1.48__2026-10-13_2216/McpServer__v0.21__2026-10-14_2235.cs` | 0.21 | 2026-10-14 22:35 |  | MCP server over HTTP (hand-written JSON-RPC, strict tool schemas, secret path) |
| `tools/backup__v1.48__2026-10-13_2216/McpTaskManager__v0.16__2026-10-14_2235.cs` | 0.16 | 2026-10-14 22:35 |  | MCP tasks: one task at a time, task ids, status/reply/cancel, saved to disk |
| `tools/backup__v1.48__2026-10-13_2216/McpTests__v0.11__2026-10-14_2235.cs` | 0.11 | 2026-10-14 22:35 |  | tests of the MCP layer (task life cycle and HTTP server) |
| `tools/backup__v1.49__2026-10-13_2326/Directory.Build.props` (N) | 0.2 | 2026-10-14 22:35 |  | shared build settings; app version and version date |
| `tools/backup__v1.49__2026-10-13_2326/MainForm__v0.22__2026-10-14_2235.cs` | 0.22 | 2026-10-14 22:35 |  | app window: browser + panel |
| `tools/backup__v1.49__2026-10-14_002211/Directory.Build.props` (N) | 0.2 | 2026-10-14 22:35 |  | shared build settings; app version and version date |
| `tools/backup__v1.49__2026-10-14_002211/FirefoxPasswordPicker__v0.1__2026-10-14_2235.cs` | 0.1 | 2026-10-14 22:35 |  |  |
| `tools/backup__v1.49__2026-10-14_002211/MainForm__v0.22__2026-10-14_2235.cs` | 0.22 | 2026-10-14 22:35 |  | app window: browser + panel |
| `tools/compilecheck/CompileCheck__v0.2__2026-10-05_1515.csproj` | 0.2 | 2026-10-05 15:15 |  | compile check of the Windows app outside Windows |
| `tools/deploy__v1.48__2026-10-14_2235.ps1` | 1.48 | 2026-10-14 22:35 |  |  |
| `tools/deploy__v1.49__2026-10-14_2259.ps1` | 1.49 | 2026-10-14 22:59 | yes |  |
| `tools/deploy_launch__v0.1__2026-10-14_2235.ps1` | 0.1 | 2026-10-14 22:35 |  |  |
| `tools/deploy_safely__v0.1__2026-10-14_2235.ps1` | 0.1 | 2026-10-14 22:35 |  |  |
| `tools/install__v0.12__2026-10-12_2126.ps1` | 0.12 | 2026-10-12 21:26 |  | installer: .NET 10, WebView2, build, key, shortcuts |
| `tools/make-icon__v1.0__2026-10-12_2034.py` | 1.0 | 2026-10-12 20:34 |  |  |
| `tools/pin-taskbar__v1.0__2026-10-12_2126.ps1` | 1.0 | 2026-10-12 21:26 |  |  |
| `tools/publish__v0.3__2026-10-05_2211.ps1` | 0.3 | 2026-10-05 22:11 |  |  |
| `tools/publish__v1.47__2026-10-14_2235.log` | 1.47 | 2026-10-14 22:35 |  |  |
| `tools/publish__v1.48__2026-10-14_2259.log` | 1.48 | 2026-10-14 22:59 | yes |  |
| `tools/release__v0.6__2026-10-07_1432.ps1` | 0.6 | 2026-10-07 14:32 |  | release: file versions, Metadata, INDEX, zip, audit |
| `tools/run-dev__v0.2__2026-10-05_1515.ps1` | 0.2 | 2026-10-05 15:15 |  | run the development version (dev instance) |
| `tools/testlog__v0.3__2026-10-14_2235.txt` | 0.3 | 2026-10-14 22:35 |  |  |
| `tools/testlog__v0.4__2026-10-14_2259.txt` | 0.4 | 2026-10-14 22:59 | yes |  |
| `tools/testlog__v0.4__2026-10-14_2259.txt` | 0.4 | 2026-10-14 22:59 | yes |  |
| `tools/testlog__v0.4__2026-10-14_2259.txt` | 0.4 | 2026-10-14 22:59 | yes |  |
| `tools/testlog__v0.4__2026-10-14_2259.txt` | 0.4 | 2026-10-14 22:59 | yes |  |
| `tools/testlog__v0.4__2026-10-14_2259.txt` | 0.4 | 2026-10-14 22:59 | yes |  |
| `tools/testlog__v0.4__2026-10-14_2259.txt` | 0.4 | 2026-10-14 22:59 | yes |  |
| `tools/watchdog-install__v1.0__2026-10-07_1432.ps1` | 1.0 | 2026-10-07 14:32 |  | watchdog: registers the scheduled task (not run automatically) |
| `tools/watchdog-openfill__v1.0__2026-10-07_1432.ps1` | 1.0 | 2026-10-07 14:32 |  | watchdog: one check, restarts OpenFill when it is not running |
| `tools/watchdog-uninstall__v1.0__2026-10-07_1432.ps1` | 1.0 | 2026-10-07 14:32 |  | watchdog: removes the scheduled task |
| `INDEX__v1.52__2026-10-14_2259.md` | 1.52 | 2026-10-14 22:59 | yes | index of the package files (this file) |

## State for the next release

```json
{
    "package":  "1.52",
    "stamp":  "2026-10-14_2259",
    "files":  {
                  ".gitignore":  {
                                     "name":  ".gitignore",
                                     "version":  "0.1",
                                     "display":  "2026-10-04 19:11",
                                     "hash":  "b62e890b5bcfbb598a0d4a62020551b24e8fecd38ad16f3cbb2e95cf3d371b19"
                                 },
                  "Directory.Build.props":  {
                                                "name":  "Directory.Build.props",
                                                "version":  "0.2",
                                                "display":  "2026-10-05 15:15",
                                                "hash":  "e8ffb5541fc736f1499d07276df67ad60873edb8dc4607e11c557790b6b38f21"
                                            },
                  "Directory.Build.props.before-v1.50__2026-10-14_0416.bak":  {
                                                                                  "name":  "Directory.Build.props.before-v1.50__2026-10-14_0416__v0.2__2026-10-14_2235.bak",
                                                                                  "version":  "0.2",
                                                                                  "display":  "2026-10-14 22:35",
                                                                                  "hash":  "e8ffb5541fc736f1499d07276df67ad60873edb8dc4607e11c557790b6b38f21"
                                                                              },
                  "docs/deployment.md":  {
                                             "name":  "deployment__v1.50__2026-10-14_2235.md",
                                             "version":  "1.50",
                                             "display":  "2026-10-14 22:35",
                                             "hash":  "607632ff3ecd2748956e2f633dd518f61c00e2194c5cbc0a7e189573821275e8"
                                         },
                  "docs/handoff.md":  {
                                          "name":  "handoff__v1.49__2026-10-14_2259.md",
                                          "version":  "1.49",
                                          "display":  "2026-10-14 22:59",
                                          "hash":  "44a728dbcb9d589c23ec72e88466f18847f76d351ed82d617eb6c96c5020523a"
                                      },
                  "docs/handoff_complete.md":  {
                                                   "name":  "handoff_complete__v1.0__2026-10-14_2235.md",
                                                   "version":  "1.0",
                                                   "display":  "2026-10-14 22:35",
                                                   "hash":  "26bb213b51962be47700b6e9589c0e2f1fd9b4ab12ed7cfc6285f36f150af680"
                                               },
                  "docs/integration_networking_radar.md":  {
                                                               "name":  "integration_networking_radar__v0.2__2026-10-14_2235.md",
                                                               "version":  "0.2",
                                                               "display":  "2026-10-14 22:35",
                                                               "hash":  "7bf3680b5c80bfc02ec2dbdcb21cf85b33f7e2fe8bc8c62bd4f897682b002ca7"
                                                           },
                  "docs/panel-preview.png":  {
                                                 "name":  "panel-preview__v0.1__2026-10-04_1911.png",
                                                 "version":  "0.1",
                                                 "display":  "2026-10-04 19:11",
                                                 "hash":  "d4d94b9ed820d7fb7ceb3b1f01ccc133e99288ca073a639b53a960064461af9c"
                                             },
                  "INSTALL.cmd":  {
                                      "name":  "INSTALL__v0.1__2026-10-05_1123.cmd",
                                      "version":  "0.1",
                                      "display":  "2026-10-05 11:23",
                                      "hash":  "42cfe44fac81b022362d94eed0ea705e9c5bb3f44f383d4ec4d25e5d73461ae9"
                                  },
                  "LICENSE.txt":  {
                                      "name":  "LICENSE__v0.2__2026-10-05_2212.txt",
                                      "version":  "0.2",
                                      "display":  "2026-10-05 22:12",
                                      "hash":  "8ae2758dd5bb28b3bb866b4250f4e23228916451accd21b88142264d0568f485"
                                  },
                  "OpenFill.sln":  {
                                       "name":  "OpenFill__v0.1__2026-10-04_1911.sln",
                                       "version":  "0.1",
                                       "display":  "2026-10-04 19:11",
                                       "hash":  "df1239ce3dca4bf941e16fc5b24a733298c5459d4c7c64c037bb86bfbf09ec93"
                                   },
                  "README.md":  {
                                    "name":  "README__v0.17__2026-10-12_2126.md",
                                    "version":  "0.17",
                                    "display":  "2026-10-12 21:26",
                                    "hash":  "f3a0c6e53875a9734068a81a0d72dcc2ae445d937b79f76ff5800809fd54cb7f"
                                },
                  "src/OpenFill.App/Assets/OpenFill.ico":  {
                                                               "name":  "OpenFill__v1.0__2026-10-12_2034.ico",
                                                               "version":  "1.0",
                                                               "display":  "2026-10-12 20:34",
                                                               "hash":  "7983fc54538f2bec32df4d096ed6a0e721fa416b2af75f7f53da0b3bbc7132a2"
                                                           },
                  "src/OpenFill.App/DpapiSecretProtector.cs":  {
                                                                   "name":  "DpapiSecretProtector__v0.2__2026-10-05_1515.cs",
                                                                   "version":  "0.2",
                                                                   "display":  "2026-10-05 15:15",
                                                                   "hash":  "4120407799049c1efd240943b34737f5b36eafd397385bcff4a0cdc73f756ce8"
                                                               },
                  "src/OpenFill.App/FirefoxLogins.cs":  {
                                                            "name":  "FirefoxLogins__v0.1__2026-10-14_2235.cs",
                                                            "version":  "0.1",
                                                            "display":  "2026-10-14 22:35",
                                                            "hash":  "c615da50a8b98a5766267227ed8c8b63877390f87c0f12a0f7aa83ee885dbe64"
                                                        },
                  "src/OpenFill.App/FirefoxPasswordPicker.cs":  {
                                                                    "name":  "FirefoxPasswordPicker__v0.1__2026-10-14_2235.cs",
                                                                    "version":  "0.1",
                                                                    "display":  "2026-10-14 22:35",
                                                                    "hash":  "10f5886a0f198ebc4de1d7aa36c76caa965a0194c9be608aa1633268f496dc67"
                                                                },
                  "src/OpenFill.App/MainForm.cs":  {
                                                       "name":  "MainForm__v0.24__2026-10-14_2259.cs",
                                                       "version":  "0.24",
                                                       "display":  "2026-10-14 22:59",
                                                       "hash":  "21829ee214fba92f1bd4d9c1fd8b17e3f2bf3e56cd8428c57d1235557a586613"
                                                   },
                  "src/OpenFill.App/MainForm__v0.22__2026-10-13_1035.cs.before-firefox.bak":  {
                                                                                                  "name":  "MainForm__v0.22__2026-10-13_1035.cs.before-firefox__v0.22__2026-10-14_2235.bak",
                                                                                                  "version":  "0.22",
                                                                                                  "display":  "2026-10-14 22:35",
                                                                                                  "hash":  "f1edc5ef5e062e5995126e22956a68be05ade897f134a75decabf3aab75a0ce4"
                                                                                              },
                  "src/OpenFill.App/NativeInput.cs":  {
                                                          "name":  "NativeInput__v0.1__2026-10-13_1035.cs",
                                                          "version":  "0.1",
                                                          "display":  "2026-10-13 10:35",
                                                          "hash":  "992e374c4629c6153daf061978ffeda872240bdaca86c401baa34f3f04b90c94"
                                                      },
                  "src/OpenFill.App/OpenFill.App.csproj":  {
                                                               "name":  "OpenFill.App__v0.3__2026-10-12_2034.csproj",
                                                               "version":  "0.3",
                                                               "display":  "2026-10-12 20:34",
                                                               "hash":  "9a55e8201db8202103f76b525ddcc8bead1f673e5a2c022529aea53a3e3aad44"
                                                           },
                  "src/OpenFill.App/Program.cs":  {
                                                      "name":  "Program__v0.3__2026-10-05_1838.cs",
                                                      "version":  "0.3",
                                                      "display":  "2026-10-05 18:38",
                                                      "hash":  "1d165f335e98ddc774f1ee249fa0fd30380bd06120ca095c6b8d0f65dd7d2b3b"
                                                  },
                  "src/OpenFill.App/WebView2CdpConnection.cs":  {
                                                                    "name":  "WebView2CdpConnection__v0.5__2026-10-13_1035.cs",
                                                                    "version":  "0.5",
                                                                    "display":  "2026-10-13 10:35",
                                                                    "hash":  "df582d85aadd8cace55b3ccb4a15bde249619629101a6338a5ec9c935479e956"
                                                                },
                  "src/OpenFill.App/WebViewPanelTransport.cs":  {
                                                                    "name":  "WebViewPanelTransport__v0.2__2026-10-05_1515.cs",
                                                                    "version":  "0.2",
                                                                    "display":  "2026-10-05 15:15",
                                                                    "hash":  "8e6c911c3c73bbbca75649296c16d8254ba0e22f5e6bcb62b9b49f5aeff96712"
                                                                },
                  "src/OpenFill.Cli/CdpBootstrap.cs":  {
                                                           "name":  "CdpBootstrap__v0.3__2026-10-05_1515.cs",
                                                           "version":  "0.3",
                                                           "display":  "2026-10-05 15:15",
                                                           "hash":  "b48d6488ed1ae1a2f4de3be01950d3ff5c732f7bdd16eee07818aa20b63f9723"
                                                       },
                  "src/OpenFill.Cli/ChromiumLauncher.cs":  {
                                                               "name":  "ChromiumLauncher__v0.3__2026-10-05_1515.cs",
                                                               "version":  "0.3",
                                                               "display":  "2026-10-05 15:15",
                                                               "hash":  "aa15c7b62419f464e1afc436ac2edb1b9a140e852d366321b854e9a8a503cf68"
                                                           },
                  "src/OpenFill.Cli/CliOptions.cs":  {
                                                         "name":  "CliOptions__v0.4__2026-10-05_1348.cs",
                                                         "version":  "0.4",
                                                         "display":  "2026-10-05 13:48",
                                                         "hash":  "9b997fdc671ade062b980cd841cacc2ca07408c90eb8b8b5699b964097f237fb"
                                                     },
                  "src/OpenFill.Cli/ConsoleInteraction.cs":  {
                                                                 "name":  "ConsoleInteraction__v0.3__2026-10-05_1515.cs",
                                                                 "version":  "0.3",
                                                                 "display":  "2026-10-05 15:15",
                                                                 "hash":  "c55c10b1516dd60feba63060376c98fcb3a2b2057b2f34eb8bbdaa17bbe2ef27"
                                                             },
                  "src/OpenFill.Cli/MockModelClient.cs":  {
                                                              "name":  "MockModelClient__v0.3__2026-10-05_1515.cs",
                                                              "version":  "0.3",
                                                              "display":  "2026-10-05 15:15",
                                                              "hash":  "f954a16fbdd1ee7e7d59f709ca15e67f4ae4f886e81e35aac13b647e6494c379"
                                                          },
                  "src/OpenFill.Cli/OpenFill.Cli.csproj":  {
                                                               "name":  "OpenFill.Cli__v0.1__2026-10-04_1911.csproj",
                                                               "version":  "0.1",
                                                               "display":  "2026-10-04 19:11",
                                                               "hash":  "b2099d5b3b4eea74c96c3769c01e53a4d9450d20f3bdc8516bc441554a0fcb4d"
                                                           },
                  "src/OpenFill.Cli/PanelServer.cs":  {
                                                          "name":  "PanelServer__v0.2__2026-10-05_1515.cs",
                                                          "version":  "0.2",
                                                          "display":  "2026-10-05 15:15",
                                                          "hash":  "a1d11bf2daac730212ce301158001fb2622d796a7a2cdfc336d3c3c46cb54a19"
                                                      },
                  "src/OpenFill.Cli/Program.cs":  {
                                                      "name":  "Program__v0.4__2026-10-05_1515.cs",
                                                      "version":  "0.4",
                                                      "display":  "2026-10-05 15:15",
                                                      "hash":  "ed0dfe289542cead495408e8637ff747d6dd20f2798d4030881fc9eb32ae1d15"
                                                  },
                  "src/OpenFill.Core/Agent/AgentRunner.cs":  {
                                                                 "name":  "AgentRunner__v0.25__2026-10-14_2259.cs",
                                                                 "version":  "0.25",
                                                                 "display":  "2026-10-14 22:59",
                                                                 "hash":  "1a364ec2494186d600ff0991783a901cb30577e0c9bc0fbb7d93269fae8e5478"
                                                             },
                  "src/OpenFill.Core/Agent/CostGuard.cs":  {
                                                               "name":  "CostGuard__v0.3__2026-10-07_1732.cs",
                                                               "version":  "0.3",
                                                               "display":  "2026-10-07 17:32",
                                                               "hash":  "451a8df0017ad137a16a88764d144608b525f4468e162dd5e20b885af76089f6"
                                                           },
                  "src/OpenFill.Core/Agent/GapRecorder.cs":  {
                                                                 "name":  "GapRecorder__v0.5__2026-10-07_1732.cs",
                                                                 "version":  "0.5",
                                                                 "display":  "2026-10-07 17:32",
                                                                 "hash":  "018977021f2463810c668dbb55b57a2390ab7dffdb50f18969930667b1eec21d"
                                                             },
                  "src/OpenFill.Core/Agent/IDomainGate.cs":  {
                                                                 "name":  "IDomainGate__v0.1__2026-10-07_1732.cs",
                                                                 "version":  "0.1",
                                                                 "display":  "2026-10-07 17:32",
                                                                 "hash":  "e5d1d5ae0cc7fa41a3faa69f358cd3a2bcf8f2b7cf150244b800e8ffdb625958"
                                                             },
                  "src/OpenFill.Core/Agent/ImportedCredentialVault.cs":  {
                                                                             "name":  "ImportedCredentialVault__v0.1__2026-10-14_2235.cs",
                                                                             "version":  "0.1",
                                                                             "display":  "2026-10-14 22:35",
                                                                             "hash":  "46c6892a46f54ef2b3b705bf53545d68294c8b71491cd0077738e66431b940f9"
                                                                         },
                  "src/OpenFill.Core/Agent/ImportedPasswordFiller.cs":  {
                                                                            "name":  "ImportedPasswordFiller__v0.1__2026-10-14_2235.cs",
                                                                            "version":  "0.1",
                                                                            "display":  "2026-10-14 22:35",
                                                                            "hash":  "fbd673008d0fd196577f4e145e959c88d66efc7f3dac6a32d74bc9566544946b"
                                                                        },
                  "src/OpenFill.Core/Agent/IUserInteraction.cs":  {
                                                                      "name":  "IUserInteraction__v0.6__2026-10-12_1750.cs",
                                                                      "version":  "0.6",
                                                                      "display":  "2026-10-12 17:50",
                                                                      "hash":  "c14366b298e94567e794e2e849e6df68bb313925f59e01ba6b129d779848d5d1"
                                                                  },
                  "src/OpenFill.Core/Agent/SiteNotesStore.cs":  {
                                                                    "name":  "SiteNotesStore__v0.5__2026-10-14_2259.cs",
                                                                    "version":  "0.5",
                                                                    "display":  "2026-10-14 22:59",
                                                                    "hash":  "779b0378b5279485c89c1a8f8d013a7f8f7c86fe73aa0af94487a5af3cdf1b11"
                                                                },
                  "src/OpenFill.Core/Agent/TemporaryCredentialVault.cs":  {
                                                                              "name":  "TemporaryCredentialVault__v0.1__2026-10-14_2235.cs",
                                                                              "version":  "0.1",
                                                                              "display":  "2026-10-14 22:35",
                                                                              "hash":  "964951cd0eaf76acc50f23ce75b38b3473e1b39ea5865ecc5efc2f76b83f235e"
                                                                          },
                  "src/OpenFill.Core/Agent/Tool.cs":  {
                                                          "name":  "Tool__v0.2__2026-10-05_1200.cs",
                                                          "version":  "0.2",
                                                          "display":  "2026-10-05 12:00",
                                                          "hash":  "a6211d753bf4c270f903b923e9a3f453800f9ffbc118ae821e8a962686a7ea1e"
                                                      },
                  "src/OpenFill.Core/Agent/ToolRegistry.cs":  {
                                                                  "name":  "ToolRegistry__v0.20__2026-10-14_2259.cs",
                                                                  "version":  "0.20",
                                                                  "display":  "2026-10-14 22:59",
                                                                  "hash":  "2c260928a13183ad60b9402ede31ae87560da5bdce75c6cef2318b02049c94a8"
                                                              },
                  "src/OpenFill.Core/Assets/actions.js":  {
                                                              "name":  "actions__v0.3__2026-10-05_1515.js",
                                                              "version":  "0.3",
                                                              "display":  "2026-10-05 15:15",
                                                              "hash":  "8e6b44f07db5e8f6781de2ccdfe3e0b4e60d4171023d5982055d7116584a4245"
                                                          },
                  "src/OpenFill.Core/Assets/AssetLoader.cs":  {
                                                                  "name":  "AssetLoader__v0.3__2026-10-05_1200.cs",
                                                                  "version":  "0.3",
                                                                  "display":  "2026-10-05 12:00",
                                                                  "hash":  "373a9735723b66c786c022fa02f7e0939ce16e00133c946f77021b2553ebd8ff"
                                                              },
                  "src/OpenFill.Core/Assets/extractor.js":  {
                                                                "name":  "extractor__v0.5__2026-10-13_0958.js",
                                                                "version":  "0.5",
                                                                "display":  "2026-10-13 09:58",
                                                                "hash":  "9cdd1b08d906c94f464e6a8ac84bf996dc83b1799cee7e33a310d89ee18e6934"
                                                            },
                  "src/OpenFill.Core/Assets/panel.html":  {
                                                              "name":  "panel__v0.18__2026-10-12_2034.html",
                                                              "version":  "0.18",
                                                              "display":  "2026-10-12 20:34",
                                                              "hash":  "fc4b60ea6820317ea3e1e923ee67e6df926bbab4e008f089a5b3dd5e50b74342"
                                                          },
                  "src/OpenFill.Core/Browser/BrowserController.cs":  {
                                                                         "name":  "BrowserController__v0.9__2026-10-14_2235.cs",
                                                                         "version":  "0.9",
                                                                         "display":  "2026-10-14 22:35",
                                                                         "hash":  "c30c3418707b8e8a6337dcbc5b0b0de5abdcaf7d0be79ffff3589d9619c5eaf3"
                                                                     },
                  "src/OpenFill.Core/Browser/ConsoleMonitor.cs":  {
                                                                      "name":  "ConsoleMonitor__v0.4__2026-10-07_1732.cs",
                                                                      "version":  "0.4",
                                                                      "display":  "2026-10-07 17:32",
                                                                      "hash":  "c377f6d6d19a2451aed5e38d090ee89ca96e0aa7157375e1f0621f87732cf840"
                                                                  },
                  "src/OpenFill.Core/Browser/FrameLayer.cs":  {
                                                                  "name":  "FrameLayer__v0.1__2026-10-12_2126.cs",
                                                                  "version":  "0.1",
                                                                  "display":  "2026-10-12 21:26",
                                                                  "hash":  "e6ac0fb770b240e31d93892c63e08239b8bf2968d4f8ff5844f16c23169de505"
                                                              },
                  "src/OpenFill.Core/Browser/NetworkMonitor.cs":  {
                                                                      "name":  "NetworkMonitor__v0.6__2026-10-12_1905.cs",
                                                                      "version":  "0.6",
                                                                      "display":  "2026-10-12 19:05",
                                                                      "hash":  "b79839afbd1405e02b96cf6d44421f6afdf9a793dc0a01eb010a9809271b5482"
                                                                  },
                  "src/OpenFill.Core/Browser/PageModel.cs":  {
                                                                 "name":  "PageModel__v0.3__2026-10-05_1515.cs",
                                                                 "version":  "0.3",
                                                                 "display":  "2026-10-05 15:15",
                                                                 "hash":  "2966e35ecf5b313afa14fae3abd5113b4ea3b9e3ee02fe15b0198da4d4c34394"
                                                             },
                  "src/OpenFill.Core/Browser/PopupRegistry.cs":  {
                                                                     "name":  "PopupRegistry__v0.1__2026-10-13_0600.cs",
                                                                     "version":  "0.1",
                                                                     "display":  "2026-10-13 06:00",
                                                                     "hash":  "831cbf5b17f0c5597d8d8829eb17e0f084f9c11eb6f4826aa5b4dc739edf6195"
                                                                 },
                  "src/OpenFill.Core/Browser/SignInPopup.cs":  {
                                                                   "name":  "SignInPopup__v0.1__2026-10-12_2200.cs",
                                                                   "version":  "0.1",
                                                                   "display":  "2026-10-12 22:00",
                                                                   "hash":  "2ecd1b6d403e72dc1724e388a538eabd3173d58f233125aa8387c765ba8b95bb"
                                                               },
                  "src/OpenFill.Core/BuildInfo.cs":  {
                                                         "name":  "BuildInfo__v0.4__2026-10-05_2131.cs",
                                                         "version":  "0.4",
                                                         "display":  "2026-10-05 21:31",
                                                         "hash":  "0ba1748851bbd23642d843fa88d1bf1de70b93061a103d9b2e12378173929a1c"
                                                     },
                  "src/OpenFill.Core/Cdp/CdpSession.cs":  {
                                                              "name":  "CdpSession__v0.2__2026-10-05_1515.cs",
                                                              "version":  "0.2",
                                                              "display":  "2026-10-05 15:15",
                                                              "hash":  "8ef952432064ad7643690ec764deea0a12774964b4feece9c35801c9782ad8ff"
                                                          },
                  "src/OpenFill.Core/Cdp/ICdpConnection.cs":  {
                                                                  "name":  "ICdpConnection__v0.2__2026-10-05_1515.cs",
                                                                  "version":  "0.2",
                                                                  "display":  "2026-10-05 15:15",
                                                                  "hash":  "bdad91e83e55b037f11973e21de9dcd18eb538081e98e80b9e907466fdab5a01"
                                                              },
                  "src/OpenFill.Core/Cdp/SwitchableCdpConnection.cs":  {
                                                                           "name":  "SwitchableCdpConnection__v0.3__2026-10-13_1035.cs",
                                                                           "version":  "0.3",
                                                                           "display":  "2026-10-13 10:35",
                                                                           "hash":  "7f746115fcc5b374f7777fdc0af3d982465adec16c04282215954608a9dbc571"
                                                                       },
                  "src/OpenFill.Core/Cdp/WebSocketCdpConnection.cs":  {
                                                                          "name":  "WebSocketCdpConnection__v0.2__2026-10-05_1515.cs",
                                                                          "version":  "0.2",
                                                                          "display":  "2026-10-05 15:15",
                                                                          "hash":  "89a0c3730674af4cfdebb248ac7baa32a39b789c3209093d0bf2f1931b2fe98d"
                                                                      },
                  "src/OpenFill.Core/Config/AppConfig.cs":  {
                                                                "name":  "AppConfig__v0.14__2026-10-14_2259.cs",
                                                                "version":  "0.14",
                                                                "display":  "2026-10-14 22:59",
                                                                "hash":  "fca049cea41c41c98e000140ac0cb29efd1d942d8462da583912c43be8bedcee"
                                                            },
                  "src/OpenFill.Core/Config/AppPaths.cs":  {
                                                               "name":  "AppPaths__v0.7__2026-10-14_2259.cs",
                                                               "version":  "0.7",
                                                               "display":  "2026-10-14 22:59",
                                                               "hash":  "914fa8287c86666693ec05f4fae45190e4d947f2e363adeeeba9dd54f94755ee"
                                                           },
                  "src/OpenFill.Core/Config/EmbeddedDefaults.cs":  {
                                                                       "name":  "EmbeddedDefaults__v0.1__2026-10-07_1432.cs",
                                                                       "version":  "0.1",
                                                                       "display":  "2026-10-07 14:32",
                                                                       "hash":  "0c75872b39786ac78500d4cb60dabae8fac29636064222f3c0a877fe2108b2eb"
                                                                   },
                  "src/OpenFill.Core/Config/SecretStore.cs":  {
                                                                  "name":  "SecretStore__v0.4__2026-10-07_1432.cs",
                                                                  "version":  "0.4",
                                                                  "display":  "2026-10-07 14:32",
                                                                  "hash":  "61c836da25d1e03ac56d2e4f693d0cfbf3a230356860d824aa8b295258a13d74"
                                                              },
                  "src/OpenFill.Core/Hosting/AppHost.cs":  {
                                                               "name":  "AppHost__v0.22__2026-10-14_2259.cs",
                                                               "version":  "0.22",
                                                               "display":  "2026-10-14 22:59",
                                                               "hash":  "c0c5b89b9b49757435f4fc5cf984f127b96ab2729ad5542268d4b7f79a1b5a2d"
                                                           },
                  "src/OpenFill.Core/Hosting/DomainLocks.cs":  {
                                                                   "name":  "DomainLocks__v0.1__2026-10-07_1732.cs",
                                                                   "version":  "0.1",
                                                                   "display":  "2026-10-07 17:32",
                                                                   "hash":  "85f9362a3397813ea268b5e590b016131aa4e1e2ab916255a8651a66d3be9879"
                                                               },
                  "src/OpenFill.Core/Hosting/HistoryStore.cs":  {
                                                                    "name":  "HistoryStore__v0.4__2026-10-12_1750.cs",
                                                                    "version":  "0.4",
                                                                    "display":  "2026-10-12 17:50",
                                                                    "hash":  "75c6c734b32f5b536f4aa541e2de045390b7f18617072723df53ea82f8bc80b1"
                                                                },
                  "src/OpenFill.Core/Hosting/IPanelTransport.cs":  {
                                                                       "name":  "IPanelTransport__v0.2__2026-10-05_1200.cs",
                                                                       "version":  "0.2",
                                                                       "display":  "2026-10-05 12:00",
                                                                       "hash":  "5b96293f8368c2fa5205b748575c68b0cef7052c2f50cfb71bbf1d5209ee1fed"
                                                                   },
                  "src/OpenFill.Core/Hosting/OpenFillSession.cs":  {
                                                                       "name":  "OpenFillSession__v0.6__2026-10-07_1732.cs",
                                                                       "version":  "0.6",
                                                                       "display":  "2026-10-07 17:32",
                                                                       "hash":  "4699e54e2621e37e2ce95eccf2404281c20a81a061521141caa9e467d2204f39"
                                                                   },
                  "src/OpenFill.Core/Hosting/RequesterColors.cs":  {
                                                                       "name":  "RequesterColors__v0.2__2026-10-12_1917.cs",
                                                                       "version":  "0.2",
                                                                       "display":  "2026-10-12 19:17",
                                                                       "hash":  "14165ebac380524a82f43fa9827f03f6a5c94c332acd1f74d671981bfacff2b5"
                                                                   },
                  "src/OpenFill.Core/Hosting/TabManager.cs":  {
                                                                  "name":  "TabManager__v0.6__2026-10-12_2200.cs",
                                                                  "version":  "0.6",
                                                                  "display":  "2026-10-12 22:00",
                                                                  "hash":  "c53798174755fad3283df22b6e12e33d8f3d2b102fa61a6b3b2e81da3f2dcba2"
                                                              },
                  "src/OpenFill.Core/Hosting/TaskRun.cs":  {
                                                               "name":  "TaskRun__v0.1__2026-10-07_1732.cs",
                                                               "version":  "0.1",
                                                               "display":  "2026-10-07 17:32",
                                                               "hash":  "770a1b4f498af384a5fb477fa2620ee58398a287671d1ba4a352fd67dd21bb8a"
                                                           },
                  "src/OpenFill.Core/Logging/EventLog.cs":  {
                                                                "name":  "EventLog__v0.5__2026-10-07_1732.cs",
                                                                "version":  "0.5",
                                                                "display":  "2026-10-07 17:32",
                                                                "hash":  "085c3b2cf24486219e2bdea91ed866e8ecd49ae6f82f3fb230e4ac704a3e584f"
                                                            },
                  "src/OpenFill.Core/Logging/OutputLimiter.cs":  {
                                                                     "name":  "OutputLimiter__v0.2__2026-10-05_1200.cs",
                                                                     "version":  "0.2",
                                                                     "display":  "2026-10-05 12:00",
                                                                     "hash":  "5041325aadd5b2537bcc68b44e790f7b2537ae9b7a99fe31866cbd3146f03ca4"
                                                                 },
                  "src/OpenFill.Core/Logging/Redactor.cs":  {
                                                                "name":  "Redactor__v0.2__2026-10-05_1200.cs",
                                                                "version":  "0.2",
                                                                "display":  "2026-10-05 12:00",
                                                                "hash":  "e198180aa3f0f761198cc110c0a21078bed30c747922c34f5dd682e3d4cabe49"
                                                            },
                  "src/OpenFill.Core/Logging/ResourceTelemetry.cs":  {
                                                                         "name":  "ResourceTelemetry__v0.1__2026-10-14_2259.cs",
                                                                         "version":  "0.1",
                                                                         "display":  "2026-10-14 22:59",
                                                                         "hash":  "1a0e548e42a84a59d0e8018e79b48215098f25e54df3dbc1c1132931a1a3a007"
                                                                     },
                  "src/OpenFill.Core/Mcp/CloudflareTunnel.cs":  {
                                                                    "name":  "CloudflareTunnel__v0.1__2026-10-05_1348.cs",
                                                                    "version":  "0.1",
                                                                    "display":  "2026-10-05 13:48",
                                                                    "hash":  "0a1ffde439d3786f12765bf5646f9eaabe44fed42f54d841fa4b69e18dc0e774"
                                                                },
                  "src/OpenFill.Core/Mcp/FileTransfer.cs":  {
                                                                "name":  "FileTransfer__v0.1__2026-10-14_2235.cs",
                                                                "version":  "0.1",
                                                                "display":  "2026-10-14 22:35",
                                                                "hash":  "a697101ba19dbd4cb2f8134ac5f44ad58706b59f6192bdd2b10c90b48e74da6d"
                                                            },
                  "src/OpenFill.Core/Mcp/IRunHost.cs":  {
                                                            "name":  "IRunHost__v0.10__2026-10-12_1810.cs",
                                                            "version":  "0.10",
                                                            "display":  "2026-10-12 18:10",
                                                            "hash":  "5c09dcd9a9bdd4b4471d4c09d8bdb1931424baa7aff5d47cb31d1a3c5e676ee5"
                                                        },
                  "src/OpenFill.Core/Mcp/McpServer.cs":  {
                                                             "name":  "McpServer__v0.22__2026-10-14_2235.cs",
                                                             "version":  "0.22",
                                                             "display":  "2026-10-14 22:35",
                                                             "hash":  "e6be06e4556ed74586eed94263f2d32ac4e12c24ca0cee0a514752725e0d63fc"
                                                         },
                  "src/OpenFill.Core/Mcp/McpService.cs":  {
                                                              "name":  "McpService__v0.4__2026-10-14_2235.cs",
                                                              "version":  "0.4",
                                                              "display":  "2026-10-14 22:35",
                                                              "hash":  "528ba6e146391384ca9cb3bb2678fedc585d091b63c5b8718631c8055dd2b6dc"
                                                          },
                  "src/OpenFill.Core/Mcp/McpTaskManager.cs":  {
                                                                  "name":  "McpTaskManager__v0.17__2026-10-14_2235.cs",
                                                                  "version":  "0.17",
                                                                  "display":  "2026-10-14 22:35",
                                                                  "hash":  "7a8662815e0461c142af5df10018783964f567270e987846075f6f73f0ca2e11"
                                                              },
                  "src/OpenFill.Core/Model/KeyedModelClient.cs":  {
                                                                      "name":  "KeyedModelClient__v0.3__2026-10-05_1200.cs",
                                                                      "version":  "0.3",
                                                                      "display":  "2026-10-05 12:00",
                                                                      "hash":  "444d37d6e57f94763d25735101231f2348f084ef653b7282d3be3cf2fee6b673"
                                                                  },
                  "src/OpenFill.Core/Model/OpenAIClient.cs":  {
                                                                  "name":  "OpenAIClient__v0.2__2026-10-05_1200.cs",
                                                                  "version":  "0.2",
                                                                  "display":  "2026-10-05 12:00",
                                                                  "hash":  "ac3243e584e44e8376028a536292c45386a2b296d22c3ac9f5a797eb487fdbe5"
                                                              },
                  "src/OpenFill.Core/OpenFill.Core.csproj":  {
                                                                 "name":  "OpenFill.Core__v0.2__2026-10-05_1515.csproj",
                                                                 "version":  "0.2",
                                                                 "display":  "2026-10-05 15:15",
                                                                 "hash":  "8439da43e6647455c40d11b59c4a53d3f302622721a929504e9b8bb9630944a6"
                                                             },
                  "tests/OpenFill.Tests/CostTests.cs":  {
                                                            "name":  "CostTests__v0.7__2026-10-12_1924.cs",
                                                            "version":  "0.7",
                                                            "display":  "2026-10-12 19:24",
                                                            "hash":  "a34c7af686b2348532fe75ffd2750156ec6699268cfe4350656336b669a52650"
                                                        },
                  "tests/OpenFill.Tests/DefaultsTests.cs":  {
                                                                "name":  "DefaultsTests__v0.2__2026-10-07_1450.cs",
                                                                "version":  "0.2",
                                                                "display":  "2026-10-07 14:50",
                                                                "hash":  "3b2b62a30053ec46263cf6ebea5670e00ed1689f2772f53412f27c06e035f0e3"
                                                            },
                  "tests/OpenFill.Tests/FakeModel.cs":  {
                                                            "name":  "FakeModel__v0.2__2026-10-05_1200.cs",
                                                            "version":  "0.2",
                                                            "display":  "2026-10-05 12:00",
                                                            "hash":  "a39a8c1e78007f9fb44ec7681dbda0c1c6218b6db6bda59a75bf0ad8b17786f8"
                                                        },
                  "tests/OpenFill.Tests/FileTransferTests.cs":  {
                                                                    "name":  "FileTransferTests__v0.1__2026-10-14_2235.cs",
                                                                    "version":  "0.1",
                                                                    "display":  "2026-10-14 22:35",
                                                                    "hash":  "48247aba88a0056749c48a3e2f2a066efd098bfe55e86feb6c7d5a2312e527b6"
                                                                },
                  "tests/OpenFill.Tests/McpTests.cs":  {
                                                           "name":  "McpTests__v0.12__2026-10-14_2235.cs",
                                                           "version":  "0.12",
                                                           "display":  "2026-10-14 22:35",
                                                           "hash":  "fabf00a6f980dde2e796d1bc213027bd70e88696a987662fc532fe0c6435c090"
                                                       },
                  "tests/OpenFill.Tests/OpenFill.Tests.csproj":  {
                                                                     "name":  "OpenFill.Tests__v0.3__2026-10-14_2259.csproj",
                                                                     "version":  "0.3",
                                                                     "display":  "2026-10-14 22:59",
                                                                     "hash":  "16254c46404fce40b533b55ef701c176e26f8119189e5a0983e1c6acc4b68f25"
                                                                 },
                  "tests/OpenFill.Tests/ParallelTests.cs":  {
                                                                "name":  "ParallelTests__v0.4__2026-10-12_1924.cs",
                                                                "version":  "0.4",
                                                                "display":  "2026-10-12 19:24",
                                                                "hash":  "fc604c88b9e26b67506a0587a39fcded2b712cac176eb28a71f590e460b435e3"
                                                            },
                  "tests/OpenFill.Tests/Program.cs":  {
                                                          "name":  "Program__v0.21__2026-10-14_2259.cs",
                                                          "version":  "0.21",
                                                          "display":  "2026-10-14 22:59",
                                                          "hash":  "93d21c3a3447dbd764c979d5715e378b40199a767fd5512ffa0973fbd07c8280"
                                                      },
                  "tests/OpenFill.Tests/SharedTests.cs":  {
                                                              "name":  "SharedTests__v0.1__2026-10-05_1905.cs",
                                                              "version":  "0.1",
                                                              "display":  "2026-10-05 19:05",
                                                              "hash":  "7f3eb2ca4f1f7fbce3806ddac679fa889de411f89ce9aa217451c01b8e1e08b2"
                                                          },
                  "tests/OpenFill.Tests/StaticSite.cs":  {
                                                             "name":  "StaticSite__v0.5__2026-10-14_2235.cs",
                                                             "version":  "0.5",
                                                             "display":  "2026-10-14 22:35",
                                                             "hash":  "e88735a0bf60280de42d8de00e3569d0af965024e9796f62c954f738507392b3"
                                                         },
                  "tests/OpenFill.Tests/TabTests.cs":  {
                                                           "name":  "TabTests__v0.6__2026-10-13_1035.cs",
                                                           "version":  "0.6",
                                                           "display":  "2026-10-13 10:35",
                                                           "hash":  "aeb184170fd5710c3d13ae13f77dffa53e2fffef797366270d0f3a740a5a4f67"
                                                       },
                  "tests/OpenFill.Tests/TestHarness.cs":  {
                                                              "name":  "TestHarness__v0.2__2026-10-05_1200.cs",
                                                              "version":  "0.2",
                                                              "display":  "2026-10-05 12:00",
                                                              "hash":  "ee40a638bcac626230ef55b6198f382f2bb808941d32b8687597da6a2817e243"
                                                          },
                  "tests/OpenFill.Tests/testsite/profile.html":  {
                                                                     "name":  "profile__v0.1__2026-10-04_1911.html",
                                                                     "version":  "0.1",
                                                                     "display":  "2026-10-04 19:11",
                                                                     "hash":  "b146782be887ecb6d105929650b4c0fd127386c8799e7869b6b31e923202f14e"
                                                                 },
                  "tests/OpenFill.Tests/testsite/xing.html":  {
                                                                  "name":  "xing__v0.3__2026-10-05_2131.html",
                                                                  "version":  "0.3",
                                                                  "display":  "2026-10-05 21:31",
                                                                  "hash":  "1a78f67f76e682e8b7abc1a3114ae7c787eb464c50096a84a11ae5c4a89225f1"
                                                              },
                  "tools/backup__v0.1__2026-10-13_1955/AgentRunner.cs":  {
                                                                             "name":  "AgentRunner__v0.23__2026-10-14_2235.cs",
                                                                             "version":  "0.23",
                                                                             "display":  "2026-10-14 22:35",
                                                                             "hash":  "3bf9a58f4e9b58cdf81c1fe0f5fb4fabb970d41664e61a39d13fa5afb1c94021"
                                                                         },
                  "tools/backup__v0.1__2026-10-13_1955/McpServer.cs":  {
                                                                           "name":  "McpServer__v0.21__2026-10-14_2235.cs",
                                                                           "version":  "0.21",
                                                                           "display":  "2026-10-14 22:35",
                                                                           "hash":  "a28535fcd8401be726475f92b7e31ed5c91b335c1a617c74a64b93302a844964"
                                                                       },
                  "tools/backup__v0.1__2026-10-13_1955/McpService.cs":  {
                                                                            "name":  "McpService__v0.3__2026-10-14_2235.cs",
                                                                            "version":  "0.3",
                                                                            "display":  "2026-10-14 22:35",
                                                                            "hash":  "3b833dcc39bebfe98a982644f56b0225b4eba7dac78a588dc55c2766bfb5777d"
                                                                        },
                  "tools/backup__v0.1__2026-10-13_1955/ToolRegistry.cs":  {
                                                                              "name":  "ToolRegistry__v0.18__2026-10-14_2235.cs",
                                                                              "version":  "0.18",
                                                                              "display":  "2026-10-14 22:35",
                                                                              "hash":  "08cc808da4fee6701e7394b74fb34d9535f2ce7959004f2c50977941b62920da"
                                                                          },
                  "tools/backup__v1.47__2026-10-13_2136/McpServer.cs":  {
                                                                            "name":  "McpServer__v0.21__2026-10-14_2235.cs",
                                                                            "version":  "0.21",
                                                                            "display":  "2026-10-14 22:35",
                                                                            "hash":  "376374972bb242000ea8b905f55e4563df948184a7b444631e77f4d542f97f5f"
                                                                        },
                  "tools/backup__v1.47__2026-10-13_2136/McpTaskManager.cs":  {
                                                                                 "name":  "McpTaskManager__v0.16__2026-10-14_2235.cs",
                                                                                 "version":  "0.16",
                                                                                 "display":  "2026-10-14 22:35",
                                                                                 "hash":  "059073eccab7f0f47a9913cf59caef5578453f81bcc6f13058b3ee38c19a9a64"
                                                                             },
                  "tools/backup__v1.47__2026-10-13_2136/McpTests.cs":  {
                                                                           "name":  "McpTests__v0.11__2026-10-14_2235.cs",
                                                                           "version":  "0.11",
                                                                           "display":  "2026-10-14 22:35",
                                                                           "hash":  "130e9ef02d98636e98df23b4cf1476a9077287358e2e05ed82f6ef82dba6e608"
                                                                       },
                  "tools/backup__v1.47__2026-10-13_2136/ToolRegistry.cs":  {
                                                                               "name":  "ToolRegistry__v0.18__2026-10-14_2235.cs",
                                                                               "version":  "0.18",
                                                                               "display":  "2026-10-14 22:35",
                                                                               "hash":  "03caab21012d89d5124d1b18841377ccaa15b69aad57542fdcffdb162edb362c"
                                                                           },
                  "tools/backup__v1.48__2026-10-13_2216/Directory.Build.props":  {
                                                                                     "name":  "Directory.Build.props",
                                                                                     "version":  "0.2",
                                                                                     "display":  "2026-10-14 22:35",
                                                                                     "hash":  "e8ffb5541fc736f1499d07276df67ad60873edb8dc4607e11c557790b6b38f21"
                                                                                 },
                  "tools/backup__v1.48__2026-10-13_2216/McpServer.cs":  {
                                                                            "name":  "McpServer__v0.21__2026-10-14_2235.cs",
                                                                            "version":  "0.21",
                                                                            "display":  "2026-10-14 22:35",
                                                                            "hash":  "616befe85b9b8629c0d1e2e4029f44c7db7b2f3cd4e4eb8ff4c5dce4cde1a859"
                                                                        },
                  "tools/backup__v1.48__2026-10-13_2216/McpTaskManager.cs":  {
                                                                                 "name":  "McpTaskManager__v0.16__2026-10-14_2235.cs",
                                                                                 "version":  "0.16",
                                                                                 "display":  "2026-10-14 22:35",
                                                                                 "hash":  "66789fef8ad9ea5400a1c62bca09e422c64daf9d69d981ea83fabbb235188d88"
                                                                             },
                  "tools/backup__v1.48__2026-10-13_2216/McpTests.cs":  {
                                                                           "name":  "McpTests__v0.11__2026-10-14_2235.cs",
                                                                           "version":  "0.11",
                                                                           "display":  "2026-10-14 22:35",
                                                                           "hash":  "cedb5fe63032e449f4c7ee1a6ca1987067f59200d58aa06c8017c78fd06ec1d9"
                                                                       },
                  "tools/backup__v1.49__2026-10-13_2326/Directory.Build.props":  {
                                                                                     "name":  "Directory.Build.props",
                                                                                     "version":  "0.2",
                                                                                     "display":  "2026-10-14 22:35",
                                                                                     "hash":  "e8ffb5541fc736f1499d07276df67ad60873edb8dc4607e11c557790b6b38f21"
                                                                                 },
                  "tools/backup__v1.49__2026-10-13_2326/MainForm.cs":  {
                                                                           "name":  "MainForm__v0.22__2026-10-14_2235.cs",
                                                                           "version":  "0.22",
                                                                           "display":  "2026-10-14 22:35",
                                                                           "hash":  "f1edc5ef5e062e5995126e22956a68be05ade897f134a75decabf3aab75a0ce4"
                                                                       },
                  "tools/backup__v1.49__2026-10-14_002211/Directory.Build.props":  {
                                                                                       "name":  "Directory.Build.props",
                                                                                       "version":  "0.2",
                                                                                       "display":  "2026-10-14 22:35",
                                                                                       "hash":  "e8ffb5541fc736f1499d07276df67ad60873edb8dc4607e11c557790b6b38f21"
                                                                                   },
                  "tools/backup__v1.49__2026-10-14_002211/FirefoxPasswordPicker.cs":  {
                                                                                          "name":  "FirefoxPasswordPicker__v0.1__2026-10-14_2235.cs",
                                                                                          "version":  "0.1",
                                                                                          "display":  "2026-10-14 22:35",
                                                                                          "hash":  "02295162ddf38e5d456ac61a0b8283eb206056bb40141b84e6c81eea192cb187"
                                                                                      },
                  "tools/backup__v1.49__2026-10-14_002211/MainForm.cs":  {
                                                                             "name":  "MainForm__v0.22__2026-10-14_2235.cs",
                                                                             "version":  "0.22",
                                                                             "display":  "2026-10-14 22:35",
                                                                             "hash":  "f1edc5ef5e062e5995126e22956a68be05ade897f134a75decabf3aab75a0ce4"
                                                                         },
                  "tools/compilecheck/CompileCheck.csproj":  {
                                                                 "name":  "CompileCheck__v0.2__2026-10-05_1515.csproj",
                                                                 "version":  "0.2",
                                                                 "display":  "2026-10-05 15:15",
                                                                 "hash":  "08a9e05fd281658f2f56a2a92d6b33e64ee85899e44ad9a45449b1ca49730e0a"
                                                             },
                  "tools/deploy.ps1":  {
                                           "name":  "deploy__v1.48__2026-10-14_2235.ps1",
                                           "version":  "1.48",
                                           "display":  "2026-10-14 22:35",
                                           "hash":  "d14b5136f9ab54c2d7246a2e12e5cd1d0f65dc859d4ff4a01d72e78eb4a6ffed"
                                       },
                  "tools/deploy_launch.ps1":  {
                                                  "name":  "deploy_launch__v0.1__2026-10-14_2235.ps1",
                                                  "version":  "0.1",
                                                  "display":  "2026-10-14 22:35",
                                                  "hash":  "6b1dcc04ab0f5348138b0ef66cd62ecbc050b5615af6849fa29d5ed76f5c61bd"
                                              },
                  "tools/deploy_safely.ps1":  {
                                                  "name":  "deploy_safely__v0.1__2026-10-14_2235.ps1",
                                                  "version":  "0.1",
                                                  "display":  "2026-10-14 22:35",
                                                  "hash":  "42b3912a280d3b87b50fb7c3d04330a619adb9674270088eb15c0b93756c9ab9"
                                              },
                  "tools/install.ps1":  {
                                            "name":  "install__v0.12__2026-10-12_2126.ps1",
                                            "version":  "0.12",
                                            "display":  "2026-10-12 21:26",
                                            "hash":  "bf3f110cacb728107c1eb5e1a552458f60e46f6d35a0e269a7dea258d847dd20"
                                        },
                  "tools/make-icon.py":  {
                                             "name":  "make-icon__v1.0__2026-10-12_2034.py",
                                             "version":  "1.0",
                                             "display":  "2026-10-12 20:34",
                                             "hash":  "0db54ac0def3ab838fa2f375dea528df4f1cb3f57a0524250fd72667c654db4c"
                                         },
                  "tools/pin-taskbar.ps1":  {
                                                "name":  "pin-taskbar__v1.0__2026-10-12_2126.ps1",
                                                "version":  "1.0",
                                                "display":  "2026-10-12 21:26",
                                                "hash":  "f82bb32b0b80542e15c331bd8a89cff4246cb260f4864a978f7fe046e4479dc6"
                                            },
                  "tools/publish.log":  {
                                            "name":  "publish__v1.48__2026-10-14_2259.log",
                                            "version":  "1.48",
                                            "display":  "2026-10-14 22:59",
                                            "hash":  "1f93fc7526e62472e58ac382c6d597dcdca3f4399b13588fd61d2fd9fcfdfdcb"
                                        },
                  "tools/publish.ps1":  {
                                            "name":  "publish__v0.3__2026-10-05_2211.ps1",
                                            "version":  "0.3",
                                            "display":  "2026-10-05 22:11",
                                            "hash":  "7379a17060e7aae773794d8204404712c8b994079a26646bd187ec1ead184d0b"
                                        },
                  "tools/release.ps1":  {
                                            "name":  "release__v0.6__2026-10-07_1432.ps1",
                                            "version":  "0.6",
                                            "display":  "2026-10-07 14:32",
                                            "hash":  "443b8e49fedd15aa2589c3674d1500ffa19e8aaf971440781aa4d5d36697006b"
                                        },
                  "tools/run-dev.ps1":  {
                                            "name":  "run-dev__v0.2__2026-10-05_1515.ps1",
                                            "version":  "0.2",
                                            "display":  "2026-10-05 15:15",
                                            "hash":  "128c8b530134e8b1e0a00b59b03234d8e8f89e979510a4e132d7ee791d5b8e02"
                                        },
                  "tools/testlog.txt":  {
                                            "name":  "testlog__v0.3__2026-10-14_2235.txt",
                                            "version":  "0.3",
                                            "display":  "2026-10-14 22:35",
                                            "hash":  "16c5e754e2a2d82db399d60447c8c73d72b9dcb5ec55b0dc9e6f15b5c7e4528d"
                                        },
                  "tools/watchdog-install.ps1":  {
                                                     "name":  "watchdog-install__v1.0__2026-10-07_1432.ps1",
                                                     "version":  "1.0",
                                                     "display":  "2026-10-07 14:32",
                                                     "hash":  "17408506239142e941c28a4e2e8c3bd547ab682c6ee7723a5a42543868cea6e0"
                                                 },
                  "tools/watchdog-openfill.ps1":  {
                                                      "name":  "watchdog-openfill__v1.0__2026-10-07_1432.ps1",
                                                      "version":  "1.0",
                                                      "display":  "2026-10-07 14:32",
                                                      "hash":  "b44f6f85169ef38286e75211dd9984e93a2bb2bc6386f15efc78148aa7918afa"
                                                  },
                  "tools/watchdog-uninstall.ps1":  {
                                                       "name":  "watchdog-uninstall__v1.0__2026-10-07_1432.ps1",
                                                       "version":  "1.0",
                                                       "display":  "2026-10-07 14:32",
                                                       "hash":  "c9ff13b8e9f5923a6f051aec0d59cb0bb7779e253a41bff925843307897fef07"
                                                   }
              }
}
```
