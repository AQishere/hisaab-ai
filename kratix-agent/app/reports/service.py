"""Make a report file for a user, record it, and find it again for download."""

import asyncio
import uuid
from datetime import UTC, date, datetime, timedelta
from pathlib import Path
from typing import Any

from ..auth import CurrentUser
from ..erp_client import ErpClient, ErpError
from ..tools import ToolInputError, date_arg, resolve_period
from .catalog import REPORT_TYPES
from .excel import write_excel
from .slides import write_slides

FORMATS = {"xlsx": write_excel, "pptx": write_slides}
# Owner and Admin may download any user's report (the question log is theirs to audit too).
SEE_ALL_ROLES = {"Owner", "Admin"}


class ReportError(Exception):
    def __init__(self, status: int, message: str):
        super().__init__(message)
        self.status = status
        self.message = message


def _iso(moment: datetime) -> str:
    return moment.isoformat(timespec="milliseconds")


class ReportService:
    def __init__(self, store, data_dir: Path, company: str, *, retention_days: int, per_hour: int,
                 clock=lambda: datetime.now(UTC)):
        self._store = store
        self._dir = data_dir / "reports"
        self._company = company
        self._retention = timedelta(days=retention_days)
        self._per_hour = per_hour
        self._clock = clock

    def _dates(self, kind: str, period: str | None, from_date: str | None, to_date: str | None,
               today: date) -> tuple[date, date]:
        if period:
            return resolve_period(period, today)
        start, end = date_arg({"d": from_date}, "d"), date_arg({"d": to_date}, "d")
        if not start and not end:
            return resolve_period("this_month", today)
        start_day = date.fromisoformat(start) if start else today.replace(day=1)
        end_day = date.fromisoformat(end) if end else today
        if end_day < start_day:
            raise ToolInputError("The end date must be on or after the start date.")
        return start_day, end_day

    async def create(self, user: CurrentUser, erp: ErpClient, kind: str, fmt: str, *, period: str | None = None,
                     from_date: str | None = None, to_date: str | None = None) -> dict[str, Any]:
        report_type = REPORT_TYPES.get(kind)
        if report_type is None:
            raise ReportError(422, f"Report type must be one of {', '.join(REPORT_TYPES)}.")
        if fmt not in FORMATS:
            raise ReportError(422, "Format must be xlsx (Excel) or pptx (PowerPoint).")
        now = self._clock()
        if self._store.reports_since(user.id, _iso(now - timedelta(hours=1))) >= self._per_hour:
            raise ReportError(429, f"You can make {self._per_hour} reports an hour. Please try again later.")
        today = now.astimezone().date()
        try:
            start, end = self._dates(kind, period, from_date, to_date, today)
        except ToolInputError as exc:
            raise ReportError(422, str(exc)) from exc

        try:
            data = await report_type.build(erp, start, end)
        except ErpError as exc:
            if exc.status == 401:
                raise ReportError(401, "Sign in again.") from exc
            if exc.status == 403:
                raise ReportError(403, "Your role does not have access to this information.") from exc
            raise ReportError(502, "The ERP server is not responding.") from exc

        report_id = str(uuid.uuid4())
        folder = self._dir / str(user.id)
        folder.mkdir(parents=True, exist_ok=True)
        path = folder / f"{report_id}.{fmt}"
        # Writing takes a moment for big decks; keep the event loop free for other users' answers.
        await asyncio.to_thread(FORMATS[fmt], data, path, self._company, user.username, today)

        if report_type.uses_period:
            params = {"from": start.isoformat(), "to": end.isoformat()}
            file_name = f"{report_type.file_stem}_{start.isoformat()}_to_{end.isoformat()}.{fmt}"
        else:
            params = {"asOf": today.isoformat()}
            file_name = f"{report_type.file_stem}_as_of_{today.isoformat()}.{fmt}"
        record = {"id": report_id, "user_id": user.id, "type": kind, "format": fmt, "params": params,
                  "file_name": file_name, "size_bytes": path.stat().st_size, "created_at": _iso(now),
                  "expires_at": _iso(now + self._retention)}
        self._store.add_report(record)
        return self._store.get_report(report_id, _iso(now))

    def list(self, user: CurrentUser) -> list[dict[str, Any]]:
        return self._store.list_reports(user.id, _iso(self._clock()))

    def file_for(self, user: CurrentUser, report_id: str) -> tuple[Path, str]:
        """The file and its download name, if the user may have it. Expired or missing: ReportError 404."""
        report = self._store.get_report(report_id, _iso(self._clock()))
        if report is None or (report["userId"] != user.id and user.role not in SEE_ALL_ROLES):
            raise ReportError(404, "Report not found or expired.")
        path = self._dir / str(report["userId"]) / f"{report['id']}.{report['format']}"
        if not path.is_file():
            raise ReportError(404, "Report not found or expired.")
        return path, report["fileName"]
