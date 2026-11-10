$ErrorActionPreference='Stop'
$secret=[IO.File]::ReadAllText('C:\Users\pboru\OpenFill\data\dev\runtime\mcp-secret.txt').Trim()
$endpoint='http://127.0.0.1:48732/mcp/'+$secret
$task=@'
W lokalnej aplikacji demonstracyjnej http://127.0.0.1:18877/invitations.html wyślij osiem zaproszeń do następujących osób, każdemu wpisz odrębną notatkę. Pracuj na stronach kontaktów, do których prowadzi adres http://127.0.0.1:18877/invitations.html?vanityName=contact-NN, gdzie NN to 01...08.
01 Alex Morgan 1: Pozdrawiam z OpenFill, kontakt 1.
02 Taylor Lee 2: Pozdrawiam z OpenFill, kontakt 2.
03 Morgan Chen 3: Pozdrawiam z OpenFill, kontakt 3.
04 Sam Wilson 4: Pozdrawiam z OpenFill, kontakt 4.
05 Jordan Fox 5: Pozdrawiam z OpenFill, kontakt 5.
06 Casey Kim 6: Pozdrawiam z OpenFill, kontakt 6.
07 Jamie Reed 7: Pozdrawiam z OpenFill, kontakt 7.
08 Riley Cruz 8: Pozdrawiam z OpenFill, kontakt 8.
Wykonaj całość efektywnie w narzędziu act_many, nawigując pomiędzy profilami lokalnie. Sprawdź potwierdzenia każdego wysłania i podaj rzeczywistą liczbę udanych wysyłek w podsumowaniu.
'@
$args=@{ requester='ChatGPT';chat='OpenFill finalizacja OF-IMP 20261101';title='A22 Luna 8 lokalnych zaproszeń';idempotency_key='OFIMP-A22-LUNA-E2E-20261101-02';wait_seconds=20;task=$task }
$body=@{jsonrpc='2.0';id=20261122;method='tools/call';params=@{name='openfill_start_task';arguments=$args}} | ConvertTo-Json -Depth 12 -Compress
$result=Invoke-RestMethod -Uri $endpoint -Method POST -Body ([Text.Encoding]::UTF8.GetBytes($body)) -ContentType 'application/json; charset=utf-8' -TimeoutSec 35
if($result.error){throw ('DEV_RPC_ERROR_'+$result.error.code)}
$result.result.content[0].text
