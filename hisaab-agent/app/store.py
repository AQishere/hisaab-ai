"""The agent's own data in SQLite: chat sessions, messages, the question log, reports and feedback.

Hisaab_ERP is never written to. Losing this file loses chat history and logs, never business data.
"""

import json
import sqlite3
import threading
import uuid
from dataclasses import dataclass
from datetime import UTC, datetime
from pathlib import Path
from typing import Any

# Numbered migrations; PRAGMA user_version records how many have been applied.
MIGRATIONS = [
    """
    CREATE TABLE sessions (
      id TEXT PRIMARY KEY,
      user_id INTEGER NOT NULL,
      username TEXT NOT NULL,
      title TEXT,
      created_at TEXT NOT NULL,
      updated_at TEXT NOT NULL,
      deleted_at TEXT
    );
    CREATE INDEX ix_sessions_user ON sessions(user_id, updated_at);

    CREATE TABLE reports (
      id TEXT PRIMARY KEY,
      user_id INTEGER NOT NULL,
      type TEXT NOT NULL,
      format TEXT NOT NULL CHECK (format IN ('xlsx', 'pptx')),
      params TEXT NOT NULL,
      file_name TEXT NOT NULL,
      size_bytes INTEGER NOT NULL,
      created_at TEXT NOT NULL,
      expires_at TEXT NOT NULL
    );
    CREATE INDEX ix_reports_user ON reports(user_id, created_at);

    CREATE TABLE messages (
      id TEXT PRIMARY KEY,
      session_id TEXT NOT NULL REFERENCES sessions(id),
      role TEXT NOT NULL CHECK (role IN ('user', 'assistant')),
      content TEXT NOT NULL,
      report_id TEXT REFERENCES reports(id),
      created_at TEXT NOT NULL
    );
    CREATE INDEX ix_messages_session ON messages(session_id, created_at);

    CREATE TABLE question_log (
      id INTEGER PRIMARY KEY AUTOINCREMENT,
      message_id TEXT REFERENCES messages(id),
      session_id TEXT,
      user_id INTEGER NOT NULL,
      username TEXT NOT NULL,
      role TEXT NOT NULL,
      question TEXT NOT NULL,
      tool_calls TEXT NOT NULL,
      model TEXT NOT NULL,
      first_token_ms INTEGER,
      llm_ms INTEGER,
      erp_ms INTEGER,
      total_ms INTEGER NOT NULL,
      status TEXT NOT NULL CHECK (status IN ('ok', 'denied', 'error', 'timeout', 'busy')),
      error TEXT,
      created_at TEXT NOT NULL
    );
    CREATE INDEX ix_log_created ON question_log(created_at);
    CREATE INDEX ix_log_user ON question_log(user_id, created_at);

    CREATE TABLE feedback (
      message_id TEXT NOT NULL REFERENCES messages(id),
      user_id INTEGER NOT NULL,
      rating INTEGER NOT NULL CHECK (rating IN (-1, 1)),
      comment TEXT,
      created_at TEXT NOT NULL,
      PRIMARY KEY (message_id, user_id)
    );
    """,
    # Which provider answered (gemini, groq, ollama; "gemini,groq" when one question used both), how many model
    # calls fell back to a later provider, tokens and estimated cost.
    """
    ALTER TABLE question_log ADD COLUMN provider TEXT;
    ALTER TABLE question_log ADD COLUMN fallbacks INTEGER NOT NULL DEFAULT 0;
    ALTER TABLE question_log ADD COLUMN prompt_tokens INTEGER;
    ALTER TABLE question_log ADD COLUMN completion_tokens INTEGER;
    ALTER TABLE question_log ADD COLUMN cost_usd REAL;
    """,
]

TITLE_LENGTH = 60


def now() -> str:
    return datetime.now(UTC).isoformat(timespec="milliseconds")


