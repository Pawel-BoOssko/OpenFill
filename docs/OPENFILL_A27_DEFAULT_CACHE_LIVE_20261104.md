# OpenFill A27: natural cache reuse, invalidation, LIVE

Date: 2026-11-04, Warsaw 21:46 CET. Host: PBOX1.
Initial LIVE: Core SHA C7632AF5A4CE7A691C0CDD827A298DEEE71C8CFEEC6E6F3F4E41BF1DC46223B1, PID 3756.
Scope: only P1-A27, plus read-only UX browser acceptance. No other backlog items changed.

## Observed defect before source change
- Natural OpenFill task OF-20261104-2138-113a at https://example.com/
- Repeated page_capture(mode=text, selector=body), default use_best=true.
- Two distinct capture IDs, both status=saved, equal SHA256 of content, no reused_snapshot/cache_verification.
- Same task continued with use_best=false: OF-20261104-2141-c825, existing capture reused twice, reused_snapshot=true and exact_live_dom_text_sha256.
- Control OF-20261104-2141-2479 with default use_best=true returned recipe_selection=verified_live_view but a fresh capture ID, no reuse.
- Root cause in SOURCE: PageCaptureService.CaptureAsync skipped A27 cache whenever expectedViewSignature was supplied by the verified recipe selection.

## Implemented SOURCE
- src/OpenFill.Core/Agent/PageCaptureService__v0.1__2026-10-30.cs
- src/OpenFill.Core/Agent/CurrentTextCaptureCache__v0.1__2026-11-02.cs
- Backups: each SOURCE path + .before_a27_default_cache_20261104
- Default verified recipe now participates in reuse, but only on neutral pages and when current URL, current DOM text SHA256, persisted file SHA256, capture freshness, account exclusion and current DOM structure signature match.
- Existing no-recipe reuse and account-sensitive host exclusion unchanged.
- Patch script: tools/of_a27_default_cache_patch_20261104.py

## Build, smoke, deployment
- dotnet build FileFilterSmoke Release: success, 0 errors; existing warnings.
- A27_CURRENT_TEXT_CACHE_6_CHECKS_OK, exit 0.
- Existing real Chromium ask card test: OPENFILL_ASK_BROWSER_12_CHECKS_OK, exit 0. This is real DOM/browser, but not proof of natural WebView2 Ask card event.
- dotnet publish Release: exit 0, staging/of_a27_default_cache_20261104.
- DEV activation: tools/of_a27_default_cache_20261104_activate_dev.ps1, DEV_RELEASE_OK PID 2532, SHA match.
- CheckOnly RELEASE_READY_NO_IDLE_GATE.
- LIVE_RELEASE_OK, mcp=True, exit 0.
- LIVE PID 19828, EXE C:/Users/pboru/OpenFill/app/OpenFill.exe, OpenFill-open Running.
- CORE SHA256: 713BBD7655410CF0518C9BA12DEB13B86C79F1D9AD09297A5F94470991D266FC. Independent staging/LIVE hash equality verified.
- Rollback: C:/Users/pboru/OpenFill/app__rollback__before_release_20261104_214424.

## Natural production acceptance
- OF-20261104-2144-116f: default use_best=true with verified_live_view, two consecutive captures, same ID capture_20261104_204154_089_639012, both reused_snapshot=true and cache_verification=exact_live_dom_text_sha256, 50ms and 32ms. Task outcome success.
- Invalidation fixture on PBOX1: local HTTP 127.0.0.1:18791, stopped after test.
- OF-20261104-2145-c8b9: Version A, second capture reused original ID capture_20261104_204600_728_579b4e.
- Changed server document at identical URL, with same HTML structure, different h1 and paragraph.
- OF-20261104-2146-2129: after reload Version B, fresh capture ID capture_20261104_204629_907_d85a9c, no reuse field. Verified new text.
- P1-A27 natural reuse AND invalidation: ACCEPTED. No need for further A27 test campaign.

## Remaining items
- D01/D02 external sends need exact authorized sender, recipient and message batches; candidate implementation already LIVE.
- D04 external publication adapters require actual UI/media/schedule; inspect authenticated pages read-only before implementation.
- E02 production promotion/suspension needs independently verified real batches.
- LocalPlan broader auto routing requires comparable real results by task class; no effectful auto routing.
- GUI Stop and native WebView2 ask card acceptance remain natural acceptance gaps.
- E03 improvements contingent on concrete non-success or cost records; do not clip calls by arbitrary thresholds.
- Airbnb D03 cancelled. Project wake remains disabled. No GitHub autosync requested.
