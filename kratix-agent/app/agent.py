import asyncio
import json
import re
import time
from collections.abc import AsyncIterator
from dataclasses import dataclass, field
from datetime import date
from typing import Any

from .config import settings
from .erp_client import ErpClient
from .llm import Llm
from .tools import TOOL_SCHEMAS, TOOLS, ToolContext, ToolOutput, redirect_file_request, run_tool

FALLBACK_ANSWER = "Sorry, I could not produce an answer. Please rephrase the question."
# Sent when the model answers with figures without calling a tool: with a few answers in the conversation history,
# qwen2.5:7b copied their pattern and invented figures (5,000 kg wheat, PKR 25,000,000, "Supplier XYZ").
# A question about the business answered without a tool gets one nudge. Only these: "can you write me a python
# code" was nudged too, and the model then looked up the overview and repeated its previous answer (2026-10-05).
BUSINESS = re.compile(
    r"\b(sale|sales|sold|stock|inventory|wheat|gandum|atta|aata|flour|maida|suji|bran|chokar|bag|bags|kg|customer|"
    r"customers|supplier|suppliers|paise|paisa|payment|payments|paid|owe|owes|baqi|udhaar|invoice|invoices|receivable|"
    r"receivables|order|orders|po|so|purchase|delivery|deliveries|dispatch|dispatched|production|produced|milling|yield|"
    r"batch|batches|waste|quality|moisture|report|excel|powerpoint|slides|warehouse|godown|revenue|profit|kitni|kitna|"
    r"kitne|bana|bani|hui|hua|maal|lot)\b|\b(po|so|inv|ge|mb|grn)-\d", re.IGNORECASE)
# Filters naming a party or place. The model invented some ("Khwab Foods", a "main" warehouse) and then reported
# "nothing found"; a name is kept only if the conversation mentions it. Products are not checked: the model rightly
# turns "gandum" into wheat.
# Words that mark a follow-up ("And last month?"), which needs the earlier messages although it names no business word.
FOLLOW_UP = re.compile(
    r"^\W*(and|aur|or|also|what about|how about|same|uska|unka|iska|their|them|those|that|it)\b|"
    r"\b(today|yesterday|week|month|year|aaj|kal|hafte|mahine|saal|last|previous|pichle)\b", re.IGNORECASE)
PART_SPLIT = re.compile(r"\s+(?:and|aur|also|plus)\s+|[?;]\s+", re.IGNORECASE)
MIN_PART_WORDS = 4


def question_parts(question: str) -> list[str]:
    """Split "expected deliveries today and kis customer ne paise dene hain" into its questions. qwen2.5:7b often
    looked up only one part and guessed the other. Split only when every part is a business question of at least
    four words, so "sales of atta and maida this month" stays one question."""
    parts = [p.strip(" ?.,") for p in PART_SPLIT.split(question) if p.strip(" ?.,")]
    if 1 < len(parts) <= 3 and all(len(p.split()) >= MIN_PART_WORDS and BUSINESS.search(p) for p in parts):
        return parts
    return [question]


def needs_history(question: str) -> bool:
    """Off-topic questions ("can you write me a python code") go to the model without the conversation: with it, the
    model looked something up and repeated its previous answer."""
    return bool(BUSINESS.search(question) or FOLLOW_UP.search(question))


NAME_FILTERS = ("customer", "supplier", "warehouse")
# Words in names that say nothing on their own: "Raw Material Warehouse" must not pass because the user said "warehouse".
GENERIC_WORDS = {"warehouse", "godown", "store", "main", "raw", "material", "finished", "goods", "the", "and", "foods",
                 "traders", "trading", "mart", "retail", "group", "company", "ltd", "sons", "supplies", "wholesale"}


