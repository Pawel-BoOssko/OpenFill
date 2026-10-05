# OpenFill

**Tell a chat model what to do on the web. A small, cheap model does the clicking.**

OpenFill is a Windows app that puts a real browser and an AI agent side by side. You (or another AI, such as ChatGPT) describe a job in plain language: *"sign me up for Thursday's 7:00 class on this pool's website"*, *"fill in my profile on this career site with this data"*, *"find the cheapest flight on these dates"*. OpenFill's embedded model then drives the browser until the job is done, asking you only when it truly has to.

## Metadata
- Document version: 0.5
- Date: 2026-10-05 20:40
- App version: shown at the top of the app window and the panel
- Author: OpenFill project

## Why it exists

Big chat models are good at deciding *what* to do, and expensive and slow at clicking through forms, calendars and consent banners. OpenFill splits the work:

- the **big model** (for example ChatGPT) talks to you and hands over a task over MCP,
- the **small model inside OpenFill** (default: `gpt-6-luna`) operates the browser cheaply and quickly,
- **you** stay in charge of everything irreversible: payments, final submissions, codes and passwords.

## Highlights

- **Sees structure, not pixels.** The model gets a logical picture of the page (forms, fields, buttons, dialogs, messages, network calls), not screenshots. That is faster, cheaper and more reliable. Screenshots are available when the structure is not enough.
- **Picks the right way to act.** Fills a whole form in one call, types character by character for autocomplete, or uses real mouse and keyboard events when a page ignores ordinary ones.
- **Use it from a chat.** A built-in MCP server lets ChatGPT (or any MCP client) start tasks, poll their status, answer questions and cancel.
- **You stay in control.** Questions only you can answer (codes, passwords) appear as cards in the OpenFill window and are never passed to the calling model. Payments and final submissions always ask first.
- **Hard cost limits.** Per-task notices, a question to you at a set cost, a hard cumulative limit per website and a lifetime ceiling. Only you can lift a block.
- **Loop and runaway protection.** Repeated identical actions are detected, warned about and stopped.
- **Survives restarts.** Tasks are saved; a task cut off by a restart is reported as `interrupted` and can be continued.
- **One tab per task.** Tasks keep their own tab; returning to a task reopens it where it stopped, with logins preserved.
- **Shared folder.** A folder (for example synced with Google Drive) where the calling model and OpenFill exchange files such as a CV or a photo.
- **Starts with Windows.** An autostart task keeps OpenFill running, and restarts it if it crashes.
- **Full transparency.** A live panel shows every model step, tool call, network call and error. Logs are redacted: no tokens, cookies, passwords or keys are written.

## How it works

```
 ChatGPT / any MCP client          you
          |                         |
          |  MCP over HTTPS         |  panel (live view, cards, settings)
          v                         v
   +--------------------------------------------+
   |                  OpenFill                  |
   |  task manager --> agent loop --> tools     |
   |                    (OpenAI Responses API)  |
   |                          |                 |
   |                  browser controller        |
   +--------------------------|-----------------+
                              | Chrome DevTools Protocol
                              v
                    browser (WebView2 in the window)
```

The core (`OpenFill.Core`) does not know which window it runs in. The same core and the same panel run in the Windows app (WebView2) and in a command-line mode (Chromium or Edge over CDP, panel in a browser tab).

## Quick start

1. Download or clone the repository.
2. Double-click `INSTALL__*.cmd`.

The installer does each step only when needed:

- installs the .NET SDK 10 and the Microsoft Edge WebView2 Runtime (no administrator rights for .NET),
- builds the app into `%USERPROFILE%\OpenFill\app`,
- stores your OpenAI key encrypted with Windows DPAPI if `OPENAI_API_KEY` is set (otherwise the panel asks for it on first start),
- creates Start menu and desktop shortcuts,
- registers autostart at logon (skip with `-NoAutostart`, start minimized with `-Minimized`).

Running the installer again updates the app.

## Using it

### From the panel

1. Type the task under **What should I do?** and press **Run** (or Ctrl+Enter).
2. Watch the live panel: every model step, every tool call, the important network calls, errors. Filters: Model, Tools, Browser, Network, Problems.
3. When the model asks something or needs permission, a card appears. A question card has a **Choose file** button that opens a normal file dialog.
4. At the end you get a result: Done, Partly done, Failed or Blocked, with a short summary.

If a site needs a login, log in once in the browser on the left. The profile is persistent.

### From ChatGPT or another MCP client

Turn on MCP in **Settings** (or `"mcpEnabled": true` in `config.json`) and restart. OpenFill prints the address and copies it to the clipboard. Add it in ChatGPT as a custom MCP app with no authentication. The address contains a random secret, so treat it like a password.

| Tool | Purpose |
|---|---|
| `openfill_start_task` | hand over a task in plain language |
| `openfill_task_status` | poll status, progress, steps, cost and warnings |
| `openfill_reply` | answer a question from OpenFill's model |
| `openfill_cancel_task` | cancel the running task |

Statuses: `running`, `needs_input`, `waiting_for_user`, `done`, `failed`, `blocked`, `interrupted`, `busy`. One task runs at a time. A cut-off task can be continued with `continue_task_id`.

For a **stable address**, run your own tunnel (for example Tailscale Funnel or a named Cloudflare tunnel) and put its public address in **Settings > Public address** with a fixed MCP port. Without one, OpenFill opens a Cloudflare quick tunnel whose address changes at every start.

## Safety and cost control

