"""Gemini client over its OpenAI-compatible endpoint: streamed chat with tools, and a gate so the agent sends a
limited number of questions to the API at a time."""

import asyncio
import json
from collections.abc import AsyncIterator
from contextlib import asynccontextmanager
from typing import Any

import httpx


class LlmError(Exception):
    pass


class BusyError(Exception):
    pass


class Llm:
    def __init__(self, http: httpx.AsyncClient, model: str, *, parallel: int, queue_cap: int, options: dict[str, Any]):
        self._http = http
        self.model = model
        self._gate = asyncio.Semaphore(parallel)
        self._queue_cap = queue_cap
        self._waiting = 0
        self._options = options

    @property
    def queued(self) -> int:
        """Questions waiting for or using the model."""
        return self._waiting

    @asynccontextmanager
    async def slot(self):
        """Hold the model for one whole question. Raises BusyError when the queue is full."""
        if self._waiting >= self._queue_cap:
            raise BusyError()
        self._waiting += 1
        try:
            async with self._gate:
                yield
        finally:
            self._waiting -= 1

    def _body(self, messages: list[dict[str, Any]], tools: list[dict[str, Any]] | None) -> dict[str, Any]:
        body: dict[str, Any] = {"model": self.model, "messages": messages, "stream": True,
                                **{k: v for k, v in self._options.items() if v not in (None, "")}}
        if tools:
            body["tools"] = tools
        return body

    async def stream_chat(self, messages: list[dict[str, Any]],
                          tools: list[dict[str, Any]] | None) -> AsyncIterator[dict[str, Any]]:
        """Yield {"content": text} as answer text arrives, then {"tool_calls": [...]} once if the model called tools.
        Tool calls are passed on as Gemini sent them (with any thought signature), so they can be sent back as is.
        One retry on a connection error before anything arrived; none on timeout, where a retry would double the wait."""
        body = self._body(messages, tools)
        for attempt in (1, 2):
            calls: dict[int, dict[str, Any]] = {}
            try:
                async with self._http.stream("POST", "chat/completions", json=body) as response:
                    if response.status_code == 429:
                        raise BusyError()
                    if response.status_code != 200:
                        text = (await response.aread()).decode(errors="replace")[:200]
                        raise LlmError(f"The language model returned HTTP {response.status_code}: {text}")
                    async for line in response.aiter_lines():
                        if not line.startswith("data:"):
                            continue
                        data = line[5:].strip()
                        if data == "[DONE]":
                            break
                        for choice in json.loads(data).get("choices") or []:
                            delta = choice.get("delta") or {}
                            for call in delta.get("tool_calls") or []:
                                _merge_call(calls, call)
                            if delta.get("content"):
                                yield {"content": delta["content"]}
                if calls:
                    yield {"tool_calls": [calls[i] for i in sorted(calls)]}
                return
            except httpx.ConnectError as exc:
                if attempt == 2:
                    raise LlmError("The language model could not be reached.") from exc
            except httpx.HTTPError as exc:
                raise LlmError(f"The language model failed ({type(exc).__name__}).") from exc

    async def reachable(self) -> bool:
        """The API key is accepted and the model exists."""
        response = await self._http.get(f"models/{self.model}", timeout=5)
        return response.status_code == 200


def _merge_call(calls: dict[int, dict[str, Any]], part: dict[str, Any]) -> None:
    """Streamed tool calls arrive whole or in pieces sharing an index; argument text is joined, other fields kept."""
    index = part.get("index", len(calls))
    call = calls.setdefault(index, {"id": "", "type": "function", "function": {"name": "", "arguments": ""}})
    for key, value in part.items():
        if key == "function":
            call["function"]["name"] += value.get("name") or ""
            arguments = value.get("arguments") or ""
            call["function"]["arguments"] += arguments if isinstance(arguments, str) else json.dumps(arguments)
        elif key != "index" and value:
            call[key] = value
    if not call["id"]:
        call["id"] = f"call_{index}"