def mentioned_filters(args: dict[str, Any], user_text: str) -> dict[str, Any]:
    """Drop name filters the user never typed. Checked against the user's own words only: the assistant's earlier
    answers mention warehouse and customer names the model then reused as filters."""
    text = user_text.lower()
    kept = {}
    for key, value in args.items():
        if key in NAME_FILTERS and isinstance(value, str):
            words = [w for w in re.findall(r"[a-z0-9]+", value.lower()) if len(w) >= 3 and w not in GENERIC_WORDS]
            if not any(w in text for w in words):
                continue
        kept[key] = value
    return kept


NO_TOOL_NUDGE = ("You answered without calling a tool. Never answer from memory or from earlier answers: call the "
                 "right tool now to get current ERP data, then answer.")

TOOL_LABELS = {
    "business_overview": "Checking today's figures",
    "receivables": "Checking receivables",
    "sales_report": "Checking sales",
    "current_stock": "Checking stock",
    "stock_movement_report": "Checking stock movements",
    "production_report": "Checking production",
    "supplier_quality": "Checking supplier quality",
    "purchase_orders": "Checking purchase orders",
    "sales_orders": "Checking sales orders",
    "customer_details": "Checking customers",
    "create_report": "Making the report",
}


@dataclass
class ToolCallRecord:
    name: str
    arguments: dict[str, Any]
    error: str | None = None
    ms: int = 0


@dataclass
class AgentResult:
    answer: str
    tool_calls: list[ToolCallRecord] = field(default_factory=list)
    llm_ms: int = 0
    erp_ms: int = 0
    report: dict[str, Any] | None = None


@dataclass
class Event:
    """One step of an answer: tool (a tool is running), token (answer text), data (a full table for the app),
    report (a report file was made), or result (the finished AgentResult, always last)."""
    kind: str
    payload: Any


def system_prompt(role: str | None, today: date) -> str:
    """The role stays here although it costs speed when users of different roles take turns (Ollama re-reads the
    tool list: first words up to 1.4 s instead of about 0.5 s). Tried 2026-10-05: moving the role next to the
    question made qwen2.5:7b answer English questions in Urdu script, copy the Roman Urdu example without calling a
    tool, and miss SO-2026-002. Revisit with fine-tuning."""
    return f"""You are Kratix Assistant, the business assistant inside the Kratix ERP for {settings.company_name}, a flour mill.
Today is {today:%A, %d %B %Y} ({today.isoformat()}). The user is signed in with the role: {role or "unknown"}.

How to answer:
- Use the tools to get ERP data. Never invent numbers, names, dates or invoice numbers; only state what a tool returned.
- Call a tool for every question about the business, including follow-ups: earlier answers in this conversation may be
  out of date and are never a source of figures.
- If the tools cannot answer the question, say so briefly and suggest what you can answer instead.
- If the question has several parts, call a tool for each part before answering; never answer a part from guesswork.
- If the question is not about the mill's business (for example writing code or general knowledge), call no tool and
  say in one sentence that you only answer questions about the mill's ERP data.
- If a tool returns an error saying the role has no access, tell the user their role cannot see that information.
- For time ranges use the period argument (today, this_week, last_month, ...). Use from_date/to_date only for
  specific dates such as "since 15 September".
- Only pass filters the user actually gave. Put document numbers (PO-..., SO-..., INV-...) in their number field.
- When the user asks for a report as a file (Excel, sheet, PowerPoint, slides, presentation), call create_report;
  never say a file is ready unless create_report returned it.
- Wheat means raw wheat (Raw Material). Wheat bran is a by-product; mention it separately, never as wheat stock.
- Money is in PKR; write amounts like PKR 1,250,000. Weights are kg unless the unit is bag.
- Answer in 1 to 3 short sentences: the direct answer and the 2-3 most important figures. Never list every customer,
  product or invoice; the app shows the full data under your answer. Speed matters: every extra word makes the user wait.
- Reply in the language of the current question: English, Urdu, or Roman Urdu (Urdu in Latin letters, for example
  "Kisi customer ka payment baqi nahi hai")."""


