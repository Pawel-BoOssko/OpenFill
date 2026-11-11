param([ValidateSet('messenger','network','crm')][string]$Stage='messenger')
$ErrorActionPreference='Stop'
if ($env:COMPUTERNAME -ne 'PBOX1') { throw 'WRONG_HOST' }
$base='http://127.0.0.1:18877/'
$prompts=@{
messenger='STAGING local multi-stage QA part 1. Only use the locally hosted site http://127.0.0.1:18877/messenger.html. Navigate there with view_mode=structure, then call read_view with NO arguments. Report how many semantic sections and visible records it returned, whether both conversation previews and current thread messages were visible, and whether there was a full-page DOM capture. Search for contact Anna Nowak 1, open that conversation using existing UI controls (get_page/act only when necessary), insert and send EXACTLY one local test message: OFIMP local benchmark A. Check the outgoing message with a narrow verify_view or act.verify, and finish. Never contact any real external site or person.'
network='STAGING local multi-stage QA part 2. Only use local http://127.0.0.1:18877/network.html. Navigate with view_mode=structure and call read_view with NO arguments. Confirm semantic list and visible conversation are returned. Use the search UI to locate a professional profile containing Jordan Hill, inspect the profile, send EXACTLY one LOCAL test message OFIMP local benchmark B with act and selective verification. Observe no separate full get_page after the final send, then finish.'
crm='STAGING local multi-stage QA part 3. Only use http://127.0.0.1:18877/crm.html. Navigate with view_mode=structure and call read_view with NO arguments. Confirm it returns table records for the 190-record app, with incomplete/paginated scope. Find Contoso 1 using existing search UI, open the Edit form, set status=Review, save ON THIS LOCAL FIXTURE ONLY and verify the changed row using a narrow current DOM check. Finish without exporting or external actions.'
}
$id=@{messenger='OFIMP-A10-REALISTIC-MULTISTAGE-M1-20261101';network='OFIMP-A10-REALISTIC-MULTISTAGE-N2-20261101';crm='OFIMP-A10-REALISTIC-MULTISTAGE-C3-20261101'}[$Stage]
$secret=[IO.File]::ReadAllText('C:\Users\pboru\OpenFill\data\dev\runtime\mcp-secret.txt').Trim()
$endpoint='http://127.0.0.1:48732/mcp/'+$secret
$args=@{requester='ChatGPT';chat='OpenFill krokowe wdrożenie OF-IMP';title='A10 realistic multi-stage '+$Stage;idempotency_key=$id;wait_seconds=15;task=$prompts[$Stage]}
$body=@{jsonrpc='2.0';id=861101;method='tools/call';params=@{name='openfill_start_task';arguments=$args}}|ConvertTo-Json -Depth 12 -Compress
$r=Invoke-RestMethod -Uri $endpoint -Method Post -Body ([Text.Encoding]::UTF8.GetBytes($body)) -ContentType 'application/json; charset=utf-8' -TimeoutSec 30
if ($r.error){throw ('MCP_ERROR_'+$r.error.code)}
$r.result.content[0].text
