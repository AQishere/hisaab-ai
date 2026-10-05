"""Per-user limits: a number of questions per minute, and one question in flight at a time."""

import time
from collections import defaultdict, deque


class LimitError(Exception):
    def __init__(self, status: int, message: str, log_status: str):
        super().__init__(message)
        self.status = status
        self.message = message
        self.log_status = log_status


class UserLimits:
    def __init__(self, per_minute: int, clock=time.monotonic):
        self._per_minute = per_minute
        self._clock = clock
        self._recent: dict[int, deque[float]] = defaultdict(deque)
        self._in_flight: set[int] = set()

    def acquire(self, user_id: int) -> None:
        """Reserve a question for the user, or raise LimitError. Call release() when the answer is finished."""
        if user_id in self._in_flight:
            raise LimitError(429, "Still answering your last question.", "busy")
        now = self._clock()
        recent = self._recent[user_id]
        while recent and now - recent[0] >= 60:
            recent.popleft()
        if len(recent) >= self._per_minute:
            raise LimitError(429, "Too many questions in the last minute. Please wait a moment.", "busy")
        recent.append(now)
        self._in_flight.add(user_id)

    def release(self, user_id: int) -> None:
        self._in_flight.discard(user_id)
