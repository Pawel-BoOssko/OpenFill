# OpenFill - package INDEX

## Metadata
- Document version: 1.18
- Date: 2026-10-05 21:31
- Package and app version: 1.18 (version date: 2026-10-05 21:31)
- Files: 76 (including this INDEX)
- Changed in this release: 11

Every file has a version in its name and in the Metadata section of its content. Exceptions marked (N) have a name required by tools; (B) are binary files - the version is in the name only. Audit: `pwsh tools/release*.ps1 -Check`.

| File | Version | Date | Changed | Role |
|---|---|---|---|---|
| `.gitignore` (N) | 0.1 | 2026-10-04 19:11 |  | files ignored by git |
| `Directory.Build.props` (N) | 0.2 | 2026-10-05 15:15 |  | shared build settings; app version and version date |
| `docs/panel-preview__v0.1__2026-10-04_1911.png` (B) | 0.1 | 2026-10-04 19:11 |  | panel screenshot from a test (visual check) |
| `INSTALL__v0.1__2026-10-05_1123.cmd` | 0.1 | 2026-10-05 11:23 |  | double-click: install / update on Windows |
| `OpenFill__v0.1__2026-10-04_1911.sln` | 0.1 | 2026-10-04 19:11 |  | solution file (Visual Studio) |
| `README__v0.6__2026-10-05_2131.md` | 0.6 | 2026-10-05 21:31 | yes | app description, installation, usage, data, settings, architecture, development |
| `src/OpenFill.App/DpapiSecretProtector__v0.2__2026-10-05_1515.cs` | 0.2 | 2026-10-05 15:15 |  | key encryption with DPAPI |
| `src/OpenFill.App/MainForm__v0.9__2026-10-05_1855.cs` | 0.9 | 2026-10-05 18:55 |  | app window: browser + panel |
| `src/OpenFill.App/OpenFill.App__v0.2__2026-10-05_1515.csproj` | 0.2 | 2026-10-05 15:15 |  | project file OpenFill.App |
| `src/OpenFill.App/Program__v0.3__2026-10-05_1838.cs` | 0.3 | 2026-10-05 18:38 |  | entry point: OpenFill.App |
| `src/OpenFill.App/WebView2CdpConnection__v0.3__2026-10-05_1515.cs` | 0.3 | 2026-10-05 15:15 |  | CDP through WebView2 (UI thread) |
| `src/OpenFill.App/WebViewPanelTransport__v0.2__2026-10-05_1515.cs` | 0.2 | 2026-10-05 15:15 |  | panel channel through WebView2 |
| `src/OpenFill.Cli/CdpBootstrap__v0.3__2026-10-05_1515.cs` | 0.3 | 2026-10-05 15:15 |  | creating a tab and a CDP session |
| `src/OpenFill.Cli/ChromiumLauncher__v0.3__2026-10-05_1515.cs` | 0.3 | 2026-10-05 15:15 |  | starting Chromium/Edge with CDP |
| `src/OpenFill.Cli/CliOptions__v0.4__2026-10-05_1348.cs` | 0.4 | 2026-10-05 13:48 |  | CLI parameters |
| `src/OpenFill.Cli/ConsoleInteraction__v0.3__2026-10-05_1515.cs` | 0.3 | 2026-10-05 15:15 |  | model questions in the console |
| `src/OpenFill.Cli/MockModelClient__v0.3__2026-10-05_1515.cs` | 0.3 | 2026-10-05 15:15 |  | scripted model for tests (no network) |
| `src/OpenFill.Cli/OpenFill.Cli__v0.1__2026-10-04_1911.csproj` | 0.1 | 2026-10-04 19:11 |  | project file OpenFill.Cli |
| `src/OpenFill.Cli/PanelServer__v0.2__2026-10-05_1515.cs` | 0.2 | 2026-10-05 15:15 |  | panel server for the CLI (HTTP + WebSocket) |
| `src/OpenFill.Cli/Program__v0.4__2026-10-05_1515.cs` | 0.4 | 2026-10-05 15:15 |  | entry point: OpenFill.Cli |
| `src/OpenFill.Core/Agent/AgentRunner__v0.10__2026-10-05_2040.cs` | 0.10 | 2026-10-05 20:40 |  | model loop (Responses API), limits, history compaction, images |
| `src/OpenFill.Core/Agent/CostGuard__v0.2__2026-10-05_1915.cs` | 0.2 | 2026-10-05 19:15 |  |  |
| `src/OpenFill.Core/Agent/GapRecorder__v0.4__2026-10-05_1515.cs` | 0.4 | 2026-10-05 15:15 |  | gap reports from the model |
| `src/OpenFill.Core/Agent/IUserInteraction__v0.4__2026-10-05_1648.cs` | 0.4 | 2026-10-05 16:48 |  | questions and consent (interface, unattended variant) |
| `src/OpenFill.Core/Agent/SiteNotesStore__v0.3__2026-10-05_1200.cs` | 0.3 | 2026-10-05 12:00 |  | notes about sites (local; a central database later) |
| `src/OpenFill.Core/Agent/Tool__v0.2__2026-10-05_1200.cs` | 0.2 | 2026-10-05 12:00 |  | tool and result definition |
| `src/OpenFill.Core/Agent/ToolRegistry__v0.9__2026-10-05_1905.cs` | 0.9 | 2026-10-05 19:05 |  | the model's internal tools |
| `src/OpenFill.Core/Assets/actions__v0.3__2026-10-05_1515.js` | 0.3 | 2026-10-05 15:15 |  | JS: page actions (set/type/click/select...) |
| `src/OpenFill.Core/Assets/AssetLoader__v0.3__2026-10-05_1200.cs` | 0.3 | 2026-10-05 12:00 |  | loading embedded JS/HTML resources |
| `src/OpenFill.Core/Assets/extractor__v0.3__2026-10-05_1515.js` | 0.3 | 2026-10-05 15:15 |  | JS: logical page structure (forms, fields, buttons, options, dialogs, messages) |
| `src/OpenFill.Core/Assets/panel__v0.12__2026-10-05_2053.html` | 0.12 | 2026-10-05 20:53 |  | live panel (HTML) - shared by the app and the CLI |
| `src/OpenFill.Core/Browser/BrowserController__v0.4__2026-10-05_1709.cs` | 0.4 | 2026-10-05 17:09 |  | page control through CDP: navigation, extraction, actions, real events, files, screenshots |
| `src/OpenFill.Core/Browser/ConsoleMonitor__v0.3__2026-10-05_1709.cs` | 0.3 | 2026-10-05 17:09 |  | page console and JavaScript errors |
| `src/OpenFill.Core/Browser/NetworkMonitor__v0.3__2026-10-05_1709.cs` | 0.3 | 2026-10-05 17:09 |  | network traffic framed by requestId, response bodies, panel entries |
| `src/OpenFill.Core/Browser/PageModel__v0.3__2026-10-05_1515.cs` | 0.3 | 2026-10-05 15:15 |  | text picture of the page for the model and differences after an action |
| `src/OpenFill.Core/BuildInfo__v0.4__2026-10-05_2131.cs` | 0.4 | 2026-10-05 21:31 | yes | version, version date, build time |
| `src/OpenFill.Core/Cdp/CdpSession__v0.2__2026-10-05_1515.cs` | 0.2 | 2026-10-05 15:15 |  | CDP calls on a tab (evaluate, call) |
| `src/OpenFill.Core/Cdp/ICdpConnection__v0.2__2026-10-05_1515.cs` | 0.2 | 2026-10-05 15:15 |  | CDP channel abstraction |
| `src/OpenFill.Core/Cdp/SwitchableCdpConnection__v0.1__2026-10-05_1709.cs` | 0.1 | 2026-10-05 17:09 |  |  |
| `src/OpenFill.Core/Cdp/WebSocketCdpConnection__v0.2__2026-10-05_1515.cs` | 0.2 | 2026-10-05 15:15 |  | CDP over WebSocket (CLI) |
| `src/OpenFill.Core/Config/AppConfig__v0.10__2026-10-05_2040.cs` | 0.10 | 2026-10-05 20:40 |  | settings (config.json) |
| `src/OpenFill.Core/Config/AppPaths__v0.6__2026-10-05_1905.cs` | 0.6 | 2026-10-05 19:05 |  | instance directories (stable/dev) |
| `src/OpenFill.Core/Config/SecretStore__v0.3__2026-10-05_1200.cs` | 0.3 | 2026-10-05 12:00 |  | OpenAI key (variable or encrypted file) |
| `src/OpenFill.Core/Hosting/AppHost__v0.14__2026-10-05_2131.cs` | 0.14 | 2026-10-05 21:31 | yes | shared core of the app: panel <-> session, question and consent cards, tasks |
| `src/OpenFill.Core/Hosting/HistoryStore__v0.1__2026-10-05_1428.cs` | 0.1 | 2026-10-05 14:28 |  | task history: saved list of tasks (panel and MCP) with results, and a reader for the steps of one task |
| `src/OpenFill.Core/Hosting/IPanelTransport__v0.2__2026-10-05_1200.cs` | 0.2 | 2026-10-05 12:00 |  | panel <-> core channel (WebSocket or WebView2) |
| `src/OpenFill.Core/Hosting/OpenFillSession__v0.5__2026-10-05_1905.cs` | 0.5 | 2026-10-05 19:05 |  | session: log, browser, notes, task start |
| `src/OpenFill.Core/Hosting/TabManager__v0.2__2026-10-05_1838.cs` | 0.2 | 2026-10-05 18:38 |  |  |
| `src/OpenFill.Core/Logging/EventLog__v0.4__2026-10-05_1515.cs` | 0.4 | 2026-10-05 15:15 |  | NDJSON log (global, per task, network) and the stream to the panel |
| `src/OpenFill.Core/Logging/OutputLimiter__v0.2__2026-10-05_1200.cs` | 0.2 | 2026-10-05 12:00 |  | tool output limit and overflow to a file |
| `src/OpenFill.Core/Logging/Redactor__v0.2__2026-10-05_1200.cs` | 0.2 | 2026-10-05 12:00 |  | removing secrets from logs and notes |
| `src/OpenFill.Core/Mcp/CloudflareTunnel__v0.1__2026-10-05_1348.cs` | 0.1 | 2026-10-05 13:48 |  | temporary public HTTPS address for MCP (Cloudflare quick tunnel) |
| `src/OpenFill.Core/Mcp/IRunHost__v0.7__2026-10-05_2131.cs` | 0.7 | 2026-10-05 21:31 | yes | interface between the MCP layer and the app: start/stop a task, route questions to the MCP caller |
| `src/OpenFill.Core/Mcp/McpServer__v0.8__2026-10-05_2131.cs` | 0.8 | 2026-10-05 21:31 | yes | MCP server over HTTP (hand-written JSON-RPC, strict tool schemas, secret path) |
| `src/OpenFill.Core/Mcp/McpService__v0.3__2026-10-05_1838.cs` | 0.3 | 2026-10-05 18:38 |  | starts MCP: secret address, server, tunnel, self-test |
| `src/OpenFill.Core/Mcp/McpTaskManager__v0.8__2026-10-05_2131.cs` | 0.8 | 2026-10-05 21:31 | yes | MCP tasks: one task at a time, task ids, status/reply/cancel, saved to disk |
| `src/OpenFill.Core/Model/KeyedModelClient__v0.3__2026-10-05_1200.cs` | 0.3 | 2026-10-05 12:00 |  | model client that reads the key at call time |
| `src/OpenFill.Core/Model/OpenAIClient__v0.2__2026-10-05_1200.cs` | 0.2 | 2026-10-05 12:00 |  | OpenAI Responses API client |
| `src/OpenFill.Core/OpenFill.Core__v0.2__2026-10-05_1515.csproj` | 0.2 | 2026-10-05 15:15 |  | project file OpenFill.Core |
| `tests/OpenFill.Tests/CostTests__v0.5__2026-10-05_2131.cs` | 0.5 | 2026-10-05 21:31 | yes |  |
| `tests/OpenFill.Tests/FakeModel__v0.2__2026-10-05_1200.cs` | 0.2 | 2026-10-05 12:00 |  |  |
| `tests/OpenFill.Tests/McpTests__v0.5__2026-10-05_2131.cs` | 0.5 | 2026-10-05 21:31 | yes | tests of the MCP layer (task life cycle and HTTP server) |
| `tests/OpenFill.Tests/OpenFill.Tests__v0.1__2026-10-04_1911.csproj` | 0.1 | 2026-10-04 19:11 |  | project file OpenFill.Tests |
| `tests/OpenFill.Tests/Program__v0.14__2026-10-05_2131.cs` | 0.14 | 2026-10-05 21:31 | yes | entry point: OpenFill.Tests |
| `tests/OpenFill.Tests/SharedTests__v0.1__2026-10-05_1905.cs` | 0.1 | 2026-10-05 19:05 |  |  |
| `tests/OpenFill.Tests/StaticSite__v0.4__2026-10-05_2131.cs` | 0.4 | 2026-10-05 21:31 | yes | server of test pages with a simple API |
| `tests/OpenFill.Tests/TabTests__v0.2__2026-10-05_1747.cs` | 0.2 | 2026-10-05 17:47 |  |  |
| `tests/OpenFill.Tests/TestHarness__v0.2__2026-10-05_1200.cs` | 0.2 | 2026-10-05 12:00 |  | minimal test harness |
| `tests/OpenFill.Tests/testsite/profile__v0.1__2026-10-04_1911.html` | 0.1 | 2026-10-04 19:11 |  | test page: a hard form (modal, React, autocomplete, custom list, file) |
| `tests/OpenFill.Tests/testsite/xing__v0.3__2026-10-05_2131.html` | 0.3 | 2026-10-05 21:31 | yes | test page: a simple profile form |
| `tools/compilecheck/CompileCheck__v0.2__2026-10-05_1515.csproj` | 0.2 | 2026-10-05 15:15 |  | compile check of the Windows app outside Windows |
| `tools/install__v0.9__2026-10-05_1905.ps1` | 0.9 | 2026-10-05 19:05 |  | installer: .NET 10, WebView2, build, key, shortcuts |
| `tools/publish__v0.2__2026-10-05_2058.ps1` | 0.2 | 2026-10-05 20:58 |  |  |
| `tools/release__v0.5__2026-10-05_1515.ps1` | 0.5 | 2026-10-05 15:15 |  | release: file versions, Metadata, INDEX, zip, audit |
| `tools/run-dev__v0.2__2026-10-05_1515.ps1` | 0.2 | 2026-10-05 15:15 |  | run the development version (dev instance) |
| `INDEX__v1.18__2026-10-05_2131.md` | 1.18 | 2026-10-05 21:31 | yes | index of the package files (this file) |

