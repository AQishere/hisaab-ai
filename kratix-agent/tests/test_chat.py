"""B1: token check, SQLite store, limits, and a whole question through the streamed chat API."""

import asyncio
import json
import time

import httpx
import jwt
import pytest
from fastapi.testclient import TestClient

from app import main
from app.agent import Agent
from app.auth import NAME_CLAIM, NAME_ID_CLAIM, ROLE_CLAIM, AuthError, verify
from app.chat import ChatError, ChatService
from app.config import settings
from app.erp_client import ErpClient, ResponseCache
from app.limits import LimitError, UserLimits
from app.store import MIGRATIONS, LogEntry, Store
from tests.test_agent import answer, provider, scripted_llm, tool_call

KEY = "test-key-that-is-at-least-32-characters-long"


def token(user_id=1, username="owner", role="Owner", key=KEY, issuer="Kratix.ERP", audience="kratix-ui", expires_in=3600):
    return jwt.encode({NAME_ID_CLAIM: str(user_id), NAME_CLAIM: username, ROLE_CLAIM: role, "iss": issuer,
                       "aud": audience, "exp": int(time.time()) + expires_in}, key, algorithm="HS256")


def check(t):
    return verify(t, KEY, "Kratix.ERP", "kratix-ui")


# ---------------------------------------------------------------- auth

def test_valid_token_gives_the_user():
    user = check(token(7, "sana", "Sales"))
    assert (user.id, user.username, user.role) == (7, "sana", "Sales")


@pytest.mark.parametrize("bad", [
    token(key="another-key-that-is-at-least-32-characters"),
    token(issuer="Someone.Else"),
    token(audience="other-app"),
    token(expires_in=-120),          # past the one minute of leeway
    "not-a-jwt",
])
def test_bad_tokens_are_refused(bad):
    with pytest.raises(AuthError):
        check(bad)


def test_missing_key_is_refused():
    with pytest.raises(AuthError, match="AGENT_JWT_KEY"):
        verify(token(), "", "Kratix.ERP", "kratix-ui")


# ---------------------------------------------------------------- store

def entry(**fields):
    return LogEntry(**{"user_id": 1, "username": "owner", "role": "Owner", "question": "q", "model": "m",
                       "tool_calls": [], "total_ms": 5, "status": "ok", **fields})


def test_store_migrates_once_and_keeps_data(tmp_path):
    store = Store(tmp_path / "agent.db")
    session = store.create_session(1, "owner")
    store.close()
    store = Store(tmp_path / "agent.db")
    assert store.schema_version == len(MIGRATIONS) and store.owns_session(session["id"], 1)


def test_sessions_belong_to_their_user():
    store = Store(":memory:")
    session = store.create_session(1, "owner")
    assert store.owns_session(session["id"], 1) and not store.owns_session(session["id"], 2)
    assert not store.delete_session(session["id"], 2)
    assert store.delete_session(session["id"], 1)
    assert store.list_sessions(1) == [] and not store.owns_session(session["id"], 1)


def test_answer_saves_messages_title_and_log_together():
    store = Store(":memory:")
    session = store.create_session(1, "owner")
    message_id = store.save_answer(session["id"], "Who owes us money?" + "x" * 80, "Ali Sons owes PKR 5,000.",
                                   entry(session_id=session["id"]))
    second = store.save_answer(session["id"], "And Lahore?", "Nothing.", entry(session_id=session["id"]))
    messages = store.messages(session["id"])
    assert [m["role"] for m in messages] == ["user", "assistant", "user", "assistant"]
    assert messages[1]["id"] == message_id and messages[3]["id"] == second
    assert store.list_sessions(1)[0]["title"] == ("Who owes us money?" + "x" * 80)[:60]
    assert [m["content"] for m in store.history(session["id"], 2)] == ["And Lahore?", "Nothing."]
    assert [r["message_id"] for r in store.log_rows()] == [message_id, second]


# ---------------------------------------------------------------- limits

def test_one_question_at_a_time_per_user():
    limits = UserLimits(20)
    limits.acquire(1)
    with pytest.raises(LimitError) as exc:
        limits.acquire(1)
    assert exc.value.status == 429 and "last question" in exc.value.message
    limits.acquire(2)
    limits.release(1)
    limits.acquire(1)


def test_questions_per_minute():
    now = [0.0]
    limits = UserLimits(2, clock=lambda: now[0])
    for _ in range(2):
        limits.acquire(1)
        limits.release(1)
    with pytest.raises(LimitError, match="last minute"):
        limits.acquire(1)
    now[0] = 61.0
    limits.acquire(1)


# ---------------------------------------------------------------- ERP cache

