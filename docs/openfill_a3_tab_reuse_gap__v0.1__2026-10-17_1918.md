# OF-225/226/228/229/230/231: source audit, 2026-10-17 19:18 CEST
Evidence: TabManager v0.6 lines 138-181, 220-288, 365-387; AppHost v0.22 line 844; MainForm v0.24 line 268.
SOURCE GAP: BeginAsync selects an earlier tab only when explicit continueKey is supplied. AppHost passes options?.ContinueFromKey. No automatic same requester/chat plus domain lookup for a new task was observed.
SOURCE GAP: MainForm constructs TabManager with releaseFinishedTabs: true. EndAsync may close a finished tab or navigate it to about:blank, discarding transient form state. This conflicts with the A3 requirement to keep a reusable page/tab.
SOURCE GAP: MakeRoomAsync returns when every tab is busy and explicitly allows exceeding Limit. OF-231 requires waiting in a queue instead.
SOURCE GAP: The inspected TabInfo has Requester but no separate chat/domain ownership fields. TaskKey alone is not a stable per-chat/domain identity.
TEST: read-only source inspection; no app build, unit test, UI smoke or production change.
NEXT: implement stable (requester, chat, domain) ownership and reuse, bounded queue at 5, explicit reason for a replacement tab, then regression and staged/live acceptance when release gates permit.
