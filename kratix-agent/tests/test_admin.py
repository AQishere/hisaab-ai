"""B3: feedback, Owner/Admin stats, retention cleanup and backups."""

import sqlite3
import time
from datetime import UTC, datetime, timedelta

import pytest

from app import main
from app.admin import Housekeeping, build_stats, percentile
from app.store import LogEntry, Store
from tests.test_chat import client, token  # noqa: F401

NOW = datetime(2026, 10, 5, 9, 0, tzinfo=UTC)


def iso(moment: datetime) -> str:
    return moment.isoformat(timespec="milliseconds")


def entry(**fields) -> LogEntry:
    return LogEntry(**{"user_id": 1, "username": "owner", "role": "Owner", "question": "q", "model": "qwen2.5:7b",
                       "tool_calls": [], "total_ms": 1000, "status": "ok", **fields})


def answered(store: Store, user_id=1, question="Who owes us?", answer="Nobody.", **fields) -> tuple[str, str]:
    username = "owner" if user_id == 1 else "sana"
    session = store.create_session(user_id, username)
    fields.setdefault("role", "Owner" if user_id == 1 else "Sales")
    message_id = store.save_answer(session["id"], question, answer, entry(
        user_id=user_id, username=username, question=question, session_id=session["id"], **fields))
    return session["id"], message_id


# ---------------------------------------------------------------- feedback

def test_feedback_is_one_per_user_and_replaced():
    store = Store(":memory:")
    _, message_id = answered(store)
    assert store.answer_owner(message_id) == 1
    store.set_feedback(message_id, 1, -1, "Wrong total")
    store.set_feedback(message_id, 1, 1, None)
    rows = store.stats_rows("2000-01-01")["feedback"]
    assert len(rows) == 1 and rows[0]["rating"] == 1 and rows[0]["comment"] is None


def test_only_live_answers_can_be_rated():
    store = Store(":memory:")
    session_id, message_id = answered(store)
    question_id = store.messages(session_id)[0]["id"]
    assert store.answer_owner(question_id) is None  # the user's own question, not an answer
    assert store.answer_owner("missing") is None
    store.delete_session(session_id, 1)
    assert store.answer_owner(message_id) is None


def test_feedback_route(client):  # noqa: F811
    store = main.app.state.store
    _, message_id = answered(store)
    owner = {"Authorization": f"Bearer {token()}"}
    other = {"Authorization": f"Bearer {token(2, 'sana', 'Sales')}"}
    ok = client.post(f"/messages/{message_id}/feedback", json={"rating": -1, "comment": "  wrong  "}, headers=owner)
    assert ok.status_code == 200 and ok.json()["comment"] == "wrong"
    assert client.post(f"/messages/{message_id}/feedback", json={"rating": 1}, headers=other).status_code == 404
    assert client.post(f"/messages/{message_id}/feedback", json={"rating": 0}, headers=owner).status_code == 422
    assert client.post(f"/messages/{message_id}/feedback", json={"rating": 1}).status_code == 401


# ---------------------------------------------------------------- stats

def test_percentile():
    assert percentile([], 50) is None
    assert percentile([300, 100, 200], 50) == 200
    assert percentile(list(range(1, 101)), 95) == 95


def test_stats_add_up():
    store = Store(":memory:")
    answered(store, first_token_ms=400, total_ms=900,
             tool_calls=[{"name": "receivables", "arguments": {}, "error": None, "ms": 5}])
    _, bad = answered(store, question="Sales today?", answer="Your role cannot see that.", user_id=2, status="denied",
                      first_token_ms=600, total_ms=700,
                      tool_calls=[{"name": "sales_report", "arguments": {}, "error": "Your role does not have access", "ms": 3}])
    store.log(entry(status="error", error="Ollama down", total_ms=50))
    store.log(entry(status="busy", total_ms=0))
    store.set_feedback(bad, 2, -1, "Should see sales")

    stats = build_stats(store, 7, datetime.now(UTC))
    assert stats["questions"] == 4 and stats["byStatus"] == {"ok": 1, "denied": 1, "error": 1, "busy": 1}
    assert stats["failedPercent"] == 25.0
    assert stats["latencyMs"]["firstWords"] == {"p50": 400, "p95": 600}
    assert stats["latencyMs"]["complete"] == {"p50": 700, "p95": 900}
    assert {(t["name"], t["calls"], t["errors"]) for t in stats["tools"]} == {("receivables", 1, 0), ("sales_report", 1, 1)}
    assert stats["perUser"][0] == {"username": "owner", "role": "Owner", "questions": 3}
    assert stats["feedback"] == {"up": 0, "down": 1}
    assert stats["thumbsDown"][0]["question"] == "Sales today?" and stats["thumbsDown"][0]["comment"] == "Should see sales"
    assert sum(d["questions"] for d in stats["perDay"]) == 4


