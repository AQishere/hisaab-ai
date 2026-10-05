"""One question in a chat session: limits, the model slot, the agent's streamed answer as server-sent events,
and the stored messages and question log."""

import asyncio
import json
import time
from collections.abc import AsyncIterator
from typing import Any

import httpx

from .agent import Agent, AgentResult
from .auth import CurrentUser
from .config import settings
from .erp_client import ErpClient, ErpError, ResponseCache
from .limits import LimitError, UserLimits
from .llm import BusyError, Llm, OllamaError
from .store import LogEntry, Store
from .tools import ToolContext

DENIED = "Your role does not have access to this information."


class ChatError(Exception):
    """Refused before the answer starts streaming, so it can still be an HTTP status."""

    def __init__(self, status: int, message: str):
        super().__init__(message)
        self.status = status
        self.message = message


def sse(event: str, data: Any) -> str:
    return f"event: {event}\ndata: {json.dumps(data, default=str)}\n\n"


def _ms(seconds: float) -> int:
    return round(seconds * 1000)


def report_out(report: dict[str, Any]) -> dict[str, Any]:
    """A report as the app sees it, with the link to download it."""
    return {k: v for k, v in report.items() if k != "userId"} | {"downloadUrl": f"/reports/{report['id']}/download"}


class ChatService:
    def __init__(self, store: Store, agent: Agent, llm: Llm, limits: UserLimits, erp_http: httpx.AsyncClient,
                 cache: ResponseCache, reports=None):
        self._store = store
        self._reports = reports
        self._agent = agent
        self._llm = llm
        self._limits = limits
        self._erp_http = erp_http
        self._cache = cache

    def _entry(self, user: CurrentUser, session_id: str | None, question: str, status: str, total_ms: int,
               **fields: Any) -> LogEntry:
        return LogEntry(user_id=user.id, username=user.username, role=user.role, question=question,
                        model=self._llm.model, tool_calls=fields.pop("tool_calls", []), total_ms=total_ms,
                        status=status, session_id=session_id, **fields)

    def start(self, user: CurrentUser, session_id: str, question: str) -> AsyncIterator[str]:
        """Check ownership and limits, then return the event stream. Raises ChatError before anything is sent."""
        if not self._store.owns_session(session_id, user.id):
            raise ChatError(404, "Conversation not found.")
        try:
            self._limits.acquire(user.id)
        except LimitError as exc:
            self._store.log(self._entry(user, session_id, question, exc.log_status, 0, error=exc.message))
            raise ChatError(exc.status, exc.message) from exc
        if self._llm.queued >= settings.llm_queue_cap:
            self._limits.release(user.id)
            self._store.log(self._entry(user, session_id, question, "busy", 0, error="model queue full"))
            raise ChatError(503, "The assistant is busy, try again in a minute.")
        return self._answer(user, session_id, question)

    async def _answer(self, user: CurrentUser, session_id: str, question: str) -> AsyncIterator[str]:
        started = time.perf_counter()
        first_token_ms: int | None = None
        result: AgentResult | None = None
        status, error, user_message = "error", None, None
        try:
            async with asyncio.timeout(settings.request_timeout_seconds):
                async with self._llm.slot():
                    history = self._store.history(session_id, settings.history_messages)
                    erp = ErpClient(self._erp_http, user.token, self._cache)
                    context = ToolContext(user, self._reports)
                    async for event in self._agent.run(question, history, erp, user.role, context=context):
                        if event.kind == "token":
                            if first_token_ms is None:
                                first_token_ms = _ms(time.perf_counter() - started)
                            yield sse("token", {"text": event.payload})
                        elif event.kind == "result":
                            result = event.payload
                        elif event.kind == "report":
                            yield sse("report", report_out(event.payload))
                        else:
                            yield sse(event.kind, event.payload)

            errors = [c.error for c in result.tool_calls]
            status = "denied" if errors and all(e == DENIED for e in errors) else "ok"
            total_ms = _ms(time.perf_counter() - started)
            message_id = self._store.save_answer(session_id, question, result.answer, self._entry(
                user, session_id, question, status, total_ms, tool_calls=_calls(result), first_token_ms=first_token_ms,
                llm_ms=result.llm_ms, erp_ms=result.erp_ms), report_id=result.report["id"] if result.report else None)
            yield sse("done", {
                "messageId": message_id,
                "toolCalls": [{"name": c.name, "arguments": c.arguments, "error": c.error} for c in result.tool_calls],
                "firstTokenMs": first_token_ms, "totalMs": total_ms,
                "report": report_out(result.report) if result.report else None,
            })
        except TimeoutError:
            status, error, user_message = "timeout", "request timed out", "That took too long. Please try again."
        except BusyError:
            status, error, user_message = "busy", "model queue full", "The assistant is busy, try again in a minute."
        except OllamaError as exc:
            error, user_message = str(exc), "The assistant is offline. Please try again shortly."
        except ErpError as exc:
            error = exc.message
            user_message = "Sign in again." if exc.status == 401 else "The ERP server is not responding."
        except Exception as exc:  # noqa: BLE001 - logged, and the user gets a plain message instead of a broken stream
            error, user_message = f"{type(exc).__name__}: {exc}", "Something went wrong. Please try again."
        finally:
            self._limits.release(user.id)
            if status not in ("ok", "denied"):
                self._store.log(self._entry(
                    user, session_id, question, status, _ms(time.perf_counter() - started),
                    tool_calls=_calls(result) if result else [], first_token_ms=first_token_ms,
                    error=error or "client disconnected"))
        if user_message:
            yield sse("error", {"message": user_message})


def _calls(result: AgentResult) -> list[dict[str, Any]]:
    return [{"name": c.name, "arguments": c.arguments, "error": c.error, "ms": c.ms} for c in result.tool_calls]
