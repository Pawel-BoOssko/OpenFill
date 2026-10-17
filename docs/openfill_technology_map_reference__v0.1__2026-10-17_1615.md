# OpenFill Technology Map: source-linked reference v0.1
Date: 2026-10-17 16:15 CEST. Scope: A9 OF-026/028/184/188.
Status: SOURCE documentation only. Not a deployed UI or verified runtime manifest.

## Single source of truth
Technology topics and interview descriptions: `src/OpenFill.App/TechnologyAtlas__v0.1__2026-10-15_2314.cs`, field `TechnologyAtlas.Topics`.
Visible banner: `TechnologyAtlas.OneLine`; dynamic build version: `BuildInfo.Version`; dynamic PID: `Environment.ProcessId`.
Searchable dialog: `src/OpenFill.App/TechnologyAtlasDialog__v0.1__2026-10-15_2315.cs`.
Main window wiring: `src/OpenFill.App/MainForm__v0.24__2026-10-14_2259.cs`.
This document links to the source rather than maintaining a second technology inventory.

## Flows
1. ChatGPT/Claude -> MCP JSON-RPC/HTTP -> McpServer -> McpTaskManager -> AgentRunner (Luna) -> BrowserController -> CDP/WebView2 -> website -> task status.
2. Firefox profile -> Firefox NSS importer -> Windows Credential Manager -> origin-aware lookup -> browser form.
3. Networking Radar -> idempotent MCP task -> OpenFill browser action -> verified external outcome -> optional calendar integration. Calendar is not proof of registration.

## Decisions and trade-offs
WinForms/WebView2: local interactive desktop session and existing browser profile; cost: Windows-only GUI and renderer memory.
DOM/CDP: structured browser state and precise actions; cost: dynamic pages and pop-ups need fallbacks.
Local agent: close to local credentials and browser session; cost: uptime depends on PBOX1.
MCP: client-independent task protocol; cost: polling, task persistence and maintenance lifecycle need explicit handling.
Credential vault: OS-managed secrets outside routine model context; no secrets in this document.
Release: SessionId 1 scheduled-task launcher; SessionId 0 remote shell is not a GUI deployment channel.

## Verified environment snapshot
PBOX1: Windows 10 Pro 10.0.19045; 4 logical CPU; 15.84 GiB RAM.
Production process observed: OpenFill.exe PID 17808 in SessionId 1, version 1.52 from previous verified handoff.
New TechnologyAtlas source is not built, staged or deployed.

## Outstanding acceptance
OF-029: generated, versioned technology manifest linked to current config.
OF-168/030: compiled tests and interactive screenshot/resize/search/copy smoke.
OF-184: documentation and UI must derive from the same current source and deployed version.

