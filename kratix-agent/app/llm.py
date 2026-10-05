"""Ollama client: streamed chat, warm-up, keep-alive and a gate so the GPU answers one question at a time."""

import asyncio
import json
from collections.abc import AsyncIterator
from contextlib import asynccontextmanager
from typing import Any

import httpx


class OllamaError(Exception):
    pass


class BusyError(Exception):
    pass


class Llm:
    def __init__(self, http: httpx.AsyncClient, model: str, *, keep_alive: str, parallel: int, queue_cap: int,
                 options: dict[str, Any]):
        self._http = http
        self.model = model
        self._keep_alive = keep_alive
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

    def _body(self, messages: list[dict[str, Any]], tools: list[dict[str, Any]] | None, **options: Any) -> dict[str, Any]:
        body: dict[str, Any] = {"model": self.model, "messages": messages, "stream": True,
                                "keep_alive": self._keep_alive, "options": {**self._options, **options}}
        if tools:
            body["tools"] = tools
        return body

    async def stream_chat(self, messages: list[dict[str, Any]],
                          tools: list[dict[str, Any]] | None) -> AsyncIterator[dict[str, Any]]:
        """Yield Ollama's streamed chunks. One retry on a connection error before anything arrived; none on timeout,
        where a retry would double the wait."""
        body = self._body(messages, tools)
        for attempt in (1, 2):
            try:
                async with self._http.stream("POST", "/api/chat", json=body) as response:
                    if response.status_code != 200:
                        text = (await response.aread()).decode(errors="replace")[:200]
                        raise OllamaError(f"The language model returned HTTP {response.status_code}: {text}")
                    async for line in response.aiter_lines():
                        if line:
                            yield json.loads(line)
                return
            except httpx.ConnectError as exc:
                if attempt == 2:
                    raise OllamaError("The language model could not be reached. Is Ollama running?") from exc
            except httpx.HTTPError as exc:
                raise OllamaError(f"The language model failed ({type(exc).__name__}).") from exc

    async def warm_up(self, messages: list[dict[str, Any]], tools: list[dict[str, Any]]) -> None:
        """Load the model and read the shared prompt prefix once, so the first question is not a cold start."""
        async with self._gate:
            response = await self._http.post("/api/chat", json={**self._body(messages, tools, num_predict=1),
                                                                "stream": False})
            response.raise_for_status()

    async def loaded(self) -> bool:
        response = await self._http.get("/api/ps", timeout=5)
        return any(m.get("name") == self.model for m in response.json().get("models", []))

    async def pulled(self) -> bool:
        response = await self._http.get("/api/tags", timeout=5)
        return any(m.get("name") == self.model for m in response.json().get("models", []))