def test_erp_responses_are_reused_per_token_and_expire():
    calls = []

    def handler(request):
        calls.append((request.headers["authorization"], str(request.url)))
        return httpx.Response(200, json=[1])

    now = [0.0]
    cache = ResponseCache(30, clock=lambda: now[0])
    http = httpx.AsyncClient(transport=httpx.MockTransport(handler), base_url="http://erp/api/")

    async def scenario():
        a, b = ErpClient(http, "token-a", cache), ErpClient(http, "token-b", cache)
        await a.get("customers")
        await a.get("customers")
        await b.get("customers")
        now[0] = 31.0
        await a.get("customers")

    asyncio.run(scenario())
    assert [c[0] for c in calls] == ["Bearer token-a", "Bearer token-b", "Bearer token-a"]


# ---------------------------------------------------------------- whole question

def erp_http(responses: dict, status: int = 200) -> httpx.AsyncClient:
    def handler(request):
        path = request.url.path.removeprefix("/api/")
        if status != 200:
            return httpx.Response(status)
        return httpx.Response(200, json=responses[path])

    return httpx.AsyncClient(transport=httpx.MockTransport(handler), base_url="http://erp/api/")


ERP_DATA = {"dashboard/metrics": {"wheatStockKg": 0.0, "outstandingReceivables": 311000.0}}


def service(replies, status=200, store=None):
    llm, requests = scripted_llm(replies)
    store = store or Store(":memory:")
    chat = ChatService(store, Agent(llm), llm, UserLimits(20), erp_http(ERP_DATA, status), ResponseCache(30))
    return chat, store, requests


def events(stream) -> list[tuple[str, dict]]:
    async def read():
        return [chunk async for chunk in stream]

    out = []
    for chunk in asyncio.run(read()):
        lines = chunk.strip().split("\n")
        out.append((lines[0].removeprefix("event: "), json.loads(lines[1].removeprefix("data: "))))
    return out


def test_question_streams_and_is_logged():
    chat, store, _ = service([tool_call("business_overview"), {"role": "assistant", "content": "Receivables are PKR 311,000."}])
    user = check(token())
    session = store.create_session(user.id, user.username)
    got = events(chat.start(user, session["id"], "Who owes us?"))

    kinds = [k for k, _ in got]
    assert kinds[0] == "tool" and "token" in kinds and kinds[-1] == "done"
    assert "".join(d["text"] for k, d in got if k == "token") == "Receivables are PKR 311,000."
    done = got[-1][1]
    assert done["firstTokenMs"] is not None and done["toolCalls"][0]["name"] == "business_overview"

    log = store.log_rows()[0]
    assert (log["status"], log["message_id"], log["user_id"], log["role"]) == ("ok", done["messageId"], 1, "Owner")
    assert json.loads(log["tool_calls"])[0]["name"] == "business_overview"
    assert log["first_token_ms"] is not None and log["llm_ms"] is not None and log["erp_ms"] is not None
    assert [m["content"] for m in store.messages(session["id"])] == ["Who owes us?", "Receivables are PKR 311,000."]


def test_history_is_sent_to_the_model():
    chat, store, requests = service([{"role": "assistant", "content": "First."}, {"role": "assistant", "content": "Second."}])
    user = check(token())
    session = store.create_session(user.id, user.username)
    events(chat.start(user, session["id"], "Hi"))
    events(chat.start(user, session["id"], "And last month?"))
    # Earlier turns arrive as a note inside the one user message, not as chat messages the model would imitate.
    sent = requests[1]["messages"]
    assert [m["role"] for m in sent] == ["system", "user"]
    assert "User: Hi\nAssistant: First.\n\nCurrent question (reply in its language): And last month?" in sent[1]["content"]
    assert requests[0]["messages"][1]["content"] == "Hi"


def test_other_users_session_is_not_found():
    chat, store, _ = service([])
    session = store.create_session(1, "owner")
    with pytest.raises(ChatError) as exc:
        chat.start(check(token(2, "sana", "Sales")), session["id"], "Hi")
    assert exc.value.status == 404


def test_second_question_while_answering_gets_429_and_is_logged_busy():
    chat, store, _ = service([{"role": "assistant", "content": "Ok."}])
    user = check(token())
    session = store.create_session(user.id, user.username)
    first = chat.start(user, session["id"], "Hello")
    with pytest.raises(ChatError) as exc:
        chat.start(user, session["id"], "Two")
    assert exc.value.status == 429
    events(first)
    assert [r["status"] for r in store.log_rows()] == ["busy", "ok"]


