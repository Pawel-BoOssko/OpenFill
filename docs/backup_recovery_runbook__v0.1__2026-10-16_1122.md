# OpenFill: backup / restore policy and verified results
Version 0.1. Created 2026-10-16. Scope: PBOX1, source-only verified, no production restart.

## Verified implementation
- Production remains untouched, this is NOT an OpenFill deployment.
- Local source and docs script: tools/backup_openfill_source__v0.2__2026-10-16_1117.ps1
- Private local destination: %USERPROFILE%\Documents\OpenFill-Backups\
- Each execution generates a separate timestamped snapshot directory and SHA-256 JSON manifest.
- Excludes compiled artifacts bin/obj, WebView2 profiles, user data, runtime, source backups, .log, .ndjson and common credential/key formats.
- 2026-10-16 11:17:54 local: snapshot OpenFill-source__v0.2__2026-10-16_111754, 152 source/config/test/docs files, all 152 copied again to independent restore-verification staging and hash-checked, zero mismatches. Temporary restore-check folder removed after successful verification; original backup remains.
- Windows scheduled task OpenFill_Source_Backup_Daily runs 03:30 local when user has an interactive logon, using -NoProfile -NonInteractive. StartWhenAvailable and IgnoreNew configured; command invoked by the user session, not by the OpenFill process.
- Existing Google Drive docs sync OpenFill_Docs_GDrive_Sync every 15 min and GitHub Draft PR #1 provide separate documentation/code copies; these are NOT backups of production browser profiles or vault.

## Recovery
- STOP: do not overwrite a current source tree or working installation from any backup while other chats may be working. Restore to a NEW isolated directory first.
- Find a timestamped private backup and read its manifest__v0.2__YYYY-MM-DD_HHMMSS.json.
- Restore into a new directory and recompute SHA-256 for every listed relative path. Fail if any hash or file size differs.
- Only after verifying the selected snapshot and source freshness, review a diff against current source, test in isolation, then plan separately any authorized integration into the source. Do not use a direct copy over production.
- The snapshot is consistent file-by-file, not necessarily a repository-wide atomic transaction if another chat edits source during backup. The script fails on a detected file change during copying. For full consistency, stop concurrent writes under an agreed maintenance window, then snapshot.

## Explicitly NOT covered yet
- The production state at C:\Users\pboru\OpenFill\data\stable (tasks, history, notes, browser profile, sessions), Windows Credential Manager secrets, DPAPI keys, external cloud account state, Firefox profiles and user downloads. Do not claim full disaster recovery.
- A truly portable encrypted recovery key and private off-device backup for these secrets require a design. DPAPI alone is machine/user-bound and cannot guarantee recovery after complete Windows loss.
- Never publish real profiles, cookies, vault, tokens, config secrets, data or raw logs to public GitHub or unencrypted Drive.
- Full cold recovery: only after a safe, authorized SessionId 1 maintenance and zero active tasks, use a consistent encrypted data snapshot and test restoration with separate test data, not the production profile.
- Retention of source snapshots: not yet implemented. Do not delete existing snapshots until independent encrypted/off-device copies and a verified rotation policy exist.

## Blockers observed in this execution
- The first v0.1 source backup execution (lc_multihost PowerShell, one independent backup job) failed with the ordinary PowerShell runtime error 'Niezgodne typy argumentow'; it was fixed in a new version (v0.2) using List.ToArray(), which completed successfully.
- A separate read-only directory enumeration of Documents\OpenFill-Backups via mcp__lc_multihost__execute_command was refused by OpenAI safety. Small standalone read. No output returned; not retried or substituted by another listing.
- A separate read-only recursive aggregate of C:\Users\pboru\OpenFill\data\stable per-subfolder sizes via the same MCP was refused by OpenAI safety. Single moderately broad read, no changes. Not retried or rephrased to obtain the same data.
- Separate production staging/release and several code edits were denied in previous runs. These are not re-attempted by the backup work.

## Next independent packages
1. Finish scheduled-task execution verification and retention planning, without deleting backups.
2. Prepare portable encrypted production data recovery design and safe quiescence gate, without reading live secrets or touching production.
3. Complete permitted source/QA work and real interactive UI acceptance when safely available.
4. Keep master plan, checkpoints and Google Drive documentation current; GitHub PR stays Draft until verified.