def _tool_call_from_text(content: str) -> dict[str, Any] | None:
    """A model sometimes writes a tool call as plain JSON instead of a structured call; accept that too."""
    match = re.search(r"\{.*\}", content, re.DOTALL)
    if not match:
        return None
    try:
        data = json.loads(match.group(0))
    except ValueError:
        return None
    if isinstance(data, dict) and data.get("name") in TOOLS:
        return {"id": "call_text", "type": "function",
                "function": {"name": data["name"], "arguments": json.dumps(data.get("arguments") or {})}}
    return None


def _arguments(call: dict[str, Any]) -> dict[str, Any]:
    args = call["function"].get("arguments") or {}
    if isinstance(args, str):
        try:
            args = json.loads(args)
        except ValueError:
            args = {}
    return {k: v for k, v in args.items() if v not in (None, "")} if isinstance(args, dict) else {}


# Earlier answers quoted to the model are cut to this many characters.
CONTEXT_ANSWER_CHARS = 200


def with_context(question: str, history: list[dict[str, str]]) -> str:
    """Earlier turns go into the question as a short note, not as separate chat messages. Sent as messages, the past
    answers (which show no tool calls) taught the model to answer follow-ups without calling tools, and it invented
    figures. As a note, every question looks fresh to the model while follow-ups ("and Lahore?") keep their context."""
    if not history:
        return question
    lines = []
    for message in history:
        text = " ".join(message["content"].split())
        if message["role"] == "assistant" and len(text) > CONTEXT_ANSWER_CHARS:
            text = text[:CONTEXT_ANSWER_CHARS] + "..."
        lines.append(f"{'User' if message['role'] == 'user' else 'Assistant'}: {text}")
    return ("Earlier in this conversation (context only; its figures may be out of date, so get current data with a "
            "tool):\n" + "\n".join(lines) + f"\n\nCurrent question (reply in its language): {question}")


def _ms(seconds: float) -> int:
    return round(seconds * 1000)


