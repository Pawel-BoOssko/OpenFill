# OpenFill Technology Atlas: verified source coverage and gaps
Version 0.1 | 2026-10-16 16:23 CEST | STATIC SOURCE AUDIT, not GUI acceptance

## Inventory
Topic records: 41. Categories: 7.
- 01  Languages / runtime: 7 topics
- 02  Desktop / browser: 9 topics
- 03  AI / orchestration: 5 topics
- 04  Connectivity / protocols: 6 topics
- 05  Data / security: 6 topics
- 06  Operations / quality: 5 topics
- 07  Ecosystem: 3 topics

## Verified source capabilities
- Native WinForms banner factory: True.
- Searchable category tree: True.
- Copy explanation button: True.
- Minimum dialog size declared: True.

## Unfinished acceptance
1. Topic schema lacks a verified deployment/status field (used, CLI-only, test, planned).
2. OneLine is static; it does not bind banner to actual process version, build, runtime or config.
3. No production 1.52 screenshot confirms banner above address bar; implementation is source-only.
4. Interview statements about deployed releases, safe rollout and idempotency need real acceptance evidence.
5. Confirm dynamic layout at narrow width, Windows DPI 125/150/200%, clipping and focus.
6. Never expose credentials, private MCP URL, tokens or account names in the panel.
7. Validate CLI-only, planned and test-only entries against project references and runtime.

## Acceptance evidence required
Source manifest parity; component file existence; interactive SessionId1 screenshot; search/copy/resize smoke; production version readback; redaction review.
