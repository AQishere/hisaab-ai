"""Ask the 30 questions in eval/questions.json once per configured provider, each provider on its own, and compare
which tools they call. Shows where Groq or Ollama would answer a question differently from Gemini after a fallback.

Usage (the ERP must be running; keys come from kratix-agent/.env like the agent's):
    .venv/Scripts/python -m eval.compare_providers                    # every provider with a key or URL
    .venv/Scripts/python -m eval.compare_providers --only gemini groq
Signs in as the demo Owner: one-click demo sign-in when the ERP runs in demo mode, else the development password
from the git-ignored appsettings.Development.json. Read-only: tools only read the ERP.
"""

import argparse
import asyncio
import json
import sys
from pathlib import Path

import httpx

from app.agent import Agent
from app.config import settings
from app.erp_client import ErpClient
from app.llm import Llm
from app.main import _providers

QUESTIONS = Path(__file__).with_name("questions.json")
DEV_SETTINGS = Path(__file__).resolve().parents[2] / "QadoosFlourMill" / "Kratix.ERP" / "appsettings.Development.json"


def owner_token(erp_url: str) -> str:
    demo = httpx.post(f"{erp_url}/auth/demo-login", json={"username": "owner"})
    if demo.status_code == 200:
        return demo.json()["token"]
    users = json.loads(DEV_SETTINGS.read_text(encoding="utf-8-sig"))["DemoUsers"]
    owner = next(u for u in users if u["Role"] == "Owner")
    response = httpx.post(f"{erp_url}/auth/login", json={"username": owner["Username"], "password": owner["Password"]})
    response.raise_for_status()
    return response.json()["token"]


async def run(only: list[str] | None) -> int:
    questions = json.loads(QUESTIONS.read_text(encoding="utf-8"))
    providers = [p for p in _providers() if not only or p.name in only]
    if not providers:
        print("No provider configured (AGENT_GEMINI_API_KEY, AGENT_GROQ_API_KEY or AGENT_OLLAMA_URL).")
        return 1
    token = owner_token(settings.erp_api_url.rstrip("/"))
    async with httpx.AsyncClient(base_url=settings.erp_api_url.rstrip("/") + "/", timeout=30) as erp_http:
        picked: dict[str, list[list[str]]] = {}
        for provider in providers:
            # One provider at a time, with no fallback, so each column shows that provider's own choices.
            agent = Agent(Llm([provider], parallel=1, queue_cap=1, first_token_seconds=30))
            picked[provider.name] = []
            for item in questions:
                try:
                    result = await agent.ask(item["q"], [], ErpClient(erp_http, token), "Owner")
                    picked[provider.name].append([c.name for c in result.tool_calls])
                except Exception as exc:  # noqa: BLE001 - recorded in the table instead of stopping the run
                    picked[provider.name].append([f"ERROR {type(exc).__name__}"])
            await provider.http.aclose()

    names = list(picked)
    print(f"{'#':>2}  {'expected':<28}" + "".join(f"{n:<30}" for n in names) + "question")
    right = dict.fromkeys(names, 0)
    disagree = 0
    for i, item in enumerate(questions):
        cells = []
        for n in names:
            tools = picked[n][i]
            ok = set(tools) <= set(item["tools"]) and bool(tools) == bool(item["tools"])
            right[n] += ok
            cells.append(f"{'ok ' if ok else 'XX '}{','.join(tools) or '(none)'}"[:29].ljust(30))
        disagree += len({tuple(picked[n][i]) for n in names}) > 1
        print(f"{i + 1:>2}  {','.join(item['tools']) or '(none)':<28}" + "".join(cells) + item["q"][:60])
    print()
    for n in names:
        print(f"{n}: {right[n]}/{len(questions)} picked an expected tool")
    if len(names) > 1:
        print(f"Providers disagreed on {disagree} of {len(questions)} questions.")
    return 0


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--only", nargs="*", help="provider names: gemini, groq, ollama")
    args = parser.parse_args()
    sys.stdout.reconfigure(encoding="utf-8")  # Urdu questions
    sys.exit(asyncio.run(run(args.only)))


if __name__ == "__main__":
    main()
