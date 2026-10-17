# OpenFill GUI responsive navigation checkpoint v0.1

Date: 2026-10-16, source-only change. Not deployed.

## Defect
MainForm MinimumSize=1000 px, right panel nominally 520 px.
Left browser area approximately 480 px. Fixed navigation columns total
40+40+40+78+118+140=456 px, plus horizontal padding 8 px.
URL text box was left with only approximately 16 px.

## Source change
MainForm__v0.24__2026-10-14_2259.cs:
- Keep full one-row navigation when left browser area is >=720 logical DPI-scaled px.
- Below threshold, place address field on a dedicated full-width first row;
  navigation buttons Back, Forward, Reload, Go, Credentials and Import Firefox
  remain accessible on a second row.
- Navigation height adapts from 34 to 68 logical DPI-scaled px.
- Layout responds to left pane resize and main form resize.
- No browser process arguments, tabs, credentials or production data changed.

## Validation
dotnet build OpenFill.App -c Release --no-restore -v quiet:
exit code 0, errors 0, warnings 1 (existing MSB3277 WindowsBase conflict).
No interactive GUI acceptance test yet; no claim of DPI screenshot or production acceptance.
Existing 669 tests were from an earlier run, not rerun for this change.
HTTP Basic 15 new assertions are still not invoked by Program, unrelated blocked step.

## Next
Native SessionId 1 GUI smoke after a permitted deployment:
1000 px minimum window, 125%/150% DPI, normal and compact navigation,
browser area, Credentials and Firefox Import buttons, resize and restore.
Production remains OpenFill 1.52, untouched.