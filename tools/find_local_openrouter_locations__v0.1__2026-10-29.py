# Read-only local credential LOCATOR. Never outputs, copies or persists secret values.
from pathlib import Path
import os,re,time
ROOTS=[Path(r"C:\Users\pboru\Documents\Projects"),Path(r"C:\Users\pboru\Downloads")]
SKIP={".git",".venv","venv","node_modules","__pycache__","bin","obj","dist","build",".next",".cache","site-packages","logs","history","backups","Backup","packages"}
EXT={".env",".json",".jsonc",".txt",".ini",".yaml",".yml",".toml",".config",".cfg",".properties",".ps1",".py",".js",".ts",".mjs",".md",".xml",".bat",".cmd",".sh",".csv",".log"}
TOKEN=re.compile(rb"sk-or-(?:v1-)?[a-zA-Z0-9_\-]{40,}")
NAME=re.compile(rb"(?i)OPENROUTER[_-]?(?:API[_-]?)?(?:KEY|TOKEN)")
folders=0; checked=0; hits=0; began=time.monotonic()
for root in ROOTS:
    if not root.exists(): continue
    for base, dirs, files in os.walk(root):
        dirs[:]=[d for d in dirs if d not in SKIP and not d.startswith(".venv") and not d.endswith("_files")]
        folders+=1
        for name in files:
            file=Path(base)/name
            ext=file.suffix.lower()
            if ext not in EXT and name not in {".env",".env.local",".env.production","credentials","secrets"}: continue
            try:
                size=file.stat().st_size
                if size<10 or size>1200000: continue
                content=file.read_bytes()
            except (OSError,PermissionError): continue
            checked+=1
            m=TOKEN.search(content)
            label=NAME.search(content)
            if m:
                hits+=1
                print("KEY_SHAPED_FOUND | "+str(file)+" | key_length="+str(len(m.group()))+" | named="+str(bool(label)),flush=True)
            elif label and any(t in file.name.lower() for t in ("env","secret","credential","config","settings","key")):
                print("KEY_NAME_ONLY | "+str(file),flush=True)
        if folders%200==0:
            print("PROGRESS directories="+str(folders)+" files="+str(checked)+" token_hits="+str(hits),flush=True)
print("FINISHED directories="+str(folders)+" files="+str(checked)+" key_shaped_hits="+str(hits)+" elapsed="+str(round(time.monotonic()-began,1)),flush=True)
