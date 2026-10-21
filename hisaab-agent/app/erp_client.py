import time
from typing import Any

import httpx


class ErpError(Exception):
    def __init__(self, status: int, message: str):
        super().__init__(message)
        self.status = status
        self.message = message


class ResponseCache:
    """ERP responses kept for a few seconds per token and endpoint, so follow-up questions do not refetch.
    Keyed by the token, so a user never sees data fetched with another user's (or an older role's) token."""

    def __init__(self, seconds: float, clock=time.monotonic):
        self._seconds = seconds
        self._clock = clock
        self._items: dict[tuple, tuple[float, Any]] = {}

    def get(self, key: tuple) -> Any | None:
        item = self._items.get(key)
        if item is None or item[0] <= self._clock():
            return None
        return item[1]

    def put(self, key: tuple, value: Any) -> None:
        now = self._clock()
        self._items = {k: v for k, v in self._items.items() if v[0] > now}
        self._items[key] = (now + self._seconds, value)


class ErpClient:
    """Read-only access to the Kratix ERP API as the signed-in user.

    Only GET is exposed, so the agent cannot change ERP data. Every call carries the
    user's own token, so the ERP applies that user's role rules to everything the agent sees.
    """

    def __init__(self, http: httpx.AsyncClient, token: str, cache: ResponseCache | None = None):
        self._http = http
        self._token = token
        self._headers = {"Authorization": f"Bearer {token}"}
        self._cache = cache

    async def get(self, path: str, params: dict[str, Any] | None = None) -> Any:
        query = {k: v for k, v in (params or {}).items() if v is not None}
        key = (self._token, path, tuple(sorted(query.items())))
        if self._cache is not None and (cached := self._cache.get(key)) is not None:
            return cached
        try:
            response = await self._http.get(path, params=query, headers=self._headers)
        except httpx.HTTPError as exc:
            raise ErpError(503, f"The ERP server could not be reached ({type(exc).__name__}).") from exc

        if response.status_code == 401:
            raise ErpError(401, "Your session has expired. Sign in again.")
        if response.status_code == 403:
            raise ErpError(403, "Your role does not have access to this information.")
        if response.status_code >= 400:
            raise ErpError(response.status_code, response.text[:300] or f"ERP returned HTTP {response.status_code}.")
        data = response.json()
        if self._cache is not None:
            self._cache.put(key, data)
        return data
