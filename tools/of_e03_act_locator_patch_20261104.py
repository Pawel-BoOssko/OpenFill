from pathlib import Path
root=Path(r"C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill")
p=root/"src/OpenFill.Core/Agent/ToolRegistry__v0.22__2026-10-29_2314.cs"
s=p.read_text(encoding="utf-8-sig")
old='''                        return {ok:false,reason_code:"target_text_mismatch",action_attempted:false};'''
new='''                        return {ok:false,reason_code:"target_text_mismatch",
                            observed_text:(el.innerText||el.textContent||"").replace(/\\s+/g," ").trim().slice(0,180),
                            observed_count:1,action_attempted:false};'''
if s.count(old)!=1:raise RuntimeError("TARGET_TEXT_MISMATCH_SITE_CHANGED count="+str(s.count(old)))
s=s.replace(old,new)
old_desc='''Description = "Act on one exact current-DOM CSS selector (prefer selector supplied by view_digest). Supports click, set or focus. Must pass current expected_host and, for clicking a person/row, expected_text. Optional verify checks the result locally in the SAME call. Never retry a potentially effective action just because verification is uncertain.",'''
new_desc='''Description = "Act on one exact current-DOM CSS selector (prefer selector supplied by view_digest). Supports click, set or focus. Must pass current expected_host and, for clicking a person/row, expected_text. If target_text_mismatch, inspect bounded observed_text and choose a corrected locator instead of repeating the same mismatch. Optional verify checks the result locally in the SAME call. Never retry a potentially effective action just because verification is uncertain.",'''
if s.count(old_desc)!=1:raise RuntimeError("ACT_LOCATOR_DESCRIPTION_SITE_CHANGED count="+str(s.count(old_desc)))
s=s.replace(old_desc,new_desc)
backup=Path(str(p)+".before_e03_act_locator_mismatch_20261104")
if backup.exists(): raise RuntimeError("BACKUP_ALREADY_EXISTS")
backup.write_bytes(p.read_bytes())
p.write_text(s,encoding="utf-8")
print("E03_PATCH_OK",str(backup))
