# OF-233: confirmed invitation progress, acceptance contract
Status: ACCEPTANCE SPEC ONLY, not implemented or deployed. Part A of backlog v1.7.
A step count, a tool call, a click, or a generated message is NOT evidence that an invitation was sent.
For a batch of five invitations, confirmed_count increases only when the target service confirms the actual submission for a distinct recipient. Record each recipient once, including across retries; uncertain outcomes remain unconfirmed.
A status may report 2/5 only when exactly two distinct recipients have verified submission evidence. If evidence cannot be observed, report confirmed_count=null and ETA unknown, not 0/5 or an invented estimate.
Store confirmed timestamp and evidence source separately from last_tool_activity_at and poll_wait_max_seconds.
Acceptance cases: no confirmations => count unknown; two unique confirmations => 2/5; retry of same recipient => still 2/5; ambiguous response => no increment; restart => confirmed evidence and dedup survive.
Do not treat this document or successful unit tests of polling as proof that LinkedIn invitations are counted correctly. Production validation requires a deliberate observed workflow.
