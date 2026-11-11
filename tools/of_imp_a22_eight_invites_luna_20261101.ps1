$ErrorActionPreference='Stop'
$secret=[IO.File]::ReadAllText('C:\Users\pboru\OpenFill\data\dev\runtime\mcp-secret.txt').Trim()
$endpoint='http://127.0.0.1:48732/mcp/'+$secret
$prompt=@'
A21 real agent-cost benchmark on a LOCAL SIMULATED professional-network site only. All eight destinations are localhost, not real LinkedIn. Send 8 personalized invitations with unique notes to these local profiles: contact-01 Alex Morgan 1, contact-02 Taylor Lee 2, contact-03 Morgan Chen 3, contact-04 Sam Wilson 4, contact-05 Jordan Fox 5, contact-06 Casey Kim 6, contact-07 Jamie Reed 7, contact-08 Riley Cruz 8. Start by navigating ONCE to http://127.0.0.1:18877/invitations.html?vanityName=contact-01 with view_mode=content; inspect the form labels and available selectors. Subsequent profile links follow exactly the same URL pattern with vanityName=contact-02 ... contact-08. The note to each person must be "OFIMP A21 local invitation N", where N is the person's sequence index 1 through 8.

Prefer ONE act_many with interleaved action=navigate to each URL, setting the note in the current form, clicking Send invitation, and verifying recipient identity BEFORE each send and exact successful outgoing invitation AFTER each send. Navigation and action steps must each have expected_host=127.0.0.1, with verify on every navigation and send. Do not use external sites, browser screenshots, real contacts or real messages. If any step is uncertain, stop, do not retry the send, and report which recipients are confirmed versus uncertain. Return the number requested, verified local sends, the number of model calls and cost from OpenFill measurements, plus any additional DOM reads.
'@
$args=@{requester='ChatGPT';chat='OpenFill krokowe wdrożenie OF-IMP';title='A21 eight-invitation simulated campaign';idempotency_key='OFIMP-A22-LUNA-8-INVITES-20261101-02';wait_seconds=15;task=$prompt}
$body=@{jsonrpc='2.0';id=88221;method='tools/call';params=@{name='openfill_start_task';arguments=$args}} | ConvertTo-Json -Compress -Depth 12
$res=Invoke-RestMethod -Uri $endpoint -Method POST -Body ([Text.Encoding]::UTF8.GetBytes($body)) -ContentType 'application/json; charset=utf-8' -TimeoutSec 30
if($res.error){throw ('MCP_ERROR_'+$res.error.code)}
$res.result.content[0].text