| Protection | Default | What it does |
|---|---|---|
| Cost notice | $0.10 per task | warning that something may be going wrong |
| Cost question | $0.15 per task, and each further multiple | asks **you** (never the calling model) whether to go on |
| Domain limit | $0.40 per site, cumulative | hard stop; the site is blocked |
| Lifetime limit | $1.00 per site, ever | unblocking resets the count but never this ceiling |
| Loop detection | 5 identical actions in the last 10 | warning, then a stop after three warnings |
| Step limit | 60 steps, extended up to 5 times while progressing | prevents endless tasks |
| Irreversible steps | always ask | payment and final submission need your approval |
| User-only questions | always you | codes and passwords are never sent to the calling model |

A blocked site can only be unblocked by you in the panel.

## Settings

All of these are in the panel under **Settings**, and stored in `config.json`. A few (marked) apply after a restart.

| Setting | Default | Meaning |
|---|---|---|
| Model | `gpt-6-luna` | OpenAI model that drives the browser |
| Reasoning effort | `low` | low, medium, high, xhigh, max (depends on the model) |
| Approval | on | ask before payment and final submission |
| Open tabs | 5 | one tab per task; the oldest finished tab closes past this |
| Cost notice / question | 0.10 / 0.15 | per task, in USD |
| Domain limit | 0.40 | per site, cumulative, in USD |
| Lifetime limit | 1.00 | per site, ever, in USD |
| Max steps | 60 | model rounds per task |
| Step extensions | 5 | extra rounds granted while a task is making progress |
| Task timeout | 30 min | whole task |
| Tool timeout | 60 s | one browser tool call (waiting for a person is not counted) |
| Your answer wait | 20 min | how long a card waits for you |
| Chat answer wait | 5 min | how long OpenFill waits for the calling model (restart) |
| Start page | `about:blank` | address opened in a new tab |
| Shared folder | default folder | where files are exchanged with the calling model |
| MCP | off | let a chat model use OpenFill (restart) |
| Public address | empty | stable HTTPS address of your own tunnel (restart) |
| MCP port | 0 | 0 = pick a free port (restart) |
| Quick tunnel | on | use a Cloudflare quick tunnel when no public address is set (restart) |

Prices for the cost estimate (USD per 1M tokens) go in `config.json` under `prices`, keyed by model id: `"prices": { "<model>": { "input": 0, "cachedInput": 0, "output": 0 } }`. Without an entry for the model in use, only tokens are shown.

## Where the data is

Everything lives under `%USERPROFILE%\OpenFill\data\<instance>\` (`stable` for the installed app, `dev` for development builds). It is deliberately outside `AppData`: packaged desktop apps can redirect the `AppData` writes of the programs they start, which would hide the data from Task Scheduler and Explorer.

| Path | Contents |
|---|---|
| `profile\` | browser profile (logged-in sessions) |
| `config.json` | settings |
| `openai.key` | your OpenAI key, encrypted with DPAPI (only your Windows account can read it) |
| `costs.json` | cost counters and blocks per site |
| `tasks\` | saved MCP tasks |
| `tabs.json` | which task has which tab |
| `logs\runs\` | full log of each task and its network traffic |
| `logs\usage_YYYYMM.ndjson` | one line per model call (numbers only: tokens, latency, tools) |
| `notes\` | the model's notes about sites (how a form works, pitfalls) |
| `downloads\` | files downloaded during tasks |

## The model's tools

| Tool | Purpose |
|---|---|
| `get_page` | logical picture of the page; `diff=true` returns only changes after an action |
| `act`, `act_many` | one action, or many in one call (a whole form in one round) |
| `navigate`, `wait` | open an address; wait for text or for the network to go quiet |
| `read_text` | visible text of the page or an element |
| `get_network`, `get_console` | the page's network calls and console messages |
| `run_js` | custom code on the page when nothing else is enough |
| `upload_file` | attach a file from disk |
| `screenshot` | image of the page, only when the structure is not enough |
| `read_site_note`, `write_site_note` | notes about sites |
| `shared_list`, `shared_read`, `shared_save` | exchange files through the shared folder |
| `ask_user`, `confirm_irreversible` | question and permission through the panel |
| `report_gap` | report a missing tool or a workaround |
| `finish` | end the task with a status and a summary |

The model has no terminal and attaches only files whose path you gave it, or that are in the shared folder.

## Development

- Requirements: Windows 10 or 11, .NET SDK 10, WebView2 Runtime.
- Run from source (instance `dev`): `tools\run-dev__*.ps1`; with `-Cli -Mock` you get the command-line mode with a scripted model and no key.
- CLI: `openfill --task "..."` runs one task; `openfill` serves the panel at the printed address.
- Tests: `dotnet run --project tests/OpenFill.Tests`. They drive a real Chromium against test pages (cookie modal, React-style controlled field, server autocomplete, custom dropdown, rich-text editor, toggle, file picker, validation, save via fetch) and run the whole panel with a scripted model, including the cost limits, loop detection and MCP flows. The test pages are in Polish on purpose, since the app must handle sites in several languages.
- Releases: `tools\release__*.ps1` versions the files that changed (name and metadata) and generates an INDEX that matches the package; `-Check` audits it.

## Limitations

- Content in cross-origin iframes (for example some payment gateways) is not yet visible in `get_page`; the model falls back to `screenshot` and real clicks.
- CAPTCHAs and SMS verification need a person; OpenFill asks through the panel and does not try to bypass them.
- One task at a time.
- Windows only for now.

## Ideas for later

- Run OpenFill on a separate always-on machine.
- A stable tunnel on your own domain.
- Several tasks in parallel.
