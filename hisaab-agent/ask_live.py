"""Ask the running agent questions as a demo user, streamed, and print the answers with timings. Read-only.

Usage (agent on 5080 and ERP on 5069 must be running):
    .venv/Scripts/python ask_live.py                       # the MVP questions as Owner, in one conversation
    .venv/Scripts/python ask_live.py --role Sales "Who owes us money?"

Demo passwords are read from the git-ignored appsettings.Development.json, never printed.
"""

import argparse
import json
import sys
import time
from pathlib import Path

import httpx

SETTINGS = Path(__file__).resolve().parent.parent / "CrescentFlourMill" / "Kratix.ERP" / "appsettings.Development.json"

MVP_QUESTIONS = [
    "Which customers owe me money?",
    "What's my current stock?",
    "How much wheat do we have?",
    "Give me this month's sales summary.",
    "Which supplier has the best quality wheat?",
    "What was our milling yield this month?",
    "Has PO-2026-001 been fully received?",
    "Mujhe batao is mahine kitni sale hui?",
]

# First words must be on screen within this time (DESIGN.md section 8).
TARGET_MS = 500


def login(erp_url: str, role: str) -> str:
    users = json.loads(SETTINGS.read_text(encoding="utf-8-sig"))["DemoUsers"]
    user = next(u for u in users if u["Role"].lower() == role.lower())
    response = httpx.post(f"{erp_url}/auth/login", json={"username": user["Username"], "password": user["Password"]})
    response.raise_for_status()
    return response.json()["token"]


def ask(client: httpx.Client, session_id: str, question: str) -> dict:
    """Stream one answer; returns the text, tools, tables, error and client-side timings."""
    started = time.perf_counter()
    out = {"text": "", "tools": [], "tables": [], "error": None, "first_ms": None, "done": None}
    with client.stream("POST", f"/chat/sessions/{session_id}/ask", json={"question": question}) as response:
        if response.status_code != 200:
            out["error"] = f"HTTP {response.status_code}: {response.read().decode()}"
            return out
        event = None
        for line in response.iter_lines():
            if line.startswith("event: "):
                event = line[7:]
            elif line.startswith("data: "):
                data = json.loads(line[6:])
                if event == "token":
                    if out["first_ms"] is None:
                        out["first_ms"] = round((time.perf_counter() - started) * 1000)
                    out["text"] += data["text"]
                elif event == "data":
                    out["tables"].append(f"{data['title']} ({data['count']} rows)")
                elif event == "report":
                    out["tables"].append(f"report {data['fileName']} ({data['sizeBytes']:,} bytes) {data['downloadUrl']}")
                elif event == "done":
                    out["done"] = data
                elif event == "error":
                    out["error"] = data["message"]
    out["total_ms"] = round((time.perf_counter() - started) * 1000)
    return out


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("questions", nargs="*")
    parser.add_argument("--role", default="Owner")
    parser.add_argument("--agent", default="http://127.0.0.1:5080")
    parser.add_argument("--erp", default="http://127.0.0.1:5069/api")
    parser.add_argument("--new-session-each", action="store_true", help="ask every question in a fresh conversation")
    args = parser.parse_args()
    sys.stdout.reconfigure(encoding="utf-8")  # answers in Urdu script

    token = login(args.erp, args.role)
    client = httpx.Client(base_url=args.agent, headers={"Authorization": f"Bearer {token}"}, timeout=300)
    session_id = None
    firsts = []
    for question in args.questions or MVP_QUESTIONS:
        if session_id is None or args.new_session_each:
            session_id = client.post("/chat/sessions").raise_for_status().json()["id"]
        result = ask(client, session_id, question)
        first = result["first_ms"]
        if first is not None:
            firsts.append(first)
        mark = "" if first is None else ("  OK" if first <= TARGET_MS else "  SLOW")
        print(f"\n=== [{args.role}] {question}")
        print(f"  first words {first} ms{mark}, complete {result.get('total_ms')} ms")
        for call in (result["done"] or {}).get("toolCalls", []):
            print(f"  tool: {call['name']}({json.dumps(call['arguments'])}){'  ERROR: ' + call['error'] if call['error'] else ''}")
        for table in result["tables"]:
            print(f"  {'' if table.startswith('report') else 'table: '}{table}")
        if result["error"]:
            print(f"  ERROR: {result['error']}")
        print(result["text"])
    if firsts:
        print(f"\nFirst words: max {max(firsts)} ms, {sum(f <= TARGET_MS for f in firsts)}/{len(firsts)} within {TARGET_MS} ms")


if __name__ == "__main__":
    main()
