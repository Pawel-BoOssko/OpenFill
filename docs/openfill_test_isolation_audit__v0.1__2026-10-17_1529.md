# OF-117: test isolation audit
Date: 2026-10-17 15:29 CEST; SOURCE inspection, no tests executed.
All eight new AppPaths("test", ...) constructions found in tests pass explicit isolated temp roots, not the stable production root.
Three shared fixed temp paths remain in Program v0.21:
line 236: openfill_test_overflow
line 476: openfill_test_logs
line 897: openfill_panel.png
Most other temp paths include Guid suffixes.
Firefox credential tests use WinCred with test-only records and cleanup in finally. Crash during the test can leave test records; this is not proof of production credential corruption.
Open actions: isolate the three fixed temp paths and verify cleanup under concurrent test runs. Earlier equivalent source edit was refused by tool safety and was not retried.
TEST status: static audit only. BUILD, executable TEST, STAGED, DEPLOYED not performed.
