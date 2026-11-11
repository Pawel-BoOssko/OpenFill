from pathlib import Path
import os

root = Path(r"C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill")
target = root / "src" / "OpenFill.Core" / "Agent" / "AgentRunner__v0.29__2026-10-29_2314.cs"
prompt_file = root / "tools" / "of_imp_a20_compact_prompt_20261101.txt"
raw = target.read_bytes()
bom = b"\xef\xbb\xbf" if raw.startswith(b"\xef\xbb\xbf") else b""
source = raw[len(bom):].decode("utf-8")
newline = "\r\n" if "\r\n" in source else "\n"
start_marker = '    private const string SystemPrompt = """'
start = source.index(start_marker)
body_start = source.index(newline, start) + len(newline)
end_marker = newline + '""";'
end = source.index(end_marker, body_start)
old_prompt = source[body_start:end]
prompt = prompt_file.read_text("utf-8-sig").rstrip("\r\n")
assert '"""' not in prompt
assert len(prompt) > 1000
updated = source[:body_start] + prompt.replace("\n", newline) + source[end:]
backup = target.with_suffix(".cs.before_A20_20261101.bak")
if not backup.exists():
    backup.write_bytes(raw)
temporary = target.with_suffix(".cs.a20.tmp")
temporary.write_bytes(bom + updated.encode("utf-8"))
os.replace(temporary, target)
print("A20_PROMPT_APPLIED old_chars=%d new_chars=%d reduction_pct=%.1f" %
      (len(old_prompt), len(prompt), 100*(1-len(prompt)/len(old_prompt))))
