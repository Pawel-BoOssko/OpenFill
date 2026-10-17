# OF-225/226: finished-task ownership audit
Date: 2026-10-17 15:22 CEST
Evidence: SOURCE only; no build, test, staging or deployment.
McpTaskManager__v0.17: StartAsync line 154 scopes live-task dedup by Requester; line 173 calls FindSimilarFinished(task) without requester.
FindSimilarFinished (around line 687) scans 30 finished records and matches SameJob(task, r.TaskText) without comparing r.Requester.
DescribeEarlier (around line 675) includes previous task text and summary. StartAsync can pass this context to a new caller.
Risk: a matching finished task from another requester/chat can supply context and browser continuity to the wrong chat. This conflicts with OF-225/226 ownership requirements.
Required correction: scope finished-task lookup to requester/chat and test two chats with identical task text; verify separate context, no cross-chat summary, no shared tab.
No code changed. Attempt to modify only this lookup was blocked by OpenAI safety; no alternate route attempted.
