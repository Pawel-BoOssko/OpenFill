# OpenFill GUI responsive navigation checkpoint v0.3
Date: 2026-10-16 03:19 CEST. Supersedes v0.2. SOURCE ONLY, NOT DEPLOYED.

Change: MainForm__v0.24__2026-10-14_2259.cs now has responsive
two-row navigation below 720 DPI-scaled px of left-pane width,
keeping URL full-width and six navigation buttons accessible.
At wider sizes original single row is retained. Both form and
left-pane resize events update layout. TableLayoutPanel RowStyles
are initialized with a while loop to cover an initially empty collection.

Validation:
- OpenFill.App Release build after final row-style fix: exit 0,
  errors 0, one existing MSB3277 WindowsBase warning.
- Full existing OpenFill.Tests regression after the final GUI change:
  669 passed, 0 failed, process exit 0.
- Log: docs/full_test_run__v1.54-dev__2026-10-16_0318.log
  (local only, intentionally excluded from Google Drive docs sync).
- New 15 Basic Auth assertions remain NOT REGISTERED and NOT RUN.
- No real interactive WinForms screenshot or DPI smoke yet.
- Production remains OpenFill 1.52 PID 5000 SessionId 1, untouched.

Cloud: this versioned .md will be copied by existing Windows
OpenFill_Docs_GDrive_Sync on the next 15-minute schedule.
Manual execution of the sync script was blocked; not retried.
Next: permitted interactive GUI acceptance, controlled deployment
with rollback, and matched 30-60min CPU/RAM WebView2 measurement.