## State for the next release

```json
{
    "package":  "1.18",
    "stamp":  "2026-10-05_2131",
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
                  "OpenFill.sln":  {
                                       "name":  "OpenFill__v0.1__2026-10-04_1911.sln",
                                       "version":  "0.1",
                                       "display":  "2026-10-04 19:11",
                                       "hash":  "d358a7c53835bddf6af38e2c3f1f5e92bf57e88ed3516f724919c25b02e03fe7"
                                   },
                  "README.md":  {
                                    "name":  "README__v0.6__2026-10-05_2131.md",
                                    "version":  "0.6",
                                    "display":  "2026-10-05 21:31",
                                    "hash":  "a8700b8692f993a4cdb10e4886d83ff0bd790a140dce8fadebddedaa9b12ad81"
                                },
                  "src/OpenFill.App/DpapiSecretProtector.cs":  {
                                                                   "name":  "DpapiSecretProtector__v0.2__2026-10-05_1515.cs",
                                                                   "version":  "0.2",
                                                                   "display":  "2026-10-05 15:15",
                                                                   "hash":  "4120407799049c1efd240943b34737f5b36eafd397385bcff4a0cdc73f756ce8"
                                                               },
                  "src/OpenFill.App/MainForm.cs":  {
                                                       "name":  "MainForm__v0.9__2026-10-05_1855.cs",
                                                       "version":  "0.9",
                                                       "display":  "2026-10-05 18:55",
                                                       "hash":  "70d9c662547ea7aece2eabe4129708c155d2fdf31f46e2f5b6c9a2602c0d84b3"
                                                   },
                  "src/OpenFill.App/OpenFill.App.csproj":  {
                                                               "name":  "OpenFill.App__v0.2__2026-10-05_1515.csproj",
                                                               "version":  "0.2",
                                                               "display":  "2026-10-05 15:15",
                                                               "hash":  "0819ae1cd048d3d8e80a7ffde1f5b268a12ab8fdff02fde8e8dd5cecfaadb875"
                                                           },
                  "src/OpenFill.App/Program.cs":  {
                                                      "name":  "Program__v0.3__2026-10-05_1838.cs",
                                                      "version":  "0.3",
                                                      "display":  "2026-10-05 18:38",
                                                      "hash":  "1d165f335e98ddc774f1ee249fa0fd30380bd06120ca095c6b8d0f65dd7d2b3b"
                                                  },
                  "src/OpenFill.App/WebView2CdpConnection.cs":  {
                                                                    "name":  "WebView2CdpConnection__v0.3__2026-10-05_1515.cs",
                                                                    "version":  "0.3",
                                                                    "display":  "2026-10-05 15:15",
                                                                    "hash":  "a37ea63f931046a34e0e574544212344402768f61a9cfabe690b161623a090b7"
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
                                                                 "name":  "AgentRunner__v0.10__2026-10-05_2040.cs",
                                                                 "version":  "0.10",
                                                                 "display":  "2026-10-05 20:40",
                                                                 "hash":  "9582140514fa3739ffff18f2db8c9e8aeff90aafbb0480b9ace6a12fca317344"
                                                             },
                  "src/OpenFill.Core/Agent/CostGuard.cs":  {
                                                               "name":  "CostGuard__v0.2__2026-10-05_1915.cs",
                                                               "version":  "0.2",
                                                               "display":  "2026-10-05 19:15",
                                                               "hash":  "25cf13c7866a91d389893457ad75008014403e62797213d7b4580ae9549e81fa"
                                                           },
                  "src/OpenFill.Core/Agent/GapRecorder.cs":  {
                                                                 "name":  "GapRecorder__v0.4__2026-10-05_1515.cs",
                                                                 "version":  "0.4",
                                                                 "display":  "2026-10-05 15:15",
                                                                 "hash":  "79c22f73992707b1eedcaf1abbb3d79693913b2c162663df864872e5903f4ea3"
                                                             },
                  "src/OpenFill.Core/Agent/IUserInteraction.cs":  {
                                                                      "name":  "IUserInteraction__v0.4__2026-10-05_1648.cs",
                                                                      "version":  "0.4",
                                                                      "display":  "2026-10-05 16:48",
                                                                      "hash":  "91d9001c64e39030454e84d5a4649aafe519a121d47d5eeb204f2089329cb808"
                                                                  },
                  "src/OpenFill.Core/Agent/SiteNotesStore.cs":  {
                                                                    "name":  "SiteNotesStore__v0.3__2026-10-05_1200.cs",
                                                                    "version":  "0.3",
                                                                    "display":  "2026-10-05 12:00",
                                                                    "hash":  "940627acf6579c6c035ec0ffad0ff13212f20489ffdc3f9f79bbe660e1d4a82f"
                                                                },
                  "src/OpenFill.Core/Agent/Tool.cs":  {
                                                          "name":  "Tool__v0.2__2026-10-05_1200.cs",
                                                          "version":  "0.2",
                                                          "display":  "2026-10-05 12:00",
                                                          "hash":  "a6211d753bf4c270f903b923e9a3f453800f9ffbc118ae821e8a962686a7ea1e"
                                                      },
                  "src/OpenFill.Core/Agent/ToolRegistry.cs":  {
                                                                  "name":  "ToolRegistry__v0.9__2026-10-05_1905.cs",
                                                                  "version":  "0.9",
                                                                  "display":  "2026-10-05 19:05",
                                                                  "hash":  "4761a09f1f55ac99e61fa901c34c50681a00b20cb46c6b0e6c626fcd00ac235a"
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
                                                                "name":  "extractor__v0.3__2026-10-05_1515.js",
                                                                "version":  "0.3",
                                                                "display":  "2026-10-05 15:15",
                                                                "hash":  "fdf89b3b073312b2da773d00ba84a5a01c48e5935fcb358468be77ccad8436cc"
                                                            },
                  "src/OpenFill.Core/Assets/panel.html":  {
                                                              "name":  "panel__v0.12__2026-10-05_2053.html",
                                                              "version":  "0.12",
                                                              "display":  "2026-10-05 20:53",
                                                              "hash":  "72280e8bf74855c838f227832fb714dae0556ab838eb801431c4a251d2a88e53"
                                                          },
                  "src/OpenFill.Core/Browser/BrowserController.cs":  {
                                                                         "name":  "BrowserController__v0.4__2026-10-05_1709.cs",
                                                                         "version":  "0.4",
                                                                         "display":  "2026-10-05 17:09",
                                                                         "hash":  "e93525dae63edc5d0abb0b00c264402522c4e057e120083977539708c3b354c4"
                                                                     },
                  "src/OpenFill.Core/Browser/ConsoleMonitor.cs":  {
                                                                      "name":  "ConsoleMonitor__v0.3__2026-10-05_1709.cs",
                                                                      "version":  "0.3",
                                                                      "display":  "2026-10-05 17:09",
                                                                      "hash":  "c4bcda6ca765181ecdab1bd444707aee342223f6818dbf3d572d3f01c683234a"
                                                                  },
                  "src/OpenFill.Core/Browser/NetworkMonitor.cs":  {
                                                                      "name":  "NetworkMonitor__v0.3__2026-10-05_1709.cs",
                                                                      "version":  "0.3",
                                                                      "display":  "2026-10-05 17:09",
                                                                      "hash":  "90db472e3b5cece1a1eb438848d303b7e9f85922e2a3689a8cd2c7f4da095cbe"
                                                                  },
                  "src/OpenFill.Core/Browser/PageModel.cs":  {
                                                                 "name":  "PageModel__v0.3__2026-10-05_1515.cs",
                                                                 "version":  "0.3",
                                                                 "display":  "2026-10-05 15:15",
                                                                 "hash":  "2966e35ecf5b313afa14fae3abd5113b4ea3b9e3ee02fe15b0198da4d4c34394"
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
                                                                           "name":  "SwitchableCdpConnection__v0.1__2026-10-05_1709.cs",
                                                                           "version":  "0.1",
                                                                           "display":  "2026-10-05 17:09",
                                                                           "hash":  "56a7bf7fb0d8991dd57502d7ca774170fb8b316d7539128b6d8dbc9c4feea27b"
                                                                       },
                  "src/OpenFill.Core/Cdp/WebSocketCdpConnection.cs":  {
                                                                          "name":  "WebSocketCdpConnection__v0.2__2026-10-05_1515.cs",
                                                                          "version":  "0.2",
                                                                          "display":  "2026-10-05 15:15",
                                                                          "hash":  "89a0c3730674af4cfdebb248ac7baa32a39b789c3209093d0bf2f1931b2fe98d"
                                                                      },
                  "src/OpenFill.Core/Config/AppConfig.cs":  {
                                                                "name":  "AppConfig__v0.10__2026-10-05_2040.cs",
                                                                "version":  "0.10",
                                                                "display":  "2026-10-05 20:40",
                                                                "hash":  "25dcae0c583e38d96cd954aecf258a90c05fa92e52dafe1e8ae6f084df113800"
                                                            },
                  "src/OpenFill.Core/Config/AppPaths.cs":  {
                                                               "name":  "AppPaths__v0.6__2026-10-05_1905.cs",
                                                               "version":  "0.6",
                                                               "display":  "2026-10-05 19:05",
                                                               "hash":  "b8005ae0a521a59b3ef8a6c22042007b2f88f0e710721153e4090ca2d1901aab"
                                                           },
                  "src/OpenFill.Core/Config/SecretStore.cs":  {
                                                                  "name":  "SecretStore__v0.3__2026-10-05_1200.cs",
                                                                  "version":  "0.3",
                                                                  "display":  "2026-10-05 12:00",
                                                                  "hash":  "9883a7583016ae7c86feca454a49fec77571d97c6a9b05d121acf68ff41c2ef8"
                                                              },
                  "src/OpenFill.Core/Hosting/AppHost.cs":  {
                                                               "name":  "AppHost__v0.14__2026-10-05_2131.cs",
                                                               "version":  "0.14",
                                                               "display":  "2026-10-05 21:31",
                                                               "hash":  "fc9b01e4d8ca1e6d27eee75f9fb31b2c22cb7d2115a03f50b30bbc7bfcb8f2bc"
                                                           },
                  "src/OpenFill.Core/Hosting/HistoryStore.cs":  {
                                                                    "name":  "HistoryStore__v0.1__2026-10-05_1428.cs",
                                                                    "version":  "0.1",
                                                                    "display":  "2026-10-05 14:28",
                                                                    "hash":  "ce63f7ecbef6892a81fcb6a3219c4ba5ab492232710eab9d479cea747ca96a09"
                                                                },
                  "src/OpenFill.Core/Hosting/IPanelTransport.cs":  {
                                                                       "name":  "IPanelTransport__v0.2__2026-10-05_1200.cs",
                                                                       "version":  "0.2",
                                                                       "display":  "2026-10-05 12:00",
                                                                       "hash":  "5b96293f8368c2fa5205b748575c68b0cef7052c2f50cfb71bbf1d5209ee1fed"
                                                                   },
                  "src/OpenFill.Core/Hosting/OpenFillSession.cs":  {
                                                                       "name":  "OpenFillSession__v0.5__2026-10-05_1905.cs",
                                                                       "version":  "0.5",
                                                                       "display":  "2026-10-05 19:05",
                                                                       "hash":  "680f488a8e42917523c72a705f84d2b777f0e1ec4d572747117249da31c11b49"
                                                                   },
                  "src/OpenFill.Core/Hosting/TabManager.cs":  {
                                                                  "name":  "TabManager__v0.2__2026-10-05_1838.cs",
                                                                  "version":  "0.2",
                                                                  "display":  "2026-10-05 18:38",
                                                                  "hash":  "8bc9bc14a0547013219c43db78502b2b6cec39bec4898369c31fd66c030b3cbb"
                                                              },
                  "src/OpenFill.Core/Logging/EventLog.cs":  {
                                                                "name":  "EventLog__v0.4__2026-10-05_1515.cs",
                                                                "version":  "0.4",
                                                                "display":  "2026-10-05 15:15",
                                                                "hash":  "5dd143b3d722d07dcf5421cc05697b81882bbb7745a7acaccdeff4b9cad10412"
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
                  "src/OpenFill.Core/Mcp/CloudflareTunnel.cs":  {
                                                                    "name":  "CloudflareTunnel__v0.1__2026-10-05_1348.cs",
                                                                    "version":  "0.1",
                                                                    "display":  "2026-10-05 13:48",
                                                                    "hash":  "0a1ffde439d3786f12765bf5646f9eaabe44fed42f54d841fa4b69e18dc0e774"
                                                                },
                  "src/OpenFill.Core/Mcp/IRunHost.cs":  {
                                                            "name":  "IRunHost__v0.7__2026-10-05_2131.cs",
                                                            "version":  "0.7",
                                                            "display":  "2026-10-05 21:31",
                                                            "hash":  "e0e5bfe0d36bc5af409cb425e933b7108735b8d1f5d1cab25fc6bcc4393a2317"
                                                        },
                  "src/OpenFill.Core/Mcp/McpServer.cs":  {
                                                             "name":  "McpServer__v0.8__2026-10-05_2131.cs",
                                                             "version":  "0.8",
                                                             "display":  "2026-10-05 21:31",
                                                             "hash":  "073888891fe004e5826af2e796dcd97691d802eab2aeb3630d5eb3c94b6f8120"
                                                         },
                  "src/OpenFill.Core/Mcp/McpService.cs":  {
                                                              "name":  "McpService__v0.3__2026-10-05_1838.cs",
                                                              "version":  "0.3",
                                                              "display":  "2026-10-05 18:38",
                                                              "hash":  "3b833dcc39bebfe98a982644f56b0225b4eba7dac78a588dc55c2766bfb5777d"
                                                          },
                  "src/OpenFill.Core/Mcp/McpTaskManager.cs":  {
                                                                  "name":  "McpTaskManager__v0.8__2026-10-05_2131.cs",
                                                                  "version":  "0.8",
                                                                  "display":  "2026-10-05 21:31",
                                                                  "hash":  "f871053db673e6e690fd0af91cde58278af5e03f02ddb94fc9138687a1a54ef5"
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
                                                            "name":  "CostTests__v0.5__2026-10-05_2131.cs",
                                                            "version":  "0.5",
                                                            "display":  "2026-10-05 21:31",
                                                            "hash":  "4c47d81202c73055b8b80941b3306cfb67152c531ba5440b7757c226bd822873"
                                                        },
                  "tests/OpenFill.Tests/FakeModel.cs":  {
                                                            "name":  "FakeModel__v0.2__2026-10-05_1200.cs",
                                                            "version":  "0.2",
                                                            "display":  "2026-10-05 12:00",
                                                            "hash":  "a39a8c1e78007f9fb44ec7681dbda0c1c6218b6db6bda59a75bf0ad8b17786f8"
                                                        },
                  "tests/OpenFill.Tests/McpTests.cs":  {
                                                           "name":  "McpTests__v0.5__2026-10-05_2131.cs",
                                                           "version":  "0.5",
                                                           "display":  "2026-10-05 21:31",
                                                           "hash":  "28117ca0615ba8f5e22422ba3ad0bbe18e4784a84385b52d1f65afc935c1f8d0"
                                                       },
                  "tests/OpenFill.Tests/OpenFill.Tests.csproj":  {
                                                                     "name":  "OpenFill.Tests__v0.1__2026-10-04_1911.csproj",
                                                                     "version":  "0.1",
                                                                     "display":  "2026-10-04 19:11",
                                                                     "hash":  "54f204769f26d17d5eebdfdccc78e190449c771586cbb4551f5eda6ee7aa5025"
                                                                 },
                  "tests/OpenFill.Tests/Program.cs":  {
                                                          "name":  "Program__v0.14__2026-10-05_2131.cs",
                                                          "version":  "0.14",
                                                          "display":  "2026-10-05 21:31",
                                                          "hash":  "51e981bab7f039d05cf541d2e8e2f4bf0756eae1954f20c024ad998b173216fd"
                                                      },
                  "tests/OpenFill.Tests/SharedTests.cs":  {
                                                              "name":  "SharedTests__v0.1__2026-10-05_1905.cs",
                                                              "version":  "0.1",
                                                              "display":  "2026-10-05 19:05",
                                                              "hash":  "7f3eb2ca4f1f7fbce3806ddac679fa889de411f89ce9aa217451c01b8e1e08b2"
                                                          },
                  "tests/OpenFill.Tests/StaticSite.cs":  {
                                                             "name":  "StaticSite__v0.4__2026-10-05_2131.cs",
                                                             "version":  "0.4",
                                                             "display":  "2026-10-05 21:31",
                                                             "hash":  "8fd03c3d8a738699a98e9257700e1dbe52f42389a524e6602323662ae81a0da0"
                                                         },
                  "tests/OpenFill.Tests/TabTests.cs":  {
                                                           "name":  "TabTests__v0.2__2026-10-05_1747.cs",
                                                           "version":  "0.2",
                                                           "display":  "2026-10-05 17:47",
                                                           "hash":  "cf69a036755a7b304ecf6918339fd4b8ae37dbba3ec09ec7c14e1976b4d39a2c"
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
                  "tools/compilecheck/CompileCheck.csproj":  {
                                                                 "name":  "CompileCheck__v0.2__2026-10-05_1515.csproj",
                                                                 "version":  "0.2",
                                                                 "display":  "2026-10-05 15:15",
                                                                 "hash":  "08a9e05fd281658f2f56a2a92d6b33e64ee85899e44ad9a45449b1ca49730e0a"
                                                             },
                  "tools/install.ps1":  {
                                            "name":  "install__v0.9__2026-10-05_1905.ps1",
                                            "version":  "0.9",
                                            "display":  "2026-10-05 19:05",
                                            "hash":  "c4aae5d4b96f955ae1765026b7c71b55da74983b24a08c7556f13b588f453d62"
                                        },
                  "tools/publish.ps1":  {
                                            "name":  "publish__v0.2__2026-10-05_2058.ps1",
                                            "version":  "0.2",
                                            "display":  "2026-10-05 20:58",
                                            "hash":  "56427789ce686d5f5a04b0a4c01afb4dbb9c70c77d29a899d7e8813a2cd22870"
                                        },
                  "tools/release.ps1":  {
                                            "name":  "release__v0.5__2026-10-05_1515.ps1",
                                            "version":  "0.5",
                                            "display":  "2026-10-05 15:15",
                                            "hash":  "1887a26cb649c1750365ab910abfa2614c1df23daf5666721c004c04dfbc9ee7"
                                        },
                  "tools/run-dev.ps1":  {
                                            "name":  "run-dev__v0.2__2026-10-05_1515.ps1",
                                            "version":  "0.2",
                                            "display":  "2026-10-05 15:15",
                                            "hash":  "128c8b530134e8b1e0a00b59b03234d8e8f89e979510a4e132d7ee791d5b8e02"
                                        }
              }
}
```
