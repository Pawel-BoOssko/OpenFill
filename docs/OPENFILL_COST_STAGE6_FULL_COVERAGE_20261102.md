# OpenFill: pełny odczyt usage, rozliczenie przebiegów i 8 potwierdzonych łańcuchów

Ustalenia:
- all_usage_runs: 1601
- measured_non_airbnb_runs: 1574
- excluded_airbnb_runs: 27
- measured_non_airbnb_calls: 76566
- measured_non_airbnb_cost_usd: 41.459336
- confirmed_continuation_chains: 8
- comparable_operations: 1075
- comparable_actual_calls: 56611
- comparable_baseline_calls: 3415
- extra_runs_missing_from_stage1: 49
- extra_runs_call_only: 17
- potential_excess_calls_comparable_only: 53196

## Ograniczenia
- Missing task totals are reconstructed from recorded individual call events only, not assumed zero.
- Historical non-airbnb runs missing both task and call ledger records cannot be priced.
- Only the flagged 7 ambiguities were manually reviewed; other repeated work may remain unidentified.
- Frozen reference per run is not a whole-task reference for changed-scope continuation chains; comparison omitted.
- This computes hypothetical call differences, not achieved USD savings.

## 8 łańcuchów wznowień
- H20261005-161334-8196, H20261005-162133-db94
- H20261011-153302-d9f0, H20261011-201315-8ab2
- H20261011-195049-7b26, H20261011-195844-968a
- H20261011-200222-bb22, H20261011-202826-4919
- H20261011-223411-64bf, H20261011-223539-2622
- H20261012-200258-877a, H20261012-201250-73a9, H20261012-201558-182d
- H20261019-204000-cc32, H20261019-204249-5aef
- H20261027-152721-24ff, H20261027-153013-6b7c
