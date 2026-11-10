import tempfile, pathlib, json
from of_goal_cost_report_20261106 import aggregate, render

def write(file,items):
    file.write_text("".join(json.dumps(x,ensure_ascii=False)+"\n" for x in items),encoding="utf-8")
with tempfile.TemporaryDirectory() as t:
    logs=pathlib.Path(t)
    events=[
      {"kind":"business_goal_start","runId":"r1","taskKey":"a","goalId":"goal-a",
       "goalType":"publication","unitName":"publication","requestedUnits":5,"goalText":"Opublikuj 5 postów"},
      {"kind":"call","runId":"r1","step":1,"inputTokens":80,"cachedTokens":60,"outputTokens":20,"costUsd":0.001},
      {"kind":"business_goal_finish","runId":"r1","taskKey":"a","goalId":"goal-a",
       "outcome":"partial","calls":1,"inputTokens":80,"cachedTokens":60,"outputTokens":20,
       "agentReportedUnits":2,"costUsd":0.001},
      {"kind":"business_goal_start","runId":"r2","taskKey":"b","goalId":"goal-a",
       "continuedFrom":"a","goalType":"publication","unitName":"publication","requestedUnits":5,"goalText":"Opublikuj 5 postów"},
      {"kind":"call","runId":"r2","step":1,"inputTokens":150,"cachedTokens":100,"outputTokens":50,"costUsd":0.003},
      {"kind":"business_goal_finish","runId":"r2","taskKey":"b","goalId":"goal-a",
       "outcome":"success","calls":1,"inputTokens":150,"cachedTokens":100,"outputTokens":50,
       "agentReportedUnits":3,"costUsd":0.003},
      {"kind":"business_goal_start","runId":"r3","taskKey":"c","goalId":"goal-b",
       "goalType":"message","unitName":"message","requestedUnits":5,"goalText":"Wyślij 5 wiadomości"},
      {"kind":"business_goal_effect","runId":"r3","taskKey":"c","goalId":"goal-b",
       "evidence":"provider_batch_ui_verified","units":3,"actionKey":"batch:abc"},
      {"kind":"business_goal_effect","runId":"r3","taskKey":"c","goalId":"goal-b",
       "evidence":"provider_batch_ui_verified","units":3,"actionKey":"batch:abc"},
      {"kind":"business_goal_effect","runId":"r3","taskKey":"c","goalId":"goal-b",
       "evidence":"recipient_verified_in_current_ui","units":1,"actionKey":"recipient:1"},
      {"kind":"business_goal_finish","runId":"r3","taskKey":"c","goalId":"goal-b",
       "outcome":"partial","calls":7,"inputTokens":1000,"cachedTokens":600,"outputTokens":200,
       "agentReportedUnits":5,"costUsd":0.01},
      {"kind":"business_goal_start","runId":"r4","taskKey":"d","goalId":"goal-c",
       "goalType":"reading","unitName":"page_or_finding","requestedUnits":1,"goalText":"Odczytaj stronę"},
      {"kind":"business_goal_finish","runId":"r4","taskKey":"d","goalId":"goal-c",
       "outcome":"success","calls":2,"inputTokens":500,"cachedTokens":100,"outputTokens":50,
       "readCompletedUnits":1,"costUsd":0.002}
    ]
    write(logs/"usage_202611.ndjson",events)
    rows=aggregate(logs)
    assert len(rows)==3,rows
    publication=next(r for r in rows if r["goal_id"]=="goal-a")
    assert publication["runs"]==2
    assert publication["total_tokens"]==300
    assert publication["supported_units"]==3
    assert publication["tokens_per_agent_reported_unit"]==100
    assert publication["tokens_per_requested_unit_nominal"]==60
    batch=next(r for r in rows if r["goal_id"]=="goal-b")
    assert batch["provider_verified_units"]==3
    assert batch["supported_units"]==3
    assert batch["tokens_per_provider_verified_unit"]==400
    assert batch["total_tokens"]==1200
    page=next(r for r in rows if r["goal_id"]=="goal-c")
    assert page["read_completed_units"]==1
    assert page["supported_units"]==1
    assert page["tokens_per_supported_unit"]==550
    result=render(rows,logs/"report.json")
    assert result["goal_count"]==3
    assert (logs/"report.csv").exists()
print("BUSINESS_GOAL_REPORT_TESTS_13_PASS")