class Agent:
    def __init__(self, llm: Llm):
        self._llm = llm

    async def _model_turn(self, messages: list[dict[str, Any]], timing: AgentResult,
                          stream: bool) -> AsyncIterator[Event]:
        """One model call; the last event is ('turn', (full text, tool calls)). With stream, answer text is passed
        on as it arrives; without, the caller decides what to do with the text."""
        started = time.perf_counter()
        content, calls, streamed, holding = "", [], False, not stream
        try:
            # The tool list goes with every call, including the one that writes the answer, so the model can still
            # ask for more data instead of guessing.
            async for chunk in self._llm.stream_chat(messages, TOOL_SCHEMAS):
                calls += chunk.get("tool_calls") or []
                text = chunk.get("content") or ""
                if not text:
                    continue
                content += text
                if streamed:
                    yield Event("token", text)
                elif holding or not content.strip():
                    continue
                elif content.lstrip()[0] in "{<":
                    # Possibly a tool call written as text; hold it back until the reply is complete.
                    holding = True
                else:
                    streamed = True
                    yield Event("token", content.lstrip())
        finally:
            timing.llm_ms += _ms(time.perf_counter() - started)

        if not calls and holding:
            parsed = _tool_call_from_text(content)
            if parsed:
                calls = [parsed]
            elif stream:
                yield Event("token", content.strip())
        yield Event("turn", (content.strip(), calls))

    async def _run_tool(self, call: dict[str, Any], erp: ErpClient, context: ToolContext | None,
                        conversation: str) -> tuple[ToolCallRecord, ToolOutput]:
        name, args = redirect_file_request(call["function"]["name"], mentioned_filters(_arguments(call), conversation))
        started = time.perf_counter()
        output = await run_tool(name, args, erp, context)
        error = output.model.get("error") if isinstance(output.model, dict) else None
        return ToolCallRecord(name, args, error, _ms(time.perf_counter() - started)), output

    async def run(self, question: str, history: list[dict[str, str]], erp: ErpClient, role: str | None,
                  today: date | None = None, context: ToolContext | None = None) -> AsyncIterator[Event]:
        """Answer a question, or each part of a two- or three-part question in turn, as one streamed answer."""
        parts = question_parts(question)
        if not needs_history(question):
            history = []
        merged = AgentResult("")
        answers = []
        for index, part in enumerate(parts):
            if index:
                yield Event("token", "\n\n")
            async for event in self._run_one(part, history, erp, role, today, context):
                if event.kind != "result":
                    yield event
                    continue
                one: AgentResult = event.payload
                answers.append(one.answer)
                merged.tool_calls += one.tool_calls
                merged.llm_ms += one.llm_ms
                merged.erp_ms += one.erp_ms
                merged.report = one.report or merged.report
        merged.answer = "\n\n".join(answers)
        yield Event("result", merged)

    async def _run_one(self, question: str, history: list[dict[str, str]], erp: ErpClient, role: str | None,
                       today: date | None, context: ToolContext | None) -> AsyncIterator[Event]:
        messages: list[dict[str, Any]] = [
            {"role": "system", "content": system_prompt(role, today or date.today())},
            {"role": "user", "content": with_context(question, history)},
        ]
        result = AgentResult("")
        conversation = " ".join([question, *(m["content"] for m in history if m["role"] == "user")])
        nudged = False

        for round_number in range(settings.max_tool_rounds + 1):
            final = round_number == settings.max_tool_rounds
            if final:
                # Out of tool rounds: answer with what has been gathered.
                messages.append({"role": "user", "content": "Answer now using only the tool results above."})

            # Until a tool has run, the reply is held back: it is normally a tool call (so nothing is lost), and an
            # answer with figures but no data behind it must never reach the user.
            hold = not result.tool_calls
            text, calls = "", []
            async for event in self._model_turn(messages, result, stream=not hold):
                if event.kind == "turn":
                    text, calls = event.payload
                else:
                    yield event
            if hold and not calls:
                figures = bool(re.search(r"\d", text))
                if not nudged and not final and (figures or BUSINESS.search(question)):
                    nudged = True
                    messages += [{"role": "assistant", "content": text}, {"role": "user", "content": NO_TOOL_NUDGE}]
                    continue
                if figures:
                    text = ""  # still figures without data: the fallback answer below, never the invented figures
                elif text:
                    yield Event("token", text)
            if not calls or final:
                result.answer = text if not calls else ""
                break

            messages.append({"role": "assistant", "content": "", "tool_calls": calls})
            for call in calls:
                name, _ = redirect_file_request(call["function"]["name"], _arguments(call))
                yield Event("tool", {"name": name, "label": TOOL_LABELS.get(name, "Checking the ERP") + "..."})
            # Several tool calls in one round are fetched in parallel.
            started = time.perf_counter()
            outputs = await asyncio.gather(*(self._run_tool(call, erp, context, conversation) for call in calls))
            result.erp_ms += _ms(time.perf_counter() - started)
            for call, (record, output) in zip(calls, outputs):
                result.tool_calls.append(record)
                messages.append({"role": "tool", "tool_call_id": call["id"], "name": record.name,
                                 "content": json.dumps(output.model, default=str)})
                if output.data:
                    yield Event("data", output.data)
                if output.report:
                    result.report = output.report
                    yield Event("report", output.report)

        if not result.answer:
            result.answer = FALLBACK_ANSWER
            yield Event("token", FALLBACK_ANSWER)
        yield Event("result", result)

    async def ask(self, question: str, history: list[dict[str, str]], erp: ErpClient, role: str | None,
                  today: date | None = None, context: ToolContext | None = None) -> AgentResult:
        """The whole answer at once (for /ask and tests)."""
        async for event in self.run(question, history, erp, role, today, context):
            if event.kind == "result":
                return event.payload
        raise RuntimeError("The agent finished without a result.")
