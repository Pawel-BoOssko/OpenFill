# OpenFill: Login first, capture second, filter before Luna
Date 2026-10-30, PBOX1. SOURCE and STAGING only, NO LIVE deployment.

## User decision
Before a protected-page export: sign in, verify positive authenticated-page evidence, capture only afterward, filter before the model.
Do not treat a login screen as a successful data export.

## Changes implemented
- New PageAuthenticationGate (src/OpenFill.Core/Agent/PageAuthenticationGate__v0.1__2026-10-30.cs): explicit authentication_required / authentication_unverified / ready. For LinkedIn, actual login/checkpoint URLs and login forms are recognized, account-only pages require positive signed-in DOM marker.
- PageCaptureService: checks authentication and expected URL before writing any file or registering a successful technical recipe. Even when the requested CSS selector is missing on a login page, the result is LOGIN_FIRST rather than a false success.
- Luna tool page_capture: optional require_authenticated, authenticated_selector and allow_login_page. An authentication_required/unverified result is reported as an unsuccessful tool result, not a completed capture.
- AgentRunner instruction: use browser controls and existing credentials/autofill for sign-in, verify the page, retry capture only afterward; do not send login-page data as task output.
- AppHost.BuildPrompt: removed false statement claiming that a reopened tab is necessarily still logged in.
- Direct MCP openfill_page_snapshot: when redirected to login, reports authentication_required BEFORE generic URL_MISMATCH. It never logs in by itself; login is an explicit preceding action in the task tab.
- FailedCallGuard: a successful form action (act or act_many) clears previous duplicate failures, permitting a fresh capture after login.

## Test results
- SOURCE quick: 121 PASS, 0 FAIL. Log docs/SOURCE_AUTH_FIRST_GUARD_QUICK_20261030.log.
- SOURCE final full regression: 1457 PASS, 0 FAIL, exit 0. Log docs/SOURCE_AUTH_FIRST_FINAL_FULL_20261030.log.
- Real physical WebView2 (local isolated HTTP form and session cookie):
  pre-login direct MCP capture: status authentication_required, 0 files.
  login via HTML form, 302 to protected page and session cookie.
  post-login direct MCP capture: status saved, 4 files.
  unrelated requester/chat refused; 3/3 automatic downloads completed; permission event granted for test-task tab.
  Overall QA status PASS. Evidence docs/OPENFILL_LOGIN_FIRST_PHYSICAL_QA_20261030.json.
- Real signed-out LinkedIn website test:
  /messaging/ redirected to /login/?session_redirect=...
  status authentication_required, 0 files, NOT a successful export.
  Evidence docs/OPENFILL_AUTH_FIRST_LINKEDIN_QA_20261030.json.
- The authenticated LinkedIn /messaging/ export remains untested: QA does not have a signed-in LinkedIn session. This is not evidence of a flaw in the auth gate; no complete real-site sign-in was performed.

## Build/package
- STAGING publish succeeded, exit 0. Log docs/SOURCE_AUTH_FIRST_STAGING_PUBLISH_20261030.log.
- STAGING OpenFill.Core.dll SHA256: 5114362D3D73866A33320023E3AFAEB8E93C37A3A8180A4955230A97D76F27CE.
- STAGING OpenFill.dll SHA256: 45DB0B5A6409F197D8D5AD55B48827B89BDE1F7FDF18348AB238577FE58BC29F.
- Both match corresponding current SOURCE build output and differ from LIVE.
- LIVE OpenFill PID 21012 remained unchanged. No deployment, no production restart, no networking-radar changes.

## Outstanding criterion
Log in to real LinkedIn INSIDE a separate staging browser profile using the existing authorized account UI/credentials, handle any required verification, positively confirm an authenticated /messaging/ DOM, then capture the requested contents.
Do not reuse the active production WebView2 profile simultaneously with staging. Until then: REAL-LINKEDIN-AUTHENTICATED-E2E = PENDING.
