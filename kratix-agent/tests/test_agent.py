import asyncio
import json
from datetime import date

import httpx

from app.agent import FALLBACK_ANSWER, NO_TOOL_NUDGE, Agent, system_prompt
from app.llm import Llm
from tests.test_tools import FakeErp


def _ndjson(reply: dict) -> bytes:
    """Ollama's streamed reply: tool calls in one chunk, text a few characters at a time, then done."""
    chunks = []
    if reply.get("tool_calls"):
        chunks.append({"message": {"role": "assistant", "content": "", "tool_calls": reply["tool_calls"]}, "done": False})
    text = reply.get("content") or ""
    chunks += [{"message": {"role": "assistant", "content": text[i:i + 4]}, "done": False} for i in range(0, len(text), 4)]
    chunks.append({"message": {"role": "assistant", "content": ""}, "done": True})
    return "".join(json.dumps(c) + "\n" for c in chunks).encode()


def scripted_llm(replies: list[dict]) -> tuple[Llm, list[dict]]:
    """An Ollama stand-in that streams the given assistant messages in order and records each request."""
    requests: list[dict] = []

    def handler(request: httpx.Request) -> httpx.Response:
        requests.append(json.loads(request.content))
        return httpx.Response(200, content=_ndjson(replies[len(requests) - 1]))

    http = httpx.AsyncClient(transport=httpx.MockTransport(handler), base_url="http://ollama")
    return Llm(http, "qwen2.5:7b", keep_alive="30m", parallel=1, queue_cap=10, options={"temperature": 0.1}), requests


def tool_call(name: str, arguments: dict | None = None) -> dict:
    return {"role": "assistant", "content": "", "tool_calls": [{"function": {"name": name, "arguments": arguments or {}}}]}


ERP = {"dashboard/metrics": {"wheatStockKg": 1234.567, "outstandingReceivables": 0.0}}


async def collect(agent: Agent, question: str, erp=None) -> list:
    return [e async for e in agent.run(question, [], erp or FakeErp(ERP), "Owner")]


def test_prompt_contains_today_and_role():
    prompt = system_prompt("Sales", date(2026, 10, 4))
    assert "2026-10-04" in prompt and "Sales" in prompt


def test_tool_call_then_streamed_answer():
    llm, requests = scripted_llm([tool_call("business_overview"), {"role": "assistant", "content": "Wheat stock is 1,234.57 kg."}])
    events = asyncio.run(collect(Agent(llm), "How much wheat?"))

    kinds = [e.kind for e in events]
    assert kinds[0] == "tool" and kinds[-1] == "result" and kinds.count("token") > 1
    assert "".join(e.payload for e in events if e.kind == "token") == "Wheat stock is 1,234.57 kg."
    result = events[-1].payload
    assert result.answer == "Wheat stock is 1,234.57 kg." and [c.name for c in result.tool_calls] == ["business_overview"]
    tool_message = requests[1]["messages"][-1]
    assert tool_message["role"] == "tool" and "1234.57" in tool_message["content"]
    # Same prompt prefix on every call: tools and keep_alive are sent each time, streaming on.
    assert all(r["tools"] and r["keep_alive"] == "30m" and r["stream"] for r in requests)


def test_tool_call_written_as_plain_json_is_accepted_and_not_shown():
    llm, _ = scripted_llm([{"role": "assistant", "content": '{"name": "business_overview", "arguments": {}}'},
                           {"role": "assistant", "content": "Done."}])
    events = asyncio.run(collect(Agent(llm), "Overview"))
    assert "".join(e.payload for e in events if e.kind == "token") == "Done."
    assert [c.name for c in events[-1].payload.tool_calls] == ["business_overview"]


def test_text_that_starts_with_a_brace_but_is_not_a_tool_call_is_still_shown():
    llm, _ = scripted_llm([{"role": "assistant", "content": "{not json} but an answer"}])
    events = asyncio.run(collect(Agent(llm), "Hi"))
    assert "".join(e.payload for e in events if e.kind == "token") == "{not json} but an answer"


def test_tables_go_to_the_app_as_data_events():
    erp = FakeErp({"customers": [{"customerName": f"C{n}", "type": "Retail", "contactPerson": "", "phoneNumber": "",
                                  "address": "", "creditLimit": 0.0, "outstandingBalance": 0.0, "isActive": True}
                                 for n in range(12)]})
    llm, requests = scripted_llm([tool_call("customer_details"), {"role": "assistant", "content": "12 customers."}])
    events = asyncio.run(collect(Agent(llm), "List customers", erp))
    data = [e.payload for e in events if e.kind == "data"]
    assert len(data) == 1 and data[0]["count"] == 12 and len(data[0]["rows"]) == 12
    assert len(json.loads(requests[1]["messages"][-1]["content"])["customers"]) == 10


def test_several_tool_calls_in_one_round_all_run():
    both = {"role": "assistant", "content": "", "tool_calls": [
        {"function": {"name": "business_overview", "arguments": {}}},
        {"function": {"name": "business_overview", "arguments": {}}}]}
    llm, requests = scripted_llm([both, {"role": "assistant", "content": "Ok."}])
    events = asyncio.run(collect(Agent(llm), "Overview twice"))
    assert len(events[-1].payload.tool_calls) == 2
    assert [m["role"] for m in requests[1]["messages"][-2:]] == ["tool", "tool"]


