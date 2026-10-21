"""Owner/Admin usage stats, and daily housekeeping: expired reports, old log rows, purged conversations, backups."""

import json
import logging
import math
from collections import Counter, defaultdict
from datetime import UTC, datetime, timedelta
from pathlib import Path
from typing import Any

from .store import Store

log = logging.getLogger("kratix.agent")
ADMIN_ROLES = {"Owner", "Admin"}
# A question counts as failed when the user got no answer.
FAILED = {"error", "timeout"}


def _iso(moment: datetime) -> str:
    return moment.isoformat(timespec="milliseconds")


def percentile(values: list[int], p: float) -> int | None:
    """Nearest-rank percentile; None when there is nothing to measure."""
    if not values:
        return None
    ordered = sorted(values)
    return ordered[max(0, math.ceil(p / 100 * len(ordered)) - 1)]


def _local_day(created_at: str) -> str:
    return datetime.fromisoformat(created_at).astimezone().date().isoformat()


def build_stats(store: Store, days: int, now: datetime) -> dict[str, Any]:
    since = now - timedelta(days=days)
    rows = store.stats_rows(_iso(since))
    entries = rows["log"]

    statuses = Counter(e["status"] for e in entries)
    answered = [e for e in entries if e["status"] in ("ok", "denied")]
    per_day: dict[str, dict[str, int]] = defaultdict(lambda: {"questions": 0, "failed": 0})
    per_user: dict[tuple[str, str], int] = Counter()
    tools: dict[str, dict[str, int]] = defaultdict(lambda: {"calls": 0, "errors": 0})
    for e in entries:
        day = per_day[_local_day(e["created_at"])]
        day["questions"] += 1
        day["failed"] += e["status"] in FAILED
        per_user[(e["username"], e["role"])] += 1
        for call in json.loads(e["tool_calls"]):
            tools[call["name"]]["calls"] += 1
            tools[call["name"]]["errors"] += bool(call.get("error"))

    first = [e["first_token_ms"] for e in answered if e["first_token_ms"] is not None]
    total = [e["total_ms"] for e in answered]
    ratings = Counter(f["rating"] for f in rows["feedback"])
    return {
        "days": days,
        "from": _iso(since),
        "questions": len(entries),
        "byStatus": dict(statuses),
        "failedPercent": round(100 * sum(statuses[s] for s in FAILED) / len(entries), 1) if entries else 0.0,
        "latencyMs": {
            "firstWords": {"p50": percentile(first, 50), "p95": percentile(first, 95)},
            "complete": {"p50": percentile(total, 50), "p95": percentile(total, 95)},
        },
        "perDay": [{"date": d, **v} for d, v in sorted(per_day.items())],
        "perUser": [{"username": u, "role": r, "questions": n}
                    for (u, r), n in sorted(per_user.items(), key=lambda item: -item[1])],
        "tools": [{"name": n, **v} for n, v in sorted(tools.items(), key=lambda item: -item[1]["calls"])],
        "feedback": {"up": ratings[1], "down": ratings[-1]},
        "thumbsDown": [
            {"messageId": f["message_id"], "username": f["username"], "question": f["question"],
             "answer": f["answer"], "comment": f["comment"], "createdAt": f["created_at"]}
            for f in rows["feedback"] if f["rating"] == -1
        ],
        "reports": dict(Counter(f"{r['type']}.{r['format']}" for r in rows["reports"])),
        "llm": _llm_usage(entries),
    }


def _llm_usage(entries: list[dict[str, Any]]) -> dict[str, Any]:
    """Questions per provider, how often a fallback answered, tokens and estimated cost."""
    providers: dict[str, int] = Counter()
    for e in entries:
        for name in (e.get("provider") or "").split(","):
            if name:
                providers[name] += 1
    with_fallback = sum(1 for e in entries if e.get("fallbacks"))
    return {
        "byProvider": dict(providers),
        "fallbackQuestions": with_fallback,
        "fallbackPercent": round(100 * with_fallback / len(entries), 1) if entries else 0.0,
        "promptTokens": sum(e.get("prompt_tokens") or 0 for e in entries),
        "completionTokens": sum(e.get("completion_tokens") or 0 for e in entries),
        "costUsd": round(sum(e.get("cost_usd") or 0 for e in entries), 4),
    }


class Housekeeping:
    def __init__(self, store: Store, data_dir: Path, *, log_days: int, deleted_session_days: int, backups_kept: int,
                 clock=lambda: datetime.now(UTC)):
        self._store = store
        self._data_dir = data_dir
        self._log_days = log_days
        self._deleted_session_days = deleted_session_days
        self._backups_kept = backups_kept
        self._clock = clock

    def cleanup(self) -> dict[str, Any]:
        now = self._clock()
        removed = self._store.cleanup(
            now_iso=_iso(now), log_before=_iso(now - timedelta(days=self._log_days)),
            deleted_sessions_before=_iso(now - timedelta(days=self._deleted_session_days)))
        files = 0
        for report in removed["reports"]:
            path = self._data_dir / "reports" / str(report["user_id"]) / f"{report['id']}.{report['format']}"
            if path.is_file():
                path.unlink()
                files += 1
        result = {"reports": len(removed["reports"]), "files": files, "logRows": removed["logRows"],
                  "sessions": removed["sessions"]}
        log.info("Cleanup: %s", result)
        return result

    def backup(self) -> Path:
        """Copy agent.db to data/backups/, keeping the newest `backups_kept` copies."""
        folder = self._data_dir / "backups"
        target = folder / f"agent-{self._clock().astimezone():%Y%m%d-%H%M%S}.db"
        self._store.backup(target)
        for old in sorted(folder.glob("agent-*.db"))[:-self._backups_kept]:
            old.unlink()
        log.info("Backup written to %s", target)
        return target

    def run(self) -> dict[str, Any]:
        return {**self.cleanup(), "backup": self.backup().name}