def test_stats_only_count_the_window():
    store = Store(":memory:")
    with store._lock:
        store._insert_log(entry(), None, iso(datetime.now(UTC) - timedelta(days=10)))
    answered(store)
    assert build_stats(store, 7, datetime.now(UTC))["questions"] == 1
    assert build_stats(store, 30, datetime.now(UTC))["questions"] == 2


def test_stats_route_is_owner_and_admin_only(client):  # noqa: F811
    assert client.get("/admin/stats", headers={"Authorization": f"Bearer {token()}"}).status_code == 200
    assert client.get("/admin/stats", headers={"Authorization": f"Bearer {token(5, 'admin', 'Admin')}"}).status_code == 200
    assert client.get("/admin/stats", headers={"Authorization": f"Bearer {token(2, 'sana', 'Sales')}"}).status_code == 403
    assert client.get("/admin/stats?days=0", headers={"Authorization": f"Bearer {token()}"}).status_code == 422


# ---------------------------------------------------------------- cleanup and backup

def keeper(store, tmp_path, clock, kept=14):
    return Housekeeping(store, tmp_path, log_days=180, deleted_session_days=30, backups_kept=kept, clock=lambda: clock[0])


def test_cleanup_removes_expired_reports_old_log_and_purged_conversations(tmp_path):
    store = Store(tmp_path / "agent.db")
    clock = [NOW]

    # A report that expires in 7 days, linked to an answer.
    store.add_report({"id": "r1", "user_id": 1, "type": "sales", "format": "xlsx", "params": {}, "file_name": "S.xlsx",
                      "size_bytes": 1, "created_at": iso(NOW), "expires_at": iso(NOW + timedelta(days=7))})
    file = tmp_path / "reports" / "1" / "r1.xlsx"
    file.parent.mkdir(parents=True)
    file.write_bytes(b"x")
    session = store.create_session(1, "owner")
    kept_answer = store.save_answer(session["id"], "Sales file", "Ready.", entry(session_id=session["id"]), report_id="r1")

    # A conversation deleted 31 days ago, with feedback; and one deleted yesterday.
    old_session, old_answer = answered(store)
    store.set_feedback(old_answer, 1, 1, None)
    recent_session, _ = answered(store)
    with store._lock:
        store._db.execute("UPDATE sessions SET deleted_at = ? WHERE id = ?", (iso(NOW - timedelta(days=31)), old_session))
        store._db.execute("UPDATE sessions SET deleted_at = ? WHERE id = ?", (iso(NOW - timedelta(days=1)), recent_session))
        store._insert_log(entry(question="ancient"), None, iso(NOW - timedelta(days=181)))

    assert keeper(store, tmp_path, clock).cleanup() == {"reports": 0, "files": 0, "logRows": 1, "sessions": 1}
    assert store.messages(old_session) == [] and store.stats_rows("2000")["feedback"] == []
    # The purged conversation's log row stays for audit, unlinked.
    assert [r["message_id"] for r in store.log_rows() if r["question"] == "Who owes us?"].count(None) == 1
    assert len(store.messages(recent_session)) == 2

    clock[0] = NOW + timedelta(days=7)
    assert keeper(store, tmp_path, clock).cleanup()["files"] == 1
    assert not file.exists() and store.get_report("r1", "2000") is None
    assert store.messages(session["id"])[1]["id"] == kept_answer and store.messages(session["id"])[1]["reportId"] is None


def test_backup_is_a_readable_copy_and_old_ones_rotate(tmp_path):
    store = Store(tmp_path / "agent.db")
    answered(store)
    clock = [NOW]
    paths = []
    for minute in range(4):
        clock[0] = NOW + timedelta(minutes=minute)
        paths.append(keeper(store, tmp_path, clock, kept=3).backup())
    assert sorted(p.name for p in (tmp_path / "backups").glob("*.db")) == sorted(p.name for p in paths[1:])
    copy = sqlite3.connect(paths[-1])
    assert copy.execute("SELECT COUNT(*) FROM messages").fetchone()[0] == 2
    copy.close()


def test_housekeeping_runs_at_startup(client, tmp_path):  # noqa: F811
    # It runs in the background, so give it a moment.
    deadline = time.monotonic() + 3
    while not list((tmp_path / "backups").glob("agent-*.db")) and time.monotonic() < deadline:
        time.sleep(0.05)
    assert list((tmp_path / "backups").glob("agent-*.db"))
