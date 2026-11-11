"""OpenFill Telegram bridge supervisor. Starts at user logon and restarts bridge after failure."""
import json
import os
from pathlib import Path
import subprocess
import sys
import time
from datetime import datetime
from windows_dpapi import unprotect

root = Path(__file__).resolve().parent
state = Path(os.environ["LOCALAPPDATA"]) / "OpenFill"
state.mkdir(parents=True, exist_ok=True)
credential_file = state / "telegram_bridge_credentials.dpapi.json"
log_path = state / "telegram_bridge_supervisor.log"

def log(text):
    with log_path.open("a", encoding="utf-8") as log_file:
        log_file.write(datetime.now().isoformat() + " " + text + "\n")

def run():
    log("supervisor_started")
    missing_logged = False
    while True:
        try:
            if not credential_file.exists():
                if not missing_logged:
                    log("credentials_missing_waiting_for_bot_setup")
                    missing_logged = True
                time.sleep(15)
                continue
            missing_logged = False
            cfg = json.loads(credential_file.read_text(encoding="utf-8"))
            token = unprotect(cfg["token"])
            pairing = unprotect(cfg["pairing"])
            if not token or not pairing:
                raise ValueError("Empty Telegram settings")
            env = os.environ.copy()
            env["OPENFILL_TELEGRAM_BOT_TOKEN"] = token
            env["OPENFILL_TELEGRAM_PAIRING_CODE"] = pairing
            env["OPENFILL_TELEGRAM_STATE"] = str(state / "telegram_bridge.sqlite3")
            log("bridge_start")
            with (state / "telegram_bridge.log").open("a", encoding="utf-8") as stdout:
                result = subprocess.run([sys.executable, "-u", str(root / "telegram_bridge.py")], env=env,
                                        stdout=stdout, stderr=subprocess.STDOUT, cwd=str(root))
            log(f"bridge_exit={result.returncode} restart_pending")
        except Exception as e:
            log("bridge_error=" + repr(e))
        time.sleep(10)

if __name__ == "__main__":
    run()
