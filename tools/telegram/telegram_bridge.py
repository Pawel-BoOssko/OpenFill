"""OpenFill mobile Telegram bridge 1.62.1. No inbound network listeners."""
import json
import hashlib
import hmac
import os
import secrets
import sqlite3
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from datetime import datetime, timezone
from windows_dpapi import protect, unprotect

PIPE_PATH = r"\\.\pipe\OpenFillMobileCards"


def utc_now():
    return datetime.now(timezone.utc).isoformat()


class PipeClient:
    def __init__(self, path=PIPE_PATH):
        self.path = path

    def call(self, kind, **kwargs):
        request = json.dumps({"kind": kind, **kwargs}, separators=(",", ":"))
        deadline = time.monotonic() + 2.0
        while True:
            try:
                pipe = open(self.path, "r+b", buffering=0)
                break
            except OSError:
                if time.monotonic() >= deadline:
                    raise
                time.sleep(0.02)
        with pipe:
            pipe.write((request + "\n").encode("utf-8"))
            data = bytearray()
            while not data.endswith(b"\n"):
                chunk = pipe.read(1)
                if not chunk or len(data) > 1048576:
                    raise OSError("Invalid local IPC response")
                data.extend(chunk)
        return json.loads(data.decode("utf-8"))


class BotApi:
    def __init__(self, token):
        self.endpoint = "https://api.telegram.org/bot" + token + "/"

    def call(self, method, **params):
        body = json.dumps(params).encode("utf-8")
        request = urllib.request.Request(self.endpoint + method, body,
                                         headers={"Content-Type": "application/json"})
        try:
            with urllib.request.urlopen(request, timeout=45) as response:
                result = json.load(response)
        except urllib.error.HTTPError as error:
            try:
                data = json.loads(error.read().decode("utf-8"))
            except (ValueError, UnicodeError):
                data = {}
            wait = (data.get("parameters") or {}).get("retry_after")
            if error.code == 429 and wait:
                raise RateLimited(int(wait)) from error
            raise
        if not result.get("ok"):
            raise RuntimeError("Telegram API returned non-ok")
        return result["result"]


class RateLimited(Exception):
    def __init__(self, seconds):
        self.seconds = seconds


class Bridge:
    def __init__(self, db_path, ipc, bot, pairing_code):
        self.db = sqlite3.connect(db_path, isolation_level=None)
        self.db.row_factory = sqlite3.Row
        self.db.execute("PRAGMA journal_mode=WAL")
        self.db.execute("PRAGMA busy_timeout=5000")
        self.ipc, self.bot, self.pairing_code = ipc, bot, pairing_code
        self.db.executescript("""
        CREATE TABLE IF NOT EXISTS meta(key TEXT PRIMARY KEY,value TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS cards(id TEXT PRIMARY KEY,instance_id TEXT NOT NULL,
          task_key TEXT NOT NULL,kind TEXT NOT NULL,question TEXT NOT NULL,
          expires TEXT NOT NULL,status TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS events(id TEXT PRIMARY KEY,seq INTEGER NOT NULL,kind TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS outbox(card_id TEXT PRIMARY KEY,token TEXT UNIQUE NOT NULL,
          status TEXT NOT NULL,next_attempt REAL NOT NULL DEFAULT 0);
        CREATE TABLE IF NOT EXISTS messages(chat_id INTEGER NOT NULL,message_id INTEGER NOT NULL,
          card_id TEXT NOT NULL,PRIMARY KEY(chat_id,message_id));
        CREATE TABLE IF NOT EXISTS updates(id INTEGER PRIMARY KEY,received TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS actions(id TEXT PRIMARY KEY,card_id TEXT NOT NULL,
          instance_id TEXT NOT NULL,task_key TEXT NOT NULL,answer TEXT NOT NULL,
          status TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS pairing(user_id INTEGER PRIMARY KEY,
          chat_id INTEGER UNIQUE NOT NULL,paired_at TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS receipts(update_id TEXT PRIMARY KEY,
          chat_id INTEGER NOT NULL,source_message_id INTEGER NOT NULL,
          card_id TEXT,state TEXT NOT NULL,body TEXT NOT NULL,
          next_attempt REAL NOT NULL DEFAULT 0);
        CREATE TABLE IF NOT EXISTS phone_acceptance_tests(
          test_id TEXT PRIMARY KEY,
          chat_id INTEGER NOT NULL,
          bot_message_id INTEGER NOT NULL,
          expected_sha256 TEXT NOT NULL,
          state TEXT NOT NULL,
          update_id INTEGER,
          reply_message_id INTEGER,
          received_at TEXT);
        """)

    def get_meta(self, key, default=""):
        row = self.db.execute("SELECT value FROM meta WHERE key=?", (key,)).fetchone()
        return row["value"] if row else default

    def put_meta(self, key, value):
        self.db.execute("INSERT INTO meta VALUES(?,?) ON CONFLICT(key) DO UPDATE SET value=excluded.value",
                        (key, str(value)))

    def sync(self):
        health = self.ipc.call("health")
        instance = health["instanceId"]
        if self.get_meta("instance") != instance:
            self.db.execute("UPDATE cards SET status='closed' WHERE status='open'")
            self.put_meta("instance", instance)
            self.put_meta("sequence", 0)
        for card in self.ipc.call("list"):
            self.open_card(card)
        last = int(self.get_meta("sequence", "0"))
        for event in self.ipc.call("events", after=last):
            if event["kind"] == "opened":
                self.open_card(event["card"])
            elif event["kind"] == "closed":
                self.db.execute("UPDATE cards SET status='closed' WHERE id=?",
                                (event["card"]["cardId"],))
            self.db.execute("INSERT OR IGNORE INTO events VALUES(?,?,?)",
                            (event["eventId"], event["sequence"], event["kind"]))
            self.put_meta("sequence", max(int(self.get_meta("sequence", "0")), event["sequence"]))

        self.disable_closed_buttons()

    def open_card(self, card):
        card_id = card["cardId"]
        self.db.execute("""INSERT OR IGNORE INTO cards VALUES(?,?,?,?,?,?,?)""",
                        (card_id, card["instanceId"], card["taskKey"], card["kind"],
                         protect(card["question"]), card["expiresAtUtc"], "open"))
        self.db.execute("INSERT OR IGNORE INTO outbox VALUES(?,?,?,0)",
                        (card_id, secrets.token_urlsafe(18), "queued"))

    def disable_closed_buttons(self):
        rows = self.db.execute("""
            SELECT c.id,m.chat_id,m.message_id
            FROM cards c JOIN outbox o ON o.card_id=c.id
            JOIN messages m ON m.card_id=c.id
            WHERE c.status='closed' AND c.kind='confirm' AND o.status='sent'
        """).fetchall()
        for item in rows:
            try:
                self.bot.call("editMessageReplyMarkup", chat_id=item["chat_id"],
                              message_id=item["message_id"],
                              reply_markup={"inline_keyboard": []})
            except Exception:
                continue
            self.db.execute("UPDATE outbox SET status='closed' WHERE card_id=?", (item["id"],))

    def deliver(self):
        paired = self.db.execute("SELECT chat_id FROM pairing LIMIT 1").fetchone()
        if not paired:
            return
        for item in self.db.execute("""SELECT c.*,o.token FROM cards c JOIN outbox o ON c.id=o.card_id
                                     WHERE c.status='open' AND o.status='queued'
                                     AND o.next_attempt<=?""", (time.time(),)).fetchall():
            if datetime.fromisoformat(item["expires"].replace("Z", "+00:00")) <= datetime.now(timezone.utc):
                self.db.execute("UPDATE cards SET status='closed' WHERE id=?", (item["id"],))
                continue
            buttons = {"inline_keyboard": [[
                {"text": "Approve", "callback_data": item["token"] + ":y"},
                {"text": "Decline", "callback_data": item["token"] + ":n"}
            ]]} if item["kind"] == "confirm" else None
            text = ("OpenFill " + item["kind"] + "\nCard: " + item["id"] +
                    "\nTask: " + item["task_key"] + "\n" + unprotect(item["question"]))
            try:
                kw = {"chat_id": paired["chat_id"], "text": text}
                if buttons:
                    kw["reply_markup"] = buttons
                response = self.bot.call("sendMessage", **kw)
            except RateLimited as error:
                self.db.execute("UPDATE outbox SET next_attempt=? WHERE card_id=?",
                                (time.time() + error.seconds, item["id"]))
                continue
            except Exception:
                # Telegram sendMessage has no caller-controlled idempotency key.
                # Unknown delivery outcomes require reconciliation, not blind resend.
                self.db.execute("UPDATE outbox SET status='uncertain' WHERE card_id=?", (item["id"],))
                continue
            self.db.execute("INSERT OR IGNORE INTO messages VALUES(?,?,?)",
                            (paired["chat_id"], response["message_id"], item["id"]))
            self.db.execute("UPDATE outbox SET status='sent' WHERE card_id=?", (item["id"],))

    def ingest(self, update):
        update_id = int(update["update_id"])
        if self.db.execute("SELECT 1 FROM updates WHERE id=?", (update_id,)).fetchone():
            return
        msg = update.get("message") or {}
        callback = update.get("callback_query") or {}
        owner = callback.get("from") or msg.get("from") or {}
        chat = (callback.get("message") or msg).get("chat") or {}
        user_id, chat_id = owner.get("id"), chat.get("id")
        if chat.get("type") != "private" or not user_id or not chat_id:
            self.db.execute("INSERT OR IGNORE INTO updates VALUES(?,?)", (update_id, utc_now()))
            return
        text = msg.get("text") or ""
        if text.startswith("/pair "):
            code = text[6:].strip()
            if self.pairing_code and secrets.compare_digest(code, self.pairing_code):
                if not self.db.execute("SELECT 1 FROM pairing LIMIT 1").fetchone():
                    self.db.execute("INSERT OR IGNORE INTO pairing VALUES(?,?,?)",
                                    (user_id, chat_id, utc_now()))
        paired = self.db.execute("SELECT 1 FROM pairing WHERE user_id=? AND chat_id=?",
                                 (user_id, chat_id)).fetchone()
        if paired:
            # A mobile acceptance reply is not an OpenFill task response.
            # Preserve only the matching update's metadata, not the secret code.
            if not callback:
                reply_target = (msg.get("reply_to_message") or {}).get("message_id")
                if reply_target is not None and text:
                    candidates = self.db.execute("""
                        SELECT test_id,expected_sha256
                        FROM phone_acceptance_tests
                        WHERE chat_id=? AND bot_message_id=? AND state='pending'
                    """, (chat_id, reply_target)).fetchall()
                    digest = hashlib.sha256(text.encode("utf-8")).hexdigest()
                    for candidate in candidates:
                        if hmac.compare_digest(digest, candidate["expected_sha256"]):
                            self.db.execute("""
                                UPDATE phone_acceptance_tests
                                SET state='response_received',update_id=?,
                                    reply_message_id=?,received_at=?
                                WHERE test_id=? AND state='pending'
                            """, (update_id, msg.get("message_id"), utc_now(), candidate["test_id"]))
            card_id, answer = None, None
            action_queued = False
            if callback:
                token, sep, choice = (callback.get("data") or "").rpartition(":")
                if sep and choice in ("y", "n"):
                    row = self.db.execute("""SELECT card_id FROM outbox
                                          WHERE token=?""", (token,)).fetchone()
                    if row:
                        card_id, answer = row["card_id"], ("yes" if choice == "y" else "no")
            else:
                reply_id = (msg.get("reply_to_message") or {}).get("message_id")
                if reply_id is not None:
                    row = self.db.execute("SELECT card_id FROM messages WHERE chat_id=? AND message_id=?",
                                          (chat_id, reply_id)).fetchone()
                    if row:
                        card_id, answer = row["card_id"], text
                elif text and not text.startswith("/"):
                    # Plain messages are unambiguous only with one active delivered card.
                    # Never implicitly confirm an irreversible action.
                    candidates = self.db.execute("""
                        SELECT c.id, c.kind, c.expires
                        FROM cards c JOIN outbox o ON o.card_id=c.id
                        JOIN messages m ON m.card_id=c.id
                        WHERE c.status='open' AND o.status='sent' AND m.chat_id=?
                    """, (chat_id,)).fetchall()
                    if len(candidates) == 1 and candidates[0]["kind"] == "ask":
                        expiry = datetime.fromisoformat(candidates[0]["expires"].replace("Z", "+00:00"))
                        if expiry > datetime.now(timezone.utc):
                            card_id, answer = candidates[0]["id"], text
            if card_id is not None:
                card = self.db.execute("SELECT * FROM cards WHERE id=?", (card_id,)).fetchone()
                if card and card["status"] == "open":
                    self.db.execute("INSERT OR IGNORE INTO actions VALUES(?,?,?,?,?,?)",
                                    (str(update_id), card_id, card["instance_id"], card["task_key"],
                                     protect(answer), "pending"))
                    action_queued = True
            # A receipt is tied to the user's Telegram message, not the bot's card.
            # Do not assert successful OpenFill processing before its IPC accepts it.
            source_id = msg.get("message_id")
            if source_id is not None and text and not text.startswith("/pair "):
                if action_queued:
                    state, body = "waiting", ""
                elif text.startswith("/"):
                    state, body = "queued", "OpenFill: polecenie odebrane. Nie przekazano go do zadania."
                else:
                    state, body = ("queued", "OpenFill: wiadomo\u015b\u0107 odebrana, ale nie zosta\u0142a "
                                   "przypisana do zadania. U\u017cyj Odpowiedz (Reply) na w\u0142a\u015bciwej karcie.")
                self.db.execute("INSERT OR IGNORE INTO receipts VALUES(?,?,?,?,?,?,0)",
                                (str(update_id), chat_id, source_id,
                                 card_id if action_queued else None, state, body))
        self.db.execute("INSERT OR IGNORE INTO updates VALUES(?,?)", (update_id, utc_now()))

    def process_actions(self):
        for row in self.db.execute("SELECT * FROM actions WHERE status='pending'").fetchall():
            try:
                response = self.ipc.call("answer", action={
                    "actionId": row["id"], "instanceId": row["instance_id"],
                    "cardId": row["card_id"], "taskKey": row["task_key"],
                    "text": unprotect(row["answer"])})
            except OSError:
                continue
            with self.db:
                self.db.execute("UPDATE actions SET status=?,answer='' WHERE id=?",
                                (response["status"], row["id"]))
                if response.get("accepted") and response["status"] == "accepted":
                    receipt_text = "OpenFill: wiadomo\u015b\u0107 odebrana i przyj\u0119ta do zadania."
                else:
                    receipt_text = ("OpenFill: wiadomo\u015b\u0107 odebrana, ale zadanie nie przyj\u0119\u0142o "
                                    "odpowiedzi (status: " + str(response["status"]) + ").")
                self.db.execute("UPDATE receipts SET state='queued',body=? "
                                "WHERE update_id=? AND state='waiting'",
                                (receipt_text, row["id"]))

    def deliver_receipts(self):
        # Durable delivery queue; a restart must not produce a second acknowledgment.
        for item in self.db.execute(
                "SELECT * FROM receipts WHERE state='queued' AND next_attempt<=?",
                (time.time(),)).fetchall():
            self.db.execute("UPDATE receipts SET state='sending' WHERE update_id=?",
                            (item["update_id"],))
            try:
                self.bot.call("sendMessage", chat_id=item["chat_id"], text=item["body"],
                              reply_parameters={"message_id": item["source_message_id"],
                                                "allow_sending_without_reply": True})
            except RateLimited as error:
                self.db.execute("UPDATE receipts SET state='queued',next_attempt=? "
                                "WHERE update_id=?",
                                (time.time() + error.seconds, item["update_id"]))
            except Exception:
                # Telegram lacks an idempotency key; uncertain delivery is never blindly repeated.
                self.db.execute("UPDATE receipts SET state='uncertain' WHERE update_id=?",
                                (item["update_id"],))
            else:
                self.db.execute("UPDATE receipts SET state='sent' WHERE update_id=?",
                                (item["update_id"],))

    def poll(self):
        offset = int(self.get_meta("offset", "0"))
        updates = self.bot.call("getUpdates", offset=offset, timeout=20,
                                allowed_updates=["message", "callback_query"])
        for update in updates:
            with self.db:
                self.ingest(update)
                self.put_meta("offset", max(int(self.get_meta("offset", "0")), update["update_id"] + 1))
        self.process_actions()
        self.deliver_receipts()

    def run(self):
        while True:
            try:
                self.sync()
                self.deliver()
                self.poll()
            except RateLimited as error:
                time.sleep(max(1, error.seconds))
            except (OSError, ValueError, KeyError, urllib.error.URLError):
                time.sleep(5)


def main():
    token = os.environ["OPENFILL_TELEGRAM_BOT_TOKEN"]
    code = os.environ["OPENFILL_TELEGRAM_PAIRING_CODE"]
    path = os.environ.get("OPENFILL_TELEGRAM_STATE", os.path.join(
        os.environ.get("LOCALAPPDATA", "."), "OpenFill", "telegram_bridge.sqlite3"))
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    Bridge(path, PipeClient(), BotApi(token), code).run()


if __name__ == "__main__":
    main()
