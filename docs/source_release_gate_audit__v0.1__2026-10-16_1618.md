# OpenFill: source-only release gate audit v0.1, 2026-10-16_1618

Scope: static source inspection only. No build, tests, staging or deployment.

## Findings
1. BLOCKER: FindByIdempotencyKey uses diskMatch but local declaration present: False. Source compile defect until fixed.
2. BLOCKER: PrepareShutdown persistence failure has explicit maintenance gate reset: False. Recovery required.
3. BLOCKER: PrepareShutdown writes interrupted records one-by-one; a later failure may leave earlier records interrupted while tasks continue. Needs transactional rollback/reconciliation.
4. TabManager MakeRoomAsync: review close-before-remove order and failed CloseTabAsync recovery; previous audit found orphaned renderer risk.
5. FileTransfer PrepareScreenshot: audit expired ticket removal and bounded capacity; previous audit found unbounded token growth.
6. Firefox logins metadata: realm=True, created=True, used=True, changed=True. SOURCE only.
7. Credentials: exact-origin boundary must remain for secret reads. SameSiteFamily may only expand metadata and needs complete PSL for public hosting suffixes.
8. Recovery: Google Drive confirms encrypted archive 1090191400 bytes and encrypted manifest 2640047 bytes, but remote content hash and real restore remain unverified.

## Release decision
NO-GO. Existing 1.52 stays installed. New Core build was previously safety-blocked; do not retry through another route. Full real restore and GUI/MCP smoke not done.
