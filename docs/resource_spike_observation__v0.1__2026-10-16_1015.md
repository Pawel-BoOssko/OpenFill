# OpenFill production resource spike observation v0.1
Date: 2026-10-16 10:15 CEST. READ ONLY. Production unchanged.
Baseline at 09:13-09:58 CEST: 6 tabs, 19 WebView2, no active tasks, about 1450-1470 MiB summed Private Bytes, CPU typically 2-3% of machine.
At 10:04 CEST: 2 active tasks, 23 WebView2, summed Private Bytes 2377 MiB, Working Set 2449 MiB, CPU 16.772%.
At 10:13 CEST: zero active tasks, 23 WebView2, Private Bytes 2102 MiB, Working Set 2221 MiB, CPU 3.866%.
One-hour 119 samples 09:13:46-10:13:04 CEST: average CPU 3.899%, max 20.010%, average summed WS 1059.8 MiB, average summed PB 1605.1 MiB. 6 tabs throughout; WebView2 count 19-24; active tasks 0-2.
This is a workload-related resource spike, not evidence of a leak or of any deployed optimization. Observe post-task recovery and process count before drawing conclusions. Do not restart or modify production.
No secrets, user profiles, raw logs, URLs or account data included.