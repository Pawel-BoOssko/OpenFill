# OF-031/033/034: production 1.52 telemetry, 2026-10-17
Window: 11:27:43-12:27:43 CEST, 120 samples, 30-second interval.
PID 17808 throughout; zero sample gaps above 45 seconds.
Idle samples 120/120; active 0; last 5 tabs and 21 WebView2 processes.
Mean CPU: 5.242% of total 4-logical-CPU machine capacity, not 5.242% of one core.
Mean summed Working Set: 1294.8 MiB; mean summed Private Bytes: 2459.5 MiB. These are different memory metrics and must not be added together.
Full-day report: resource_telemetry_analysis__v0.6__2026-10-17_1222.txt.
12h23m report: 1461 samples; peak summed Private Bytes 7019.383 MiB, Working Set 4074.707 MiB, CPU 61.254%; active sample CPU mean 23.525%, idle sample CPU mean 6.199%.
Large spikes require correlation with tasks and renderer lifetimes. No evidence of memory leak from a peak alone.
This is baseline 1.52, not a post-release performance improvement.
