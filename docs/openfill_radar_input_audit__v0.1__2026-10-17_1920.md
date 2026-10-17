# OF-160 / OF-157: Radar to OpenFill inputs, 2026-10-17
Read-only inspection of Networking-Radar on PBOX1.
config/registration.example.json fields: full_name, email, phone, organization, city. No project/account/price-limit fields in this example.
config/rules.yaml: max_price_pln=150, allow_unknown_price=true, min_rank_for_exceptional_unknown_price=85.
config/scoring.yaml: price_weight=0.45, unknown_price_score=35.
Code search found idempotency_key in event_registration.py and openfill_evidence.py; openfill_mcp.py forwards idempotency_key in openfill_start_task. This proves presence of a parameter, not stable retry behavior.
TEST: file/field presence only. Targeted pytest was refused by tool safety checks, so zero executed tests are claimed.
NEXT: confirm actual registration payload contains intended account, project, budget and organizer confirmation; retry integration and Calendar acceptance are still open.
