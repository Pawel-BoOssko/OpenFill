# OF-032: reproducible OpenFill performance profiles v0.1
State: PROFILE SPECIFICATION, no synthetic workload run on production.
Measure the OpenFill root PID and only its actual WebView2 descendants; record PID/start time, task count, tab count, CPU machine %, one-core equivalent, Working Set MiB, Private Bytes MiB, handles, threads and GC heap.
Keep the existing 30-second sampling interval. Record 7-day retention and 100 MB cap as configuration checks, not assumed results.
Profiles to compare at equal conditions:
1. Clean start, 0 tasks, 1 tab, 30 minutes idle.
2. 3 tabs, 0 tasks, 30 minutes idle.
3. 5 tabs, 0 tasks, 60 minutes idle.
4. 1 active task on a light page; record start, completion and 30-minute recovery.
5. 3 active tasks on different domains, mixed light/heavy pages, record CPU/RAM peak and recovery.
6. Login and popup, record transient renderer count and ownership.
7. 1/3/5 tabs after repeated completed tasks, 60 and 180 minutes idle.
8. Overnight session: 3+ hours without restart, then new task on a retained tab.
Do not compare mixed active/idle windows as if identical. Exclude gaps over 45 seconds and segments crossing root PID changes; report each separately.
Acceptance requires no loss of tabs, task state or login and no increase in cost caused by unwanted reloads. Measure after a genuinely deployed new version; do not attribute source-only changes to production.
