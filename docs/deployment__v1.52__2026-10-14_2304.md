# OpenFill 1.52: deployment and verification

## Metadata
- Document version: 1.52
- Date: 2026-10-14 23:04
- Source root: C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill
- Production: C:\Users\pboru\OpenFill\app\OpenFill.exe
- Production data: C:\Users\pboru\OpenFill\data\stable
- Release stage: C:\Users\pboru\OpenFill\release__v1.52__2026-10-14_2302

## Deployment result
OpenFill 1.52 was deployed successfully in interactive Windows session 1 with the safe deployment script. MCP health check passed after restart.

Production verification:
- version: 1.52
- version date: 2026-10-14 22:59
- build: 2026-10-14 23:02
- MCP: healthy
- resource telemetry: enabled
- telemetry interval: 30 seconds
- retention: 7 days
- maximum telemetry history: 100 MB
- telemetry folder: C:\Users\pboru\OpenFill\data\stable\logs\telemetry
- current telemetry file: C:\Users\pboru\OpenFill\data\stable\logs\telemetry\resources_20261014.ndjson

The deployment waited for an unrelated LinkedIn task to finish. No running task was cancelled.

## What 1.51 introduced and 1.52 keeps
1. Firefox credential picker supports checking multiple accounts and importing them in one batch.
2. Checked accounts stay selected while the person changes the search/domain filter.
3. One confirmation shows only website/origin and login names, never password values.
4. Luna has list_imported_logins and can enumerate imported account names for a service without reading passwords.
5. use_imported_password accepts login and optional explicit origin.
6. Exact-origin remains the default boundary for using a credential. Related origins may be chosen explicitly from list_imported_logins.
7. An imported credential now fills the matching login/email field when present and the password field. Password-only second-stage forms still work.
8. Local end-to-end credential test site verifies multiple accounts, selection, login+password fill, submit and server-side authentication.

## Credential acceptance tests
Local end-to-end tests passed.

Real SEOHost acceptance test before 1.51:
- imported account p.osko@hotmail.com successfully authenticated to https://panel.seohost.pl/

Real CyberFolks diagnosis:
- imported login 04229383 was confirmed present in Windows Credential Manager under exact origin https://panel.cyberfolks.pl
- it does not appear in the normal WebView2/browser password suggestion list because OpenFill imports into its own Windows Credential Manager vault
- OpenFill also had tosiapl for the same origin
- both 04229383 and tosiapl were filled by OpenFill, but CyberFolks rejected them as invalid credentials
- this indicates stale or mismatched credential values rather than a failure to save or inject the credential

Production 1.52 diagnostic list_imported_logins result:
- https://panel.seohost.pl: p.osko@hotmail.com
- https://now.cyberfolks.pl: pboosko@outlook.com
- https://panel.cyberfolks.pl: 04229383
- https://panel.cyberfolks.pl: tosiapl
No password values were exposed.

## Site memory changes in 1.52
1. Site memory is injected automatically at task start when a current host exists.
2. navigate returns saved operational memory for the destination before the live page representation.
3. read_site_note returns exact-host memory plus up to three recent sibling-host notes from the same site family.
4. write_site_note merges facts instead of replacing the whole Knowledge field.
5. Exact duplicate facts are ignored.
6. Stable labelled facts, for example login_url: or required_parameter:, replace an older fact with the same label, which allows corrections without accumulating contradictory copies.
7. finish.site_knowledge is merged by RecordOutcome instead of overwriting old knowledge.
8. Notes are atomically written and protected against concurrent writes.
9. Secret patterns are scrubbed before storage.
10. Model instructions explicitly forbid saving personal data, passwords, tokens, OTP values and one-off form values in site notes.

## Resource telemetry in 1.52
Telemetry was added before any performance tuning.

It samples the real descendant process tree of the current OpenFill.exe, not every WebView2 process in Windows.

Per-process metrics:
- PID and parent PID
- process name and start time
- Working Set
- Private Bytes
- handles
- threads
- CPU

Aggregate metrics:
- process count
- WebView2 process count
- Working Set sum
- Private Bytes sum
- handles and threads
- .NET managed memory, GC heap and committed memory
- active tasks, waiting tasks and tab count
- application uptime
- CPU in two documented scales

CPU definitions:
- cpuPctMachine: percentage of total logical CPU capacity, 0 to 100
- cpuPctCoreSum: one-core scale, where one fully busy logical CPU is 100

The sum of Working Set across processes is a process-sum metric and can count shared physical pages more than once. Private Bytes is logged alongside it for comparison.

First production samples:
- at about 3 seconds uptime, 13 processes, 12 WebView2, 579.246 MB summed Working Set, 261.762 MB Private Bytes
- at about 34 seconds uptime during an active task, 16 processes, 15 WebView2, 1523.844 MB summed Working Set, 954.008 MB Private Bytes, 17.793% total-machine CPU
- the largest WebView2 process in that second sample used about 378.719 MB Private Bytes

These short-run measurements do not resolve the reported long-run condition of about 5 GB RAM and 20% CPU. The logger is intentionally left running so the long-term trend can be analysed later.

## Tests
After the 1.52 release rename and rebuild:
- full suite: 581 passed, 0 failed
- explicit solution build: successful
- one existing WindowsBase/WebView2 assembly-version warning remains; no build errors

New tests cover:
- site note first visit
- duplicate suppression
- addition of new facts
- same-label correction
- sibling-host context
- stale/failure and later success
- secret scrubbing
- parallel writes
- persistence after restart
- resource telemetry output and CPU scale documentation
- telemetry configuration persistence
- automatic site-memory injection on navigate
- multi-account credential list/fill/login end-to-end

## Backups
Pre-credential-change source backup:
C:\Users\pboru\OpenFill\backups\source_pre_multicred__2026-10-14_2221

Pre-site-memory/telemetry source backup:
C:\Users\pboru\OpenFill\backups\source_pre_notes_telemetry__2026-10-14_2249

## Next performance step
Do not tune GC, tab limits, WebView2 process policy or memory limits blindly. Collect telemetry over idle and active periods for several hours, then analyse:
- Working Set and Private Bytes slopes
- process count growth
- WebView2 lifetime and accumulation
- per-task/tab increments
- idle CPU floor
- handle/thread growth
- whether memory returns after task/tab completion

## Early follow-up sample, about 6 minutes uptime
The telemetry file had 13 samples. The newest sample was during one active task with 3 tabs: 20 total processes, 19 WebView2 processes, 2402.211 MB summed Working Set, 1658.418 MB Private Bytes and 12.513% total-machine CPU. The heaviest current WebView2 renderer was above 500 MB Private Bytes.

An earlier sample with activeTasks=0 and 2 retained tabs still had 18 WebView2 processes and about 1026 MB Private Bytes. This makes retained tab/renderer cost an important hypothesis to test over longer uptime, but it is not yet a diagnosis. The analysis script suppresses MB/hour slopes until at least 30 minutes of telemetry exist.