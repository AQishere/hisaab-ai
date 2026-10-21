import asyncio
import logging
from contextlib import asynccontextmanager
from datetime import UTC, datetime
from typing import Literal

import httpx
from fastapi import Depends, FastAPI, Header, HTTPException, Request
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import FileResponse, StreamingResponse
from pydantic import BaseModel, Field

from .admin import ADMIN_ROLES, Housekeeping, build_stats
from .agent import Agent
from .auth import AuthError, CurrentUser, bearer_token, verify
from .chat import ChatError, ChatService, report_out
from .config import settings
from .erp_client import ErpClient, ResponseCache
from .limits import UserLimits
from .llm import Llm, Provider
from .reports.service import ReportError, ReportService
from .store import Store

log = logging.getLogger("kratix.agent")


async def _housekeeping(keeper: Housekeeping) -> None:
    while True:
        try:
            await asyncio.to_thread(keeper.run)
        except Exception as exc:  # noqa: BLE001 - try again next time; answering questions matters more
            log.warning("Housekeeping failed: %s", exc)
        await asyncio.sleep(settings.housekeeping_hours * 3600)


@asynccontextmanager
async def lifespan(app: FastAPI):
    if not settings.jwt_key:
        raise RuntimeError("AGENT_JWT_KEY is missing. Set it to the ERP's Jwt:Key in kratix-agent/.env (see README.md).")
    erp_http = httpx.AsyncClient(base_url=settings.erp_api_url.rstrip("/") + "/", timeout=settings.erp_timeout_seconds)
    providers = _providers()
    if not providers:
        raise RuntimeError("No language model is configured. Set AGENT_GEMINI_API_KEY (and optionally AGENT_GROQ_API_KEY "
                           "or AGENT_OLLAMA_URL) in kratix-agent/.env (see README.md).")
    llm = Llm(providers, parallel=settings.llm_parallel, queue_cap=settings.llm_queue_cap,
              first_token_seconds=settings.llm_first_token_seconds, breaker_failures=settings.llm_breaker_failures,
              breaker_seconds=settings.llm_breaker_seconds)
    store = Store(settings.data_dir / "agent.db")
    cache = ResponseCache(settings.erp_cache_seconds)
    reports = ReportService(store, settings.data_dir, settings.company_name,
                            retention_days=settings.report_retention_days, per_hour=settings.reports_per_hour)
    app.state.erp_http, app.state.llm, app.state.store, app.state.cache = erp_http, llm, store, cache
    app.state.reports = reports
    app.state.chat = ChatService(store, Agent(llm), llm, UserLimits(settings.questions_per_minute), erp_http, cache,
                                 reports)
    keeper = Housekeeping(store, settings.data_dir, log_days=settings.log_retention_days,
                          deleted_session_days=settings.deleted_session_days, backups_kept=settings.backups_kept)
    tasks = [asyncio.create_task(_housekeeping(keeper))]
    yield
    for task in tasks:
        task.cancel()
    await erp_http.aclose()
    for provider in providers:
        await provider.http.aclose()
    store.close()


def _providers() -> list[Provider]:
    """Gemini, then Groq, then a local Ollama: each one only if configured."""
    def client(url: str, key: str = "") -> httpx.AsyncClient:
        return httpx.AsyncClient(base_url=url.rstrip("/") + "/", timeout=settings.llm_timeout_seconds,
                                 headers={"Authorization": f"Bearer {key}"} if key else {})

    common = {"temperature": settings.temperature, "max_tokens": settings.max_answer_tokens}
    providers = []
    if settings.gemini_api_key:
        providers.append(Provider("gemini", client(settings.gemini_url, settings.gemini_api_key), settings.gemini_model,
                                  {**common, "reasoning_effort": settings.reasoning_effort},
                                  settings.gemini_price_in, settings.gemini_price_out, keeps_extra_fields=True))
    if settings.groq_api_key:
        providers.append(Provider("groq", client(settings.groq_url, settings.groq_api_key), settings.groq_model, common,
                                  settings.groq_price_in, settings.groq_price_out))
    if settings.ollama_url:
        providers.append(Provider("ollama", client(settings.ollama_url), settings.ollama_model, common))
    return providers


app = FastAPI(title="Hisaab Assistant", version="0.2.0", lifespan=lifespan)
app.add_middleware(
    CORSMiddleware,
    allow_origins=settings.cors_origins,
    allow_methods=["GET", "POST", "DELETE"],
    allow_headers=["Authorization", "Content-Type"],
)


def current_user(authorization: str | None = Header(default=None)) -> CurrentUser:
    try:
        return verify(bearer_token(authorization), settings.jwt_key, settings.jwt_issuer, settings.jwt_audience)
    except AuthError as exc:
        raise HTTPException(status_code=401, detail=str(exc)) from exc


class AskRequest(BaseModel):
    question: str = Field(min_length=1, max_length=1000)


@app.post("/chat/sessions")
def create_session(request: Request, user: CurrentUser = Depends(current_user)) -> dict:
    return request.app.state.store.create_session(user.id, user.username)


