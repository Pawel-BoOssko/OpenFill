# OF-182: version provenance audit, 2026-10-17 19:19 CEST
Directory.Build.props OpenFillVersion=1.54; OpenFillVersionDate=2026-10-15T23:45. This is SOURCE metadata, not deployed proof.
README v0.18 dated 2026-10-17 11:30 avoids hardcoding application version; its text was not changed.
INDEX v1.52 dated 2026-10-14 22:59 identifies the historical 1.52 package, not the current source tree. Do not relabel it as 1.54.
LIVE-VERIFIED: PBOX1 OpenFill PID 17808, SessionId 1, reported installed version 1.52.
TEST: read-only metadata and process inspection. No new build, stage or deployment.
NEXT: generate a new version-specific INDEX and changelog only for a genuinely built and staged release; retain historical INDEX as-is.