def test_figures_without_a_tool_call_are_held_back_and_the_model_is_told_to_use_a_tool():
    llm, requests = scripted_llm([{"role": "assistant", "content": "Wheat stock is 5,000 kg."},
                                  tool_call("business_overview"),
                                  {"role": "assistant", "content": "Wheat stock is 1,234.57 kg."}])
    events = asyncio.run(collect(Agent(llm), "How much wheat?"))
    assert "".join(e.payload for e in events if e.kind == "token") == "Wheat stock is 1,234.57 kg."
    assert requests[1]["messages"][-1]["content"] == NO_TOOL_NUDGE


def test_figures_without_a_tool_call_twice_give_the_fallback_answer():
    invented = {"role": "assistant", "content": "Sales were PKR 25,000,000."}
    llm, _ = scripted_llm([invented, invented])
    events = asyncio.run(collect(Agent(llm), "Sales?"))
    assert "".join(e.payload for e in events if e.kind == "token") == FALLBACK_ANSWER


def test_a_greeting_needs_no_tool():
    llm, requests = scripted_llm([{"role": "assistant", "content": "Hello! Ask me about sales, stock or production."}])
    events = asyncio.run(collect(Agent(llm), "Hi"))
    assert events[-1].payload.answer.startswith("Hello") and len(requests) == 1


def test_stops_calling_tools_after_the_round_limit():
    llm, requests = scripted_llm([tool_call("business_overview")] * 4 + [{"role": "assistant", "content": "Final."}])
    result = asyncio.run(Agent(llm).ask("Overview", [], FakeErp(ERP), "Owner"))
    assert result.answer == "Final." and len(result.tool_calls) == 4
    assert requests[-1]["messages"][-1]["content"].startswith("Answer now")


def test_tool_call_in_the_last_round_gives_the_fallback_answer():
    llm, _ = scripted_llm([tool_call("business_overview")] * 5)
    result = asyncio.run(Agent(llm).ask("Overview", [], FakeErp(ERP), "Owner"))
    assert result.answer == FALLBACK_ANSWER and len(result.tool_calls) == 4


def test_invented_names_are_dropped_but_mentioned_ones_kept():
    from app.agent import mentioned_filters
    args = {"customer": "Khwab Foods", "warehouse": "main", "product": "wheat", "period": "this_month"}
    assert mentioned_filters(args, "Kis customer ne paise dene hain? Kitni gandum?") == {"product": "wheat", "period": "this_month"}
    assert mentioned_filters({"customer": "Lahore Bakery Group"}, "and lahore?") == {"customer": "Lahore Bakery Group"}
    assert mentioned_filters({"warehouse": "Raw Material Warehouse"}, "wheat in the warehouse?") == {}
    assert mentioned_filters({"warehouse": "Godown A"}, "stock in godown a") == {}  # "a" is too short to count
    assert mentioned_filters({"warehouse": "Finished Goods Warehouse"}, "atta in finished goods") == {}


def test_unknown_status_is_ignored():
    from app.tools import _status
    assert _status("any", ["Dispatched"]) is None and _status("partially dispatched", ["PartiallyDispatched"]) == "PartiallyDispatched"


def test_two_part_questions_are_split_and_short_lists_are_not():
    from app.agent import question_parts
    assert question_parts("Can you tell me what are the expected deliveries today and kis customer ne paise dene hain") == [
        "Can you tell me what are the expected deliveries today", "kis customer ne paise dene hain"]
    assert question_parts("What were sales of atta and maida this month?") == ["What were sales of atta and maida this month?"]
    assert question_parts("How much wheat do we have?") == ["How much wheat do we have?"]


def test_each_part_is_answered_in_one_stream():
    llm, requests = scripted_llm([tool_call("business_overview"), {"role": "assistant", "content": "No deliveries due."},
                                  tool_call("business_overview"), {"role": "assistant", "content": "Nobody owes money."}])
    events = asyncio.run(collect(Agent(llm), "Which deliveries are expected today and which customers owe us money"))
    result = events[-1].payload
    assert result.answer == "No deliveries due.\n\nNobody owes money." and len(result.tool_calls) == 2
    assert "".join(e.payload for e in events if e.kind == "token") == result.answer


def test_off_topic_questions_drop_the_history_but_follow_ups_keep_it():
    from app.agent import needs_history
    assert not needs_history("can you write me a python code") and not needs_history("Tell me a joke")
    assert needs_history("And last month?") and needs_history("aur pichle hafte?") and needs_history("Stock of atta?")


def test_off_topic_question_is_not_forced_to_use_a_tool():
    llm, requests = scripted_llm([{"role": "assistant", "content": "I only answer questions about the mill's ERP data."}])
    events = asyncio.run(collect(Agent(llm), "can you write me a python code"))
    assert events[-1].payload.answer.startswith("I only answer") and len(requests) == 1


def test_business_words_are_recognised():
    from app.agent import BUSINESS
    for question in ["Kis customer ne paise dene hain?", "expected deliveries today", "Is hafte kitna atta bana?",
                     "status of SO-2026-002", "Mujhe batao is mahine kitni sale hui?"]:
        assert BUSINESS.search(question), question
    for question in ["can you write me a python code", "Hello", "What can you do?", "Tell me a joke"]:
        assert not BUSINESS.search(question), question


def test_business_question_answered_without_a_tool_gets_one_nudge():
    llm, requests = scripted_llm([{"role": "assistant", "content": "Kisi customer ka payment baqi nahi hai."},
                                  tool_call("business_overview"), {"role": "assistant", "content": "Koi baqi nahi."}])
    events = asyncio.run(collect(Agent(llm), "Kis customer ne paise dene hain?"))
    assert [c.name for c in events[-1].payload.tool_calls] == ["business_overview"]
    assert requests[1]["messages"][-1]["content"] == NO_TOOL_NUDGE
