# OpenFill GUI responsive navigation checkpoint v0.2

Date: 2026-10-16, source-only. Supersedes v0.1 at 03:15.
File: src/OpenFill.App/MainForm__v0.24__2026-10-14_2259.cs.

At left-pane widths below 720 logical DPI-scaled px, the address field
occupies its own full-width row, and six existing navigation buttons
remain accessible in the second row. At wider sizes, original one-row
layout remains. Layout responds to pane and form resizing.
At 1000px minimum window width and nominal 520px right panel,
the previous address field was approximately 16px wide.

Follow-up source fix: initialize ALL required TableLayoutPanel RowStyles
with while (nav.RowStyles.Count < 2) rather than a single conditional
Add, since the collection may initially contain zero styles.

Validation after both edits:
App Release dotnet build --no-restore exit 0, 0 errors,
1 pre-existing MSB3277 WindowsBase warning.
No real GUI/DPI screenshot yet; production 1.52 unchanged.
669 earlier tests predate these edits, and the separate 15 Basic Auth
assertions are not registered in Program.

Next: real WinForms acceptance at minimum width, DPI 100/125/150,
resize/restore, buttons Credentials and Import Firefox,
then controlled Session1 deployment and 30-60min CPU/RAM comparison.