def test_role_denied_is_answered_and_logged_as_denied():
    chat, store, _ = service([tool_call("business_overview"), {"role": "assistant", "content": "Your role cannot see that."}],
                             status=403)
    user = check(token(3, "worker", "Worker"))
    session = store.create_session(user.id, user.username)
    got = events(chat.start(user, session["id"], "Sales today?"))
    assert got[-1][0] == "done" and store.log_rows()[0]["status"] == "denied"


def test_expired_erp_session_ends_with_an_error_event():
    chat, store, _ = service([tool_call("business_overview")], status=401)
    user = check(token())
    session = store.create_session(user.id, user.username)
    got = events(chat.start(user, session["id"], "Overview"))
    assert got[-1] == ("error", {"message": "Sign in again."})
    assert store.log_rows()[0]["status"] == "error" and store.messages(session["id"]) == []


def test_rate_limited_api_ends_with_busy_message():
    from app.llm import Llm
    llm = Llm([provider("gemini", lambda r: httpx.Response(429))], parallel=1, queue_cap=10)
    store = Store(":memory:")
    chat = ChatService(store, Agent(llm), llm, UserLimits(20), erp_http(ERP_DATA), ResponseCache(30))
    user = check(token())
    session = store.create_session(user.id, user.username)
    got = events(chat.start(user, session["id"], "Hi"))
    assert got[-1] == ("error", {"message": "The assistant is busy, try again in a minute."})
    assert store.log_rows()[0]["status"] == "busy"


def test_model_unreachable_ends_with_offline_message():
    def handler(request):
        raise httpx.ConnectError("refused")

    from app.llm import Llm
    llm = Llm([provider("gemini", handler)], parallel=1, queue_cap=10)
    store = Store(":memory:")
    chat = ChatService(store, Agent(llm), llm, UserLimits(20), erp_http(ERP_DATA), ResponseCache(30))
    user = check(token())
    session = store.create_session(user.id, user.username)
    got = events(chat.start(user, session["id"], "Hi"))
    assert got[-1][0] == "error" and "offline" in got[-1][1]["message"]
    # The limit is released, so the next question is accepted.
    chat.start(user, session["id"], "Again")


# ---------------------------------------------------------------- routes

@pytest.fixture
def client(tmp_path, monkeypatch):
    monkeypatch.setattr(settings, "jwt_key", KEY)
    monkeypatch.setattr(settings, "data_dir", tmp_path)
    monkeypatch.setattr(settings, "gemini_api_key", "test")  # the model is faked below
    with TestClient(main.app) as test_client:
        chat, store, _ = service([{"role": "assistant", "content": "Hello."}], store=main.app.state.store)
        main.app.state.chat = chat
        yield test_client


def test_routes_need_a_valid_token(client):
    assert client.get("/chat/sessions").status_code == 401
    assert client.get("/chat/sessions", headers={"Authorization": "Bearer junk"}).status_code == 401


def test_session_routes(client):
    owner = {"Authorization": f"Bearer {token()}"}
    other = {"Authorization": f"Bearer {token(2, 'sana', 'Sales')}"}
    session = client.post("/chat/sessions", headers=owner).json()

    response = client.post(f"/chat/sessions/{session['id']}/ask", json={"question": "Hi"}, headers=owner)
    assert response.status_code == 200 and response.headers["content-type"].startswith("text/event-stream")
    assert "event: done" in response.text

    assert client.get("/chat/sessions", headers=owner).json()[0]["title"] == "Hi"
    assert len(client.get(f"/chat/sessions/{session['id']}/messages", headers=owner).json()) == 2
    assert client.get(f"/chat/sessions/{session['id']}/messages", headers=other).status_code == 404
    assert client.post(f"/chat/sessions/{session['id']}/ask", json={"question": "Hi"}, headers=other).status_code == 404
    assert client.delete(f"/chat/sessions/{session['id']}", headers=other).status_code == 404
    assert client.delete(f"/chat/sessions/{session['id']}", headers=owner).status_code == 204
    assert client.get("/chat/sessions", headers=owner).json() == []


def test_log_records_provider_fallback_tokens_and_cost():
    from app.llm import Llm
    llm = Llm([provider("gemini", lambda r: httpx.Response(503)),
               provider("groq", lambda r: answer("Hello.", {"prompt_tokens": 200, "completion_tokens": 50}))],
              parallel=1, queue_cap=10)
    store = Store(":memory:")
    chat = ChatService(store, Agent(llm), llm, UserLimits(20), erp_http(ERP_DATA), ResponseCache(30))
    user = check(token())
    session = store.create_session(user.id, user.username)
    assert events(chat.start(user, session["id"], "Hi"))[-1][0] == "done"
    row = store.log_rows()[0]
    assert (row["provider"], row["fallbacks"], row["prompt_tokens"], row["completion_tokens"]) == ("groq", 1, 200, 50)
    assert row["cost_usd"] == 0.0003