@dataclass
class LogEntry:
    user_id: int
    username: str
    role: str
    question: str
    model: str
    tool_calls: list[dict[str, Any]]
    total_ms: int
    status: str
    session_id: str | None = None
    first_token_ms: int | None = None
    llm_ms: int | None = None
    erp_ms: int | None = None
    error: str | None = None
    provider: str | None = None
    fallbacks: int = 0
    prompt_tokens: int | None = None
    completion_tokens: int | None = None
    cost_usd: float | None = None


class Store:
    def __init__(self, path: Path | str):
        if str(path) != ":memory:":
            Path(path).parent.mkdir(parents=True, exist_ok=True)
        self._db = sqlite3.connect(str(path), check_same_thread=False, isolation_level=None)
        self._db.row_factory = sqlite3.Row
        self._lock = threading.Lock()
        self._db.execute("PRAGMA journal_mode = WAL")
        self._db.execute("PRAGMA foreign_keys = ON")
        self._migrate()

    def close(self) -> None:
        self._db.close()

    def _migrate(self) -> None:
        version = self._db.execute("PRAGMA user_version").fetchone()[0]
        for number, script in enumerate(MIGRATIONS[version:], start=version + 1):
            self._db.executescript(f"BEGIN; {script}; PRAGMA user_version = {number}; COMMIT;")

    @property
    def schema_version(self) -> int:
        return self._db.execute("PRAGMA user_version").fetchone()[0]

    def ping(self) -> bool:
        try:
            with self._lock:
                self._db.execute("SELECT 1").fetchone()
            return True
        except sqlite3.Error:
            return False

    def _rows(self, sql: str, params: tuple = ()) -> list[dict[str, Any]]:
        with self._lock:
            return [dict(r) for r in self._db.execute(sql, params).fetchall()]

    # ------------------------------------------------------------ sessions

    def create_session(self, user_id: int, username: str) -> dict[str, Any]:
        session = {"id": str(uuid.uuid4()), "title": None, "createdAt": now()}
        with self._lock:
            self._db.execute(
                "INSERT INTO sessions (id, user_id, username, title, created_at, updated_at) VALUES (?, ?, ?, NULL, ?, ?)",
                (session["id"], user_id, username, session["createdAt"], session["createdAt"]))
        return session

    def list_sessions(self, user_id: int) -> list[dict[str, Any]]:
        return self._rows(
            "SELECT id, title, created_at AS createdAt, updated_at AS updatedAt FROM sessions "
            "WHERE user_id = ? AND deleted_at IS NULL ORDER BY updated_at DESC", (user_id,))

    def owns_session(self, session_id: str, user_id: int) -> bool:
        return bool(self._rows(
            "SELECT 1 FROM sessions WHERE id = ? AND user_id = ? AND deleted_at IS NULL", (session_id, user_id)))

    def delete_session(self, session_id: str, user_id: int) -> bool:
        with self._lock:
            cursor = self._db.execute(
                "UPDATE sessions SET deleted_at = ? WHERE id = ? AND user_id = ? AND deleted_at IS NULL",
                (now(), session_id, user_id))
        return cursor.rowcount == 1

    def messages(self, session_id: str) -> list[dict[str, Any]]:
        return self._rows(
            "SELECT id, role, content, report_id AS reportId, created_at AS createdAt FROM messages "
            "WHERE session_id = ? ORDER BY created_at, rowid", (session_id,))

    def history(self, session_id: str, limit: int) -> list[dict[str, str]]:
        """The last `limit` messages, oldest first, in the shape the model expects."""
        rows = self._rows(
            "SELECT role, content FROM messages WHERE session_id = ? ORDER BY created_at DESC, rowid DESC LIMIT ?",
            (session_id, limit))
        return list(reversed(rows))

    # ------------------------------------------------------------ answers and log

    def _insert_log(self, entry: LogEntry, message_id: str | None, created_at: str) -> None:
        self._db.execute(
            "INSERT INTO question_log (message_id, session_id, user_id, username, role, question, tool_calls, model, "
            "first_token_ms, llm_ms, erp_ms, total_ms, status, error, created_at, provider, fallbacks, prompt_tokens, "
            "completion_tokens, cost_usd) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
            (message_id, entry.session_id, entry.user_id, entry.username, entry.role, entry.question,
             json.dumps(entry.tool_calls, default=str), entry.model, entry.first_token_ms, entry.llm_ms, entry.erp_ms,
             entry.total_ms, entry.status, entry.error, created_at, entry.provider, entry.fallbacks,
             entry.prompt_tokens, entry.completion_tokens, entry.cost_usd))

    def save_answer(self, session_id: str, question: str, answer: str, entry: LogEntry,
                    report_id: str | None = None) -> str:
        """Store the question, the answer and the log row in one transaction; returns the answer's message id."""
        asked_at, answered_at = now(), now()
        message_id = str(uuid.uuid4())
        with self._lock:
            self._db.execute("BEGIN")
            try:
                self._db.execute("INSERT INTO messages (id, session_id, role, content, created_at) VALUES (?, ?, 'user', ?, ?)",
                                 (str(uuid.uuid4()), session_id, question, asked_at))
                self._db.execute(
                    "INSERT INTO messages (id, session_id, role, content, report_id, created_at) "
                    "VALUES (?, ?, 'assistant', ?, ?, ?)",
                    (message_id, session_id, answer, report_id, answered_at))
                self._db.execute(
                    "UPDATE sessions SET updated_at = ?, title = COALESCE(title, ?) WHERE id = ?",
                    (answered_at, question[:TITLE_LENGTH], session_id))
                self._insert_log(entry, message_id, answered_at)
                self._db.execute("COMMIT")
            except BaseException:
                self._db.execute("ROLLBACK")
                raise
        return message_id

    def log(self, entry: LogEntry) -> None:
        """Log a question that got no answer (error, timeout, busy)."""
        with self._lock:
            self._insert_log(entry, None, now())

    def log_rows(self, user_id: int | None = None) -> list[dict[str, Any]]:
        if user_id is None:
            return self._rows("SELECT * FROM question_log ORDER BY id")
        return self._rows("SELECT * FROM question_log WHERE user_id = ? ORDER BY id", (user_id,))

    # ------------------------------------------------------------ reports

    def add_report(self, report: dict[str, Any]) -> None:
        with self._lock:
            self._db.execute(
                "INSERT INTO reports (id, user_id, type, format, params, file_name, size_bytes, created_at, expires_at) "
                "VALUES (:id, :user_id, :type, :format, :params, :file_name, :size_bytes, :created_at, :expires_at)",
                {**report, "params": json.dumps(report["params"])})

    def reports_since(self, user_id: int, since: str) -> int:
        return self._rows("SELECT COUNT(*) AS n FROM reports WHERE user_id = ? AND created_at >= ?",
                          (user_id, since))[0]["n"]

    def list_reports(self, user_id: int, now_iso: str) -> list[dict[str, Any]]:
        return [_report_out(r) for r in self._rows(
            "SELECT * FROM reports WHERE user_id = ? AND expires_at > ? ORDER BY created_at DESC", (user_id, now_iso))]

    def get_report(self, report_id: str, now_iso: str) -> dict[str, Any] | None:
        rows = self._rows("SELECT * FROM reports WHERE id = ? AND expires_at > ?", (report_id, now_iso))
        return _report_out(rows[0]) if rows else None


    # ------------------------------------------------------------ feedback

    def answer_owner(self, message_id: str) -> int | None:
        """The user whose conversation holds this answer, or None if it is not a live answer."""
        rows = self._rows(
            "SELECT s.user_id FROM messages m JOIN sessions s ON s.id = m.session_id "
            "WHERE m.id = ? AND m.role = 'assistant' AND s.deleted_at IS NULL", (message_id,))
        return rows[0]["user_id"] if rows else None

    def set_feedback(self, message_id: str, user_id: int, rating: int, comment: str | None) -> dict[str, Any]:
        """One rating per user per answer; posting again replaces it."""
        created_at = now()
        with self._lock:
            self._db.execute(
                "INSERT INTO feedback (message_id, user_id, rating, comment, created_at) VALUES (?, ?, ?, ?, ?) "
                "ON CONFLICT (message_id, user_id) DO UPDATE SET rating = excluded.rating, comment = excluded.comment, "
                "created_at = excluded.created_at",
                (message_id, user_id, rating, comment, created_at))
        return {"messageId": message_id, "rating": rating, "comment": comment, "createdAt": created_at}

    # ------------------------------------------------------------ stats (Owner/Admin)

    def stats_rows(self, since: str) -> dict[str, list[dict[str, Any]]]:
        """Raw rows for /admin/stats; the service does the arithmetic."""
        return {
            "log": self._rows(
                "SELECT username, role, tool_calls, status, first_token_ms, total_ms, created_at, provider, fallbacks, "
                "prompt_tokens, completion_tokens, cost_usd FROM question_log "
                "WHERE created_at >= ? ORDER BY created_at", (since,)),
            "feedback": self._rows(
                "SELECT f.message_id, f.rating, f.comment, f.created_at, m.content AS answer, "
                "q.question, q.username FROM feedback f "
                "JOIN messages m ON m.id = f.message_id "
                "LEFT JOIN question_log q ON q.message_id = f.message_id "
                "WHERE f.created_at >= ? ORDER BY f.created_at DESC", (since,)),
            "reports": self._rows("SELECT type, format FROM reports WHERE created_at >= ?", (since,)),
        }

    # ------------------------------------------------------------ retention and backup

    def cleanup(self, *, now_iso: str, log_before: str, deleted_sessions_before: str) -> dict[str, Any]:
        """Remove expired reports (returns them so their files can be deleted), old question log rows, and
        conversations deleted long enough ago. Links to removed rows are cleared first, in one transaction."""
        with self._lock:
            self._db.execute("BEGIN")
            try:
                expired = [dict(r) for r in self._db.execute(
                    "SELECT id, user_id, format FROM reports WHERE expires_at <= ?", (now_iso,)).fetchall()]
                self._db.execute("UPDATE messages SET report_id = NULL WHERE report_id IN "
                                 "(SELECT id FROM reports WHERE expires_at <= ?)", (now_iso,))
                self._db.execute("DELETE FROM reports WHERE expires_at <= ?", (now_iso,))

                log = self._db.execute("DELETE FROM question_log WHERE created_at < ?", (log_before,)).rowcount

                gone = "SELECT id FROM sessions WHERE deleted_at IS NOT NULL AND deleted_at < ?"
                gone_messages = f"SELECT id FROM messages WHERE session_id IN ({gone})"
                # Log rows outlive the conversation for audit; only their link to the message goes.
                self._db.execute(f"UPDATE question_log SET message_id = NULL WHERE message_id IN ({gone_messages})",
                                 (deleted_sessions_before,))
                self._db.execute(f"DELETE FROM feedback WHERE message_id IN ({gone_messages})", (deleted_sessions_before,))
                self._db.execute(f"DELETE FROM messages WHERE session_id IN ({gone})", (deleted_sessions_before,))
                sessions = self._db.execute(
                    "DELETE FROM sessions WHERE deleted_at IS NOT NULL AND deleted_at < ?",
                    (deleted_sessions_before,)).rowcount
                self._db.execute("COMMIT")
            except BaseException:
                self._db.execute("ROLLBACK")
                raise
        return {"reports": expired, "logRows": log, "sessions": sessions}

    def backup(self, target: Path) -> None:
        """Consistent copy while the agent keeps running (SQLite's online backup)."""
        target.parent.mkdir(parents=True, exist_ok=True)
        destination = sqlite3.connect(str(target))
        try:
            with self._lock:
                self._db.backup(destination)
        finally:
            destination.close()


def _report_out(row: dict[str, Any]) -> dict[str, Any]:
    return {"id": row["id"], "userId": row["user_id"], "type": row["type"], "format": row["format"],
            "params": json.loads(row["params"]), "fileName": row["file_name"], "sizeBytes": row["size_bytes"],
            "createdAt": row["created_at"], "expiresAt": row["expires_at"]}
