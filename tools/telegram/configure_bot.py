"""Configure Telegram Bot API credentials with Windows user DPAPI, no plaintext credential file."""
import json
import getpass
import os
import secrets
from pathlib import Path
from windows_dpapi import protect
state = Path(os.environ["LOCALAPPDATA"]) / "OpenFill"
state.mkdir(parents=True, exist_ok=True)
token = getpass.getpass("Telegram bot token: ").strip()
if not token or ":" not in token:
    raise ValueError("Invalid bot token format")
code = secrets.token_urlsafe(16)
path = state / "telegram_bridge_credentials.dpapi.json"
path.write_text(json.dumps({"token": protect(token), "pairing": protect(code)}), encoding="utf-8")
print("Credential file:", path)
print("Pairing command for the user's private Telegram chat: /pair", code)
