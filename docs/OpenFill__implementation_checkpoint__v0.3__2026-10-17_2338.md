# OpenFill: checkpoint wykonania v0.3
Data: 2026-10-17 23:38 CEST; podstawa: master backlog v1.7, tylko A.

## Status
SOURCE: zmodyfikowane. BUILD: OpenFill.App Release OK (0 errors, 1 MSB3277 warning WindowsBase 4.0/5.0 from WebView2 dependency). TEST: 3 consecutive runs of OpenFill.Tests Release, each 771 PASS, 0 FAIL, exit 0. STAGED: not performed. DEPLOYED: not performed. LIVE-VERIFIED new source: not performed. Production remains OpenFill 1.52.0.0, MCP openfill_info answered at 23:37 with no active tasks. Do not restart live production for source experiments.

## Zmiany tej sesji
- OF-234: added simulated integration tests of 3 batches x 5 tasks in the same chat/domain, physical tab reuse, 5-busy-tab capacity and cancellable queue, protected waiting-for-user tab, loss of an unrecoverable tab and explicit reason, stored session 7 hours old reopened after manager restart. Do not describe simulated elapsed time as a real 7-hour live GUI test.
- OF-229: track whether previous physical tab really existed. If it disappeared and there is no saved URL, report previous_session_unavailable_no_saved_url rather than existing_session_reused.
- OF-228: add TabManager.IsTabPresent. MCP browser_tab.open now checks actual browser existence; requester owner included; can_reopen_saved_url distinguishes saved URL from physical tab; reuse_ready means finished state with actual tab OR restorable URL. Avoid asserting physical card is open based only on stale TabId.
- OF-233: avoid treating recipient string and service confirmation text from different lines as one verified action. Require both on the same visible page line (up to 240 characters); synthetic regression rejects cross-line proof and accepts matched local evidence. This checks service-page text, not a transactional guarantee from the service.
- OF-226: AppHost.InitialTaskDomain recognizes explicit leading Polish/English browser navigation commands, as well as old Start from this page. Does not treat arbitrary URL included as data in task body as a target. The recognized domain feeds domain locking and owner/domain tab reuse.
- Nothing changed in OF-230 releaseFinishedTabs behavior or earlier safety-denied maintenance/shutdown patches.

## Evidence
Full tests: C:\Users\pboru\Documents\OpenFill-Backups\Source-Patches\OpenFill_OF226_domain_commands__2026-10-17_2330.log, OpenFill_Release_repeat2__2026-10-17_2332.log, OpenFill_Release_repeat3__2026-10-17_2334.log. All 771/0, exit 0. Before-change source backups in the same Source-Patches folder. App Release build completed successfully with WindowsBase MSB3277 warning; warning unresolved pending investigation. Static TechnologyAtlas manifest 41/41 and source hash valid in prior checkpoint v0.2.
GitHub: Autosync log says 23:30:31 pushed openfill branch autosync/PBOX1 at dd648d843120771ba6f7322b003be3cf865914f4; remote ls-remote confirmed same. This checkpoint file was created AFTER that push and cannot yet be claimed remotely synced. No manual git push or commit-tree.

## Open items
OF-234 needs actual interactive GUI validation: real browser same-chat multi-batch, different chats, popup/user input, live waiting time, 1-3h elapsed time, renderer and memory behavior. OF-235 requires real release smoke, validated rollback and deployment. OF-230 physical tab persistence remains unresolved and historically safety-denied for the old releaseFinishedTabs change. OF-223 still lacks a verified full decrypt/restore of real production data on SATANAMD. OF-239 does not yet guarantee same-endpoint MCP long poll responder during upgrade. OF-212 prior specific shutdown persistence patch was safety-denied. No attempts to bypass those denials.

## Next safe work
1. Verify Autosync includes this checkpoint when the scheduled run occurs; do not manually push to bypass refused operations.
2. Investigate MSB3277 with read-only dependency diagnostics, change only if isolated patch/test indicates safe fix.
3. Expand owner/domain and model handoff tests, keep real production untouched until release gates met.
4. Do not mark deployment or overnight GUI observation complete based on synthetic tests.