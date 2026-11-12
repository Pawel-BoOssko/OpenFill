import os
import hashlib
import sys
import tempfile
import unittest
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from datetime import datetime, timedelta, timezone
from telegram_bridge import Bridge, RateLimited


def card(cid, instance="instance1", kind="ask", task=None, seconds=3600):
    return {"instanceId": instance, "cardId": cid, "taskKey": task or ("task-" + cid),
            "kind": kind, "question": "Question " + cid, "options": [],
            "expiresAtUtc": (datetime.now(timezone.utc) + timedelta(seconds=seconds)).isoformat()}


class FakeIpc:
    def __init__(self, cards=None):
        self.cards = cards or []
        self.events = []
        self.instance = "instance1"
        self.answers = []
        self.fail = False
        self.answer_response = None

    def call(self, method, **kwargs):
        if method == "health":
            return {"instanceId": self.instance, "lastSequence": len(self.events)}
        if method == "list":
            return self.cards
        if method == "events":
            return [e for e in self.events if e["sequence"] > kwargs["after"]]
        if method == "answer":
            if self.fail:
                raise OSError("IPC interrupted")
            self.answers.append(kwargs["action"])
            return self.answer_response or {"status": "accepted", "accepted": True}
        raise AssertionError(method)


class FakeBot:
    def __init__(self):
        self.messages = []
        self.updates = []
        self.next_id = 100
        self.error = None
        self.offset = 0

    def call(self, method, **kwargs):
        if method == "sendMessage":
            if self.error:
                raise self.error
            self.next_id += 1
            self.messages.append(kwargs)
            return {"message_id": self.next_id}
        if method == "getUpdates":
            self.offset = kwargs["offset"]
            updates = [u for u in self.updates if u["update_id"] >= self.offset]
            self.updates.clear()
            return updates
        raise AssertionError(method)


def update(uid, text, reply=None, user=12, chat=12, kind="private"):
    message = {"message_id": 1000 + uid, "text": text, "from": {"id": user},
               "chat": {"id": chat, "type": kind}}
    if reply is not None:
        message["reply_to_message"] = {"message_id": reply}
    return {"update_id": uid, "message": message}


class BridgeTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.ipc = FakeIpc()
        self.bot = FakeBot()
        self.bridge = Bridge(os.path.join(self.temp.name, "state.sqlite"),
                             self.ipc, self.bot, "pair-secret")
        self.bridge.ingest(update(1, "/pair pair-secret"))
        self.assertEqual(self.bridge.db.execute("SELECT count(*) FROM pairing").fetchone()[0], 1)

    def tearDown(self):
        self.bridge.db.close()
        self.temp.cleanup()

    def test_receipt_after_accepted_answer(self):
        self.ipc.cards = [card("A")]
        self.bridge.sync()
        self.bridge.deliver()
        self.bridge.ingest(update(2, "TAK"))
        self.assertEqual(self.bridge.db.execute("SELECT state FROM receipts").fetchone()[0], "waiting")
        self.bridge.process_actions()
        self.bridge.deliver_receipts()
        self.assertEqual(len(self.bot.messages), 2)
        ack = self.bot.messages[-1]
        self.assertIn("przyj\u0119ta do zadania", ack["text"])
        self.assertEqual(ack["reply_parameters"]["message_id"], 1002)
        self.assertEqual(self.bridge.db.execute("SELECT state FROM receipts").fetchone()[0], "sent")

    def test_receipt_unassigned_plain_message(self):
        self.bridge.ingest(update(2, "Cze??"))
        self.bridge.process_actions()
        self.bridge.deliver_receipts()
        self.assertEqual(len(self.bot.messages), 1)
        self.assertIn("nie zosta\u0142a przypisana", self.bot.messages[0]["text"])
        self.assertEqual(self.bot.messages[0]["reply_parameters"]["message_id"], 1002)

    def test_receipt_two_cards_ambiguous(self):
        self.ipc.cards = [card("A"), card("B")]
        self.bridge.sync()
        self.bridge.deliver()
        self.bridge.ingest(update(2, "TAK"))
        self.bridge.process_actions()
        self.bridge.deliver_receipts()
        self.assertEqual(len(self.bot.messages), 3)
        self.assertIn("nie zosta\u0142a przypisana", self.bot.messages[-1]["text"])

    def test_receipt_not_positive_while_ipc_down(self):
        self.ipc.cards = [card("A")]
        self.bridge.sync()
        self.bridge.deliver()
        self.bridge.ingest(update(2, "TAK"))
        self.ipc.fail = True
        self.bridge.process_actions()
        self.bridge.deliver_receipts()
        self.assertEqual(len(self.bot.messages), 1)
        self.assertEqual(self.bridge.db.execute("SELECT state FROM receipts").fetchone()[0], "waiting")
        self.ipc.fail = False
        self.bridge.process_actions()
        self.bridge.deliver_receipts()
        self.assertEqual(len(self.bot.messages), 2)
        self.assertIn("przyj\u0119ta do zadania", self.bot.messages[-1]["text"])

    def test_receipt_duplicate_update_and_retry(self):
        u = update(2, "TAK")
        self.bridge.ingest(u)
        self.bridge.ingest(u)
        self.bridge.deliver_receipts()
        self.bridge.deliver_receipts()
        self.assertEqual(len(self.bot.messages), 1)
        self.assertEqual(self.bridge.db.execute("SELECT count(*) FROM receipts").fetchone()[0], 1)

    def test_receipt_ipc_rejected_response_is_not_success(self):
        self.ipc.cards = [card("A")]
        self.bridge.sync()
        self.bridge.deliver()
        self.bridge.ingest(update(2, "NOT_A_VALID_ANSWER"))
        self.ipc.answer_response = {"status": "expired", "accepted": False}
        self.bridge.process_actions()
        self.bridge.deliver_receipts()
        self.assertIn("nie przyj\u0119\u0142o", self.bot.messages[-1]["text"])
        self.assertNotIn("przyj\u0119ta do zadania", self.bot.messages[-1]["text"])

    def test_receipt_429_retry(self):
        self.bridge.ingest(update(2, "TAK"))
        self.bot.error = RateLimited(3)
        self.bridge.deliver_receipts()
        row = self.bridge.db.execute("SELECT state,next_attempt FROM receipts").fetchone()
        self.assertEqual(row["state"], "queued")
        self.assertGreater(row["next_attempt"], 0)
        self.assertEqual(len(self.bot.messages), 0)

    def test_receipt_unknown_outcome_no_retry(self):
        self.bridge.ingest(update(2, "TAK"))
        self.bot.error = OSError("unknown send result")
        self.bridge.deliver_receipts()
        self.bot.error = None
        self.bridge.deliver_receipts()
        self.assertEqual(self.bridge.db.execute("SELECT state FROM receipts").fetchone()[0], "uncertain")
        self.assertEqual(len(self.bot.messages), 0)

    def test_receipt_not_for_unpaired_person_or_group(self):
        self.bridge.ingest(update(2, "TAK", user=99, chat=99))
        self.bridge.ingest(update(3, "TAK", kind="group"))
        self.bridge.deliver_receipts()
        self.assertEqual(len(self.bot.messages), 0)
        self.assertEqual(self.bridge.db.execute("SELECT count(*) FROM receipts").fetchone()[0], 0)

    def register_phone_test(self):
        code = "FIZYCZNY-TELEFON-1112-0104"
        self.bridge.db.execute("""
            INSERT INTO phone_acceptance_tests
            (test_id,chat_id,bot_message_id,expected_sha256,state)
            VALUES(?,?,?,?,?)
        """, ("C3", 12, 57, hashlib.sha256(code.encode()).hexdigest(), "pending"))
        return code

    def test_phone_acceptance_exact_reply_captured_once(self):
        code = self.register_phone_test()
        self.bridge.ingest(update(2, code, reply=57))
        self.bridge.ingest(update(2, code, reply=57))
        item = self.bridge.db.execute("""
            SELECT state,update_id,reply_message_id FROM phone_acceptance_tests
            WHERE test_id='C3'
        """).fetchone()
        self.assertEqual(tuple(item), ("response_received", 2, 1002))

    def test_phone_acceptance_plain_code_not_sufficient(self):
        code = self.register_phone_test()
        self.bridge.ingest(update(2, code))
        item = self.bridge.db.execute("""
            SELECT state,update_id FROM phone_acceptance_tests WHERE test_id='C3'
        """).fetchone()
        self.assertEqual(tuple(item), ("pending", None))

    def test_phone_acceptance_wrong_reply_or_code_not_sufficient(self):
        code = self.register_phone_test()
        self.bridge.ingest(update(2, code, reply=99))
        self.bridge.ingest(update(3, "wrong code", reply=57))
        self.assertEqual(self.bridge.db.execute("""
            SELECT state FROM phone_acceptance_tests WHERE test_id='C3'
        """).fetchone()[0], "pending")

    def test_phone_acceptance_wrong_sender_not_sufficient(self):
        code = self.register_phone_test()
        self.bridge.ingest(update(2, code, reply=57, user=99,chat=99))
        self.assertEqual(self.bridge.db.execute("""
            SELECT state FROM phone_acceptance_tests WHERE test_id='C3'
        """).fetchone()[0], "pending")

    def test_two_cards_reverse_reply(self):
        self.ipc.cards = [card("A"), card("B")]
        self.bridge.sync()
        self.bridge.deliver()
        self.assertEqual(len(self.bot.messages), 2)
        self.bridge.ingest(update(2, "answer B", reply=102))
        self.bridge.ingest(update(3, "answer A", reply=101))
        self.bridge.process_actions()
        self.assertEqual([(a["cardId"], a["text"]) for a in self.ipc.answers],
                         [("B", "answer B"), ("A", "answer A")])

    def test_wrong_reply_not_routed(self):
        self.ipc.cards = [card("A")]
        self.bridge.sync()
        self.bridge.deliver()
        self.bridge.ingest(update(2, "wrong", reply=999))
        self.bridge.process_actions()
        self.assertEqual(self.ipc.answers, [])

    def test_plain_answer_single_delivered_ask(self):
        self.ipc.cards = [card("A")]
        self.bridge.sync()
        self.bridge.deliver()
        self.bridge.ingest(update(2, "TAK"))
        self.bridge.process_actions()
        self.assertEqual([(a["cardId"], a["text"]) for a in self.ipc.answers],
                         [("A", "TAK")])

    def test_plain_answer_not_routed_with_two_open_cards(self):
        self.ipc.cards = [card("A"), card("B")]
        self.bridge.sync()
        self.bridge.deliver()
        self.bridge.ingest(update(2, "TAK"))
        self.bridge.process_actions()
        self.assertEqual(self.ipc.answers, [])

    def test_plain_answer_never_implicitly_confirms(self):
        self.ipc.cards = [card("A", kind="confirm")]
        self.bridge.sync()
        self.bridge.deliver()
        self.bridge.ingest(update(2, "yes"))
        self.bridge.process_actions()
        self.assertEqual(self.ipc.answers, [])

    def test_plain_command_not_routed(self):
        self.ipc.cards = [card("A")]
        self.bridge.sync()
        self.bridge.deliver()
        self.bridge.ingest(update(2, "/start"))
        self.bridge.process_actions()
        self.assertEqual(self.ipc.answers, [])

    def test_plain_answer_not_routed_when_card_expired(self):
        self.ipc.cards = [card("A", seconds=-1)]
        self.bridge.sync()
        self.bridge.deliver()
        self.bridge.ingest(update(2, "TAK"))
        self.bridge.process_actions()
        self.assertEqual(self.ipc.answers, [])

    def test_unauthorized_and_group(self):
        self.ipc.cards = [card("A")]
        self.bridge.sync()
        self.bridge.deliver()
        self.bridge.ingest(update(2, "secret", reply=101, user=88, chat=88))
        self.bridge.ingest(update(3, "group", reply=101, kind="group"))
        self.bridge.process_actions()
        self.assertEqual(self.ipc.answers, [])

    def test_duplicate_update(self):
        self.ipc.cards = [card("A")]
        self.bridge.sync()
        self.bridge.deliver()
        u = update(2, "response", reply=101)
        self.bridge.ingest(u)
        self.bridge.ingest(u)
        self.bridge.process_actions()
        self.assertEqual(len(self.ipc.answers), 1)

    def test_pending_action_replay_after_ipc_error(self):
        self.ipc.cards = [card("A")]
        self.bridge.sync()
        self.bridge.deliver()
        self.bridge.ingest(update(2, "answer", reply=101))
        self.ipc.fail = True
        self.bridge.process_actions()
        self.assertEqual(self.bridge.db.execute("SELECT status FROM actions").fetchone()[0], "pending")
        self.ipc.fail = False
        self.bridge.process_actions()
        self.assertEqual(self.bridge.db.execute("SELECT status FROM actions").fetchone()[0], "accepted")
        self.assertEqual(len(self.ipc.answers), 1)

    def test_expired_card_never_delivered(self):
        self.ipc.cards = [card("A", seconds=-1)]
        self.bridge.sync()
        self.bridge.deliver()
        self.assertEqual(self.bot.messages, [])
        self.assertEqual(self.bridge.db.execute("SELECT status FROM cards").fetchone()[0], "closed")

    def test_instance_switch_closes_stale(self):
        self.ipc.cards = [card("A")]
        self.bridge.sync()
        self.ipc.instance = "instance2"
        self.ipc.cards = [card("B", instance="instance2")]
        self.bridge.sync()
        rows = dict(self.bridge.db.execute("SELECT id,status FROM cards").fetchall())
        self.assertEqual(rows, {"A": "closed", "B": "open"})

    def test_429_retry_state(self):
        self.ipc.cards = [card("A")]
        self.bridge.sync()
        self.bot.error = RateLimited(3)
        self.bridge.deliver()
        self.assertEqual(self.bridge.db.execute("SELECT status FROM outbox").fetchone()[0], "queued")
        self.assertGreater(self.bridge.db.execute("SELECT next_attempt FROM outbox").fetchone()[0], 0)

    def test_unknown_send_result_not_blindly_retried(self):
        self.ipc.cards = [card("A")]
        self.bridge.sync()
        self.bot.error = OSError("Unknown send outcome")
        self.bridge.deliver()
        self.assertEqual(self.bridge.db.execute("SELECT status FROM outbox").fetchone()[0], "uncertain")
        self.bot.error = None
        self.bridge.deliver()
        self.assertEqual(self.bot.messages, [])

    def test_confirm_callback_exact_token(self):
        self.ipc.cards = [card("C", kind="confirm")]
        self.bridge.sync()
        self.bridge.deliver()
        token = self.bot.messages[0]["reply_markup"]["inline_keyboard"][0][0]["callback_data"]
        self.bridge.ingest({"update_id": 2, "callback_query": {
            "data": token, "from": {"id": 12},
            "message": {"chat": {"type": "private", "id": 12}}}})
        self.bridge.process_actions()
        self.assertEqual(self.ipc.answers[0]["cardId"], "C")
        self.assertEqual(self.ipc.answers[0]["text"], "yes")

    def test_poll_offset_persisted(self):
        self.bot.updates = [update(2, "nothing")]
        self.bridge.poll()
        self.assertEqual(self.bridge.get_meta("offset"), "3")
        self.bridge.poll()
        self.assertEqual(self.bot.offset, 3)


if __name__ == "__main__":
    unittest.main(verbosity=2)
