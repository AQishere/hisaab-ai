"""Language model client: Gemini first, then Groq, then a local Ollama, all over the OpenAI-compatible chat API, so
one tool schema and one message format serve every provider. A gate limits how many questions use the model at once.

A provider is skipped for the rest of a reply once it answers, so a reply never mixes two providers' text; between
model calls (tool rounds) the next healthy provider is tried again."""

import asyncio
import json
import logging
import time
from collections.abc import AsyncIterator
from contextlib import asynccontextmanager
from dataclasses import dataclass, field
from typing import Any

import httpx

log = logging.getLogger("kratix.agent.llm")


class LlmError(Exception):
    pass


class BusyError(Exception):
    pass


class _Retryable(Exception):
    """Worth trying the next provider: rate limit, server error, timeout or no connection."""

    def __init__(self, message: str, rate_limited: bool = False):
        super().__init__(message)
        self.rate_limited = rate_limited


@dataclass
class Provider:
    name: str
    http: httpx.AsyncClient
    model: str
    options: dict[str, Any] = field(default_factory=dict)  # extra body fields, e.g. reasoning_effort for Gemini
    # USD per million tokens, for the cost column of the question log.
    price_in: float = 0.0
    price_out: float = 0.0
    # Gemini returns a thought signature in tool calls and needs it back; other providers reject unknown fields.
    keeps_extra_fields: bool = False
    failures: int = 0
    open_until: float = 0.0

    def cost(self, prompt_tokens: int, completion_tokens: int) -> float:
        return (prompt_tokens * self.price_in + completion_tokens * self.price_out) / 1_000_000


class Llm:
    def __init__(self, providers: list[Provider], *, parallel: int, queue_cap: int, first_token_seconds: float = 8.0,
                 breaker_failures: int = 3, breaker_seconds: float = 60.0, clock=time.monotonic):
        if not providers:
            raise ValueError("At least one language model provider is needed.")
        self.providers = providers
        self._gate = asyncio.Semaphore(parallel)
        self._queue_cap = queue_cap
        self._waiting = 0
        self._first_token_seconds = first_token_seconds
        self._breaker_failures = breaker_failures
        self._breaker_seconds = breaker_seconds
        self._clock = clock

    @property
    def model(self) -> str:
        """The primary model, as configured."""
        return self.providers[0].model

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

    def _healthy(self) -> list[Provider]:
        now = self._clock()
        healthy = [p for p in self.providers if p.open_until <= now]
        # All tripped: try them anyway rather than refuse every question for a minute.
        return healthy or list(self.providers)

    def _failed(self, provider: Provider, reason: str) -> None:
        provider.failures += 1
        if provider.failures >= self._breaker_failures:
            provider.open_until = self._clock() + self._breaker_seconds
            log.warning("%s skipped for %.0f s after %d failures (%s)", provider.name, self._breaker_seconds,
                        provider.failures, reason)

    async def stream_chat(self, messages: list[dict[str, Any]],
                          tools: list[dict[str, Any]] | None) -> AsyncIterator[dict[str, Any]]:
        """Yield {"provider": name, "fallback": bool} first, then {"content": text} as text arrives, then
        {"tool_calls": [...]} if the model called tools and {"usage": {...}} if the provider reported tokens.

        A provider that fails before its first chunk (429, 5xx, no connection, nothing within first_token_seconds)
        is passed over for the next one. After the first chunk the reply is committed to that provider."""
        last: _Retryable | None = None
        for index, provider in enumerate(self._healthy()):
            stream = self._stream(provider, messages, tools)
            try:
                first = await asyncio.wait_for(anext(stream), self._first_token_seconds)
            except StopAsyncIteration:
                first = None
            except (_Retryable, TimeoutError) as exc:
                await stream.aclose()
                last = exc if isinstance(exc, _Retryable) else _Retryable(f"no reply in {self._first_token_seconds:g} s")
                self._failed(provider, str(last))
                log.warning("%s failed (%s); trying the next provider", provider.name, last)
                continue
            provider.failures = 0
            yield {"provider": provider.name, "fallback": index > 0 or provider is not self.providers[0]}
            if first is not None:
                yield first
                try:
                    async for chunk in stream:
                        yield chunk
                except _Retryable as exc:
                    raise LlmError(f"{provider.name} failed while answering: {exc}") from exc
            return
        if last is not None and last.rate_limited:
            raise BusyError()
        raise LlmError(f"No language model answered ({last}).")

    def _body(self, provider: Provider, messages: list[dict[str, Any]], tools: list[dict[str, Any]] | None) -> dict:
        if not provider.keeps_extra_fields:
            messages = [_standard(m) for m in messages]
        body: dict[str, Any] = {"model": provider.model, "messages": messages, "stream": True,
                                "stream_options": {"include_usage": True},
                                **{k: v for k, v in provider.options.items() if v not in (None, "")}}
        if tools:
            body["tools"] = tools
        return body

    async def _stream(self, provider: Provider, messages: list[dict[str, Any]],
                      tools: list[dict[str, Any]] | None) -> AsyncIterator[dict[str, Any]]:
        calls: dict[int, dict[str, Any]] = {}
        usage: dict[str, Any] | None = None
        try:
            async with provider.http.stream("POST", "chat/completions", json=self._body(provider, messages, tools)) as response:
                if response.status_code == 429 or response.status_code >= 500:
                    raise _Retryable(f"HTTP {response.status_code}", rate_limited=response.status_code == 429)
                if response.status_code != 200:
                    text = (await response.aread()).decode(errors="replace")[:200]
                    raise LlmError(f"{provider.name} returned HTTP {response.status_code}: {text}")
                async for line in response.aiter_lines():
                    if not line.startswith("data:"):
                        continue
                    data = line[5:].strip()
                    if data == "[DONE]":
                        break
                    chunk = json.loads(data)
                    usage = chunk.get("usage") or usage
                    for choice in chunk.get("choices") or []:
                        delta = choice.get("delta") or {}
                        for call in delta.get("tool_calls") or []:
                            _merge_call(calls, call)
                        if delta.get("content"):
                            yield {"content": delta["content"]}
        except httpx.TimeoutException as exc:
            raise _Retryable("timed out") from exc
        except httpx.TransportError as exc:
            raise _Retryable(f"could not be reached ({type(exc).__name__})") from exc
        if calls:
            yield {"tool_calls": [calls[i] for i in sorted(calls)]}
        if usage:
            prompt, completion = usage.get("prompt_tokens") or 0, usage.get("completion_tokens") or 0
            yield {"usage": {"prompt_tokens": prompt, "completion_tokens": completion,
                             "cost_usd": provider.cost(prompt, completion)}}

    async def reachable(self) -> dict[str, str]:
        """Per provider: ok, or why not (key rejected, model unknown, unreachable)."""
        status = {}
        for provider in self.providers:
            try:
                response = await provider.http.get("models", timeout=5)
                status[provider.name] = "ok" if response.status_code == 200 else f"HTTP {response.status_code}"
            except httpx.HTTPError:
                status[provider.name] = "unreachable"
            if provider.open_until > self._clock():
                status[provider.name] += " (skipped after failures)"
        return status


def _standard(message: dict[str, Any]) -> dict[str, Any]:
    """The message without provider-specific fields (Gemini's extra_content on tool calls)."""
    if not message.get("tool_calls"):
        return message
    return {**message, "tool_calls": [{k: v for k, v in c.items() if k in ("id", "type", "function")}
                                      for c in message["tool_calls"]]}


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