@app.get("/chat/sessions")
def list_sessions(request: Request, user: CurrentUser = Depends(current_user)) -> list[dict]:
    return request.app.state.store.list_sessions(user.id)


@app.get("/chat/sessions/{session_id}/messages")
def session_messages(session_id: str, request: Request, user: CurrentUser = Depends(current_user)) -> list[dict]:
    store: Store = request.app.state.store
    if not store.owns_session(session_id, user.id):
        raise HTTPException(status_code=404, detail="Conversation not found.")
    return store.messages(session_id)


@app.delete("/chat/sessions/{session_id}", status_code=204)
def delete_session(session_id: str, request: Request, user: CurrentUser = Depends(current_user)) -> None:
    if not request.app.state.store.delete_session(session_id, user.id):
        raise HTTPException(status_code=404, detail="Conversation not found.")


@app.post("/chat/sessions/{session_id}/ask")
async def ask(session_id: str, body: AskRequest, request: Request,
        user: CurrentUser = Depends(current_user)) -> StreamingResponse:
    """Answer as server-sent events: tool, token, data, then done or error."""
    question = body.question.strip()
    if not question:
        raise HTTPException(status_code=422, detail="Ask a question.")
    try:
        stream = request.app.state.chat.start(user, session_id, question)
    except ChatError as exc:
        raise HTTPException(status_code=exc.status, detail=exc.message) from exc
    return StreamingResponse(stream, media_type="text/event-stream",
                             headers={"Cache-Control": "no-cache", "X-Accel-Buffering": "no"})


class FeedbackRequest(BaseModel):
    rating: Literal[1, -1]
    comment: str | None = Field(default=None, max_length=1000)


@app.post("/messages/{message_id}/feedback")
def feedback(message_id: str, body: FeedbackRequest, request: Request,
             user: CurrentUser = Depends(current_user)) -> dict:
    """Thumbs up (1) or down (-1) on one of my answers; posting again replaces it."""
    store: Store = request.app.state.store
    if store.answer_owner(message_id) != user.id:
        raise HTTPException(status_code=404, detail="Answer not found.")
    comment = body.comment.strip() if body.comment and body.comment.strip() else None
    return store.set_feedback(message_id, user.id, body.rating, comment)


@app.get("/admin/stats")
def admin_stats(request: Request, days: int = 7, user: CurrentUser = Depends(current_user)) -> dict:
    """Usage and quality for the Owner and Admin: questions, failures, speed, tools, thumbs down."""
    if user.role not in ADMIN_ROLES:
        raise HTTPException(status_code=403, detail="Only the Owner and Admin can see assistant usage.")
    if not 1 <= days <= 180:
        raise HTTPException(status_code=422, detail="days must be between 1 and 180.")
    return build_stats(request.app.state.store, days, datetime.now(UTC))


class ReportRequest(BaseModel):
    type: str = Field(description="sales, receivables, stock or production")
    format: str = Field(default="xlsx", description="xlsx or pptx")
    period: str | None = None
    from_date: str | None = Field(default=None, alias="from")
    to_date: str | None = Field(default=None, alias="to")


@app.post("/reports", status_code=201)
async def create_report(body: ReportRequest, request: Request, user: CurrentUser = Depends(current_user)) -> dict:
    """Make a report directly (a button in the app), without asking the model."""
    erp = ErpClient(request.app.state.erp_http, user.token, request.app.state.cache)
    try:
        report = await request.app.state.reports.create(user, erp, body.type, body.format, period=body.period,
                                                        from_date=body.from_date, to_date=body.to_date)
    except ReportError as exc:
        raise HTTPException(status_code=exc.status, detail=exc.message) from exc
    return report_out(report)


@app.get("/reports")
def list_reports(request: Request, user: CurrentUser = Depends(current_user)) -> list[dict]:
    return [report_out(r) for r in request.app.state.reports.list(user)]


@app.get("/reports/{report_id}/download")
def download_report(report_id: str, request: Request, user: CurrentUser = Depends(current_user)) -> FileResponse:
    try:
        path, file_name = request.app.state.reports.file_for(user, report_id)
    except ReportError as exc:
        raise HTTPException(status_code=exc.status, detail=exc.message) from exc
    media = {"xlsx": "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
             "pptx": "application/vnd.openxmlformats-officedocument.presentationml.presentation"}
    return FileResponse(path, media_type=media[path.suffix.lstrip(".")], filename=file_name)



@app.get("/health")
async def health(request: Request) -> dict:
    llm: Llm = request.app.state.llm
    status: dict = {"agent": "ok", "model": llm.model, "queued": llm.queued, "llm": await llm.reachable()}
    try:
        response = await request.app.state.erp_http.get("warehouses", timeout=5)
        status["erp"] = "ok" if response.status_code in (200, 401) else f"HTTP {response.status_code}"
    except httpx.HTTPError:
        status["erp"] = "unreachable"
    status["db"] = "ok" if request.app.state.store.ping() else "error"
    return status
