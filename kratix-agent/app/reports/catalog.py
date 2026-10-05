"""Report definitions: which ERP data each report reads and how it is laid out.

Reports are fixed templates filled from the same ERP endpoints as the tools; the model only picks the type, format
and dates, so every figure in a file can be traced to an ERP screen. The writers (excel.py, slides.py) only know the
neutral shapes below.
"""

import asyncio
from collections.abc import Awaitable, Callable
from dataclasses import dataclass, field
from datetime import date
from typing import Any

from ..erp_client import ErpClient

# Number formats understood by both writers.
PKR, QTY, KG, PERCENT, INT, TEXT, DATE = "pkr", "qty", "kg", "percent", "int", "text", "date"


@dataclass
class Column:
    header: str
    key: str
    fmt: str = TEXT


@dataclass
class Table:
    name: str                      # sheet name and slide title, at most 31 characters (Excel's limit)
    columns: list[Column]
    rows: list[dict[str, Any]]
    empty_text: str = "Nothing for this period."


@dataclass
class Figure:
    label: str
    value: Any
    fmt: str


@dataclass
class Chart:
    title: str
    kind: str                      # column, bar or line
    categories: list[str]
    values: list[float]
    series: str
    fmt: str


@dataclass
class ReportData:
    title: str                     # "Sales Report"
    period: str                    # "1 Oct 2026 to 5 Oct 2026" or "As of 5 Oct 2026"
    figures: list[Figure]
    tables: list[Table]
    charts: list[Chart] = field(default_factory=list)


@dataclass(frozen=True)
class ReportType:
    name: str
    title: str
    file_stem: str
    uses_period: bool
    build: Callable[[ErpClient, date, date], Awaitable[ReportData]]


def _day(value: str | None) -> str | None:
    return value[:10] if value else None


def day_text(day: date) -> str:
    return f"{day.day} {day:%b %Y}"


def _period_text(start: date, end: date) -> str:
    return f"{day_text(start)} to {day_text(end)}"


def _as_of_text(day: date) -> str:
    return f"As of {day_text(day)}"


def _top(rows: list[dict[str, Any]], label: str, value: str, count: int = 10) -> tuple[list[str], list[float]]:
    best = sorted(rows, key=lambda r: r[value], reverse=True)[:count]
    return [r[label] for r in best], [r[value] for r in best]


# ---------------------------------------------------------------- report builders

async def sales(erp: ErpClient, start: date, end: date) -> ReportData:
    data = await erp.get("reports/sales", {"from": start.isoformat(), "to": end.isoformat()})
    totals = data["totals"]
    invoices = [{**i, "invoiceDate": _day(i["invoiceDate"])} for i in data["invoices"]]
    charts = []
    if data["byCustomer"]:
        charts.append(Chart("Sales by customer (PKR)", "bar", *_top(data["byCustomer"], "customerName", "net"), "Net sales", PKR))
    if data["byProduct"]:
        charts.append(Chart("Sales by product (PKR)", "bar", *_top(data["byProduct"], "productName", "amount"), "Amount", PKR))
    return ReportData(
        "Sales Report", _period_text(start, end),
        [Figure("Invoices", totals["invoices"], INT), Figure("Net sales", totals["net"], PKR),
         Figure("Paid", totals["paid"], PKR), Figure("Balance due", totals["balance"], PKR)],
        [
            Table("By customer", [Column("Customer", "customerName"), Column("Invoices", "invoices", INT),
                                  Column("Net (PKR)", "net", PKR), Column("Paid (PKR)", "paid", PKR),
                                  Column("Balance (PKR)", "balance", PKR)], data["byCustomer"]),
            Table("By product", [Column("Product", "productName"), Column("Unit", "unit"), Column("Quantity", "quantity", QTY),
                                 Column("Amount (PKR)", "amount", PKR)], data["byProduct"]),
            Table("Invoices", [Column("Invoice", "invoiceNumber"), Column("Date", "invoiceDate", DATE),
                               Column("Customer", "customerName"), Column("Status", "status"),
                               Column("Net (PKR)", "netAmount", PKR), Column("Paid (PKR)", "paid", PKR),
                               Column("Balance (PKR)", "balance", PKR)], invoices),
        ],
        charts,
    )


AGING_BUCKETS = [("notDue", "Not due"), ("days1To30", "1-30 days"), ("days31To60", "31-60 days"), ("days61Plus", "61+ days")]


async def receivables(erp: ErpClient, start: date, end: date) -> ReportData:
    data = await erp.get("reports/receivables")
    totals = data["totals"]
    by_customer = [{"customerName": c["customerName"], "invoices": c["invoices"], **c["aging"]} for c in data["byCustomer"]]
    invoices = [{**i, "invoiceDate": _day(i["invoiceDate"]), "dueDate": _day(i["dueDate"]),
                 "bucket": dict(AGING_BUCKETS).get(i["bucket"][:1].lower() + i["bucket"][1:], i["bucket"])}
                for i in data["invoices"]]
    overdue = sum(totals[k] for k, _ in AGING_BUCKETS[1:])
    charts = []
    if totals["total"]:
        charts.append(Chart("Unpaid by age (PKR)", "column", [label for _, label in AGING_BUCKETS],
                            [totals[k] for k, _ in AGING_BUCKETS], "Balance", PKR))
    aging_columns = [Column(f"{label} (PKR)", key, PKR) for key, label in AGING_BUCKETS]
    return ReportData(
        "Receivables Report", _as_of_text(date.fromisoformat(data["asOf"][:10])),
        [Figure("Total unpaid", totals["total"], PKR), Figure("Overdue", overdue, PKR),
         Figure("Over 60 days", totals["days61Plus"], PKR), Figure("Open invoices", len(invoices), INT)],
        [
            Table("By customer", [Column("Customer", "customerName"), Column("Invoices", "invoices", INT), *aging_columns,
                                  Column("Total (PKR)", "total", PKR)], by_customer, "No unpaid invoices."),
            Table("Open invoices", [Column("Invoice", "invoiceNumber"), Column("Customer", "customerName"),
                                    Column("Date", "invoiceDate", DATE), Column("Due", "dueDate", DATE),
                                    Column("Net (PKR)", "netAmount", PKR), Column("Balance (PKR)", "balance", PKR),
                                    Column("Days overdue", "daysOverdue", INT), Column("Age", "bucket")], invoices,
                  "No unpaid invoices."),
        ],
        charts,
    )


async def stock(erp: ErpClient, start: date, end: date) -> ReportData:
    movement, inventory = await asyncio.gather(
        erp.get("reports/stock", {"from": start.isoformat(), "to": end.isoformat()}), erp.get("inventory"))
    on_hand = sorted(
        ({"productName": r["productName"], "category": r["category"], "warehouseName": r["warehouseName"],
          "lotNumber": r.get("lotNumber") or "", "unit": r["unit"], "quantity": r["quantityAvailable"]}
         for r in inventory if r["quantityAvailable"]),
        key=lambda r: (r["productName"], r["warehouseName"], r["lotNumber"]))
    rows = movement["rows"]
    charts = []
    # Bags and kg are not added up or drawn on one axis.
    for unit, label in [("bag", "bags"), ("kg", "kg")]:
        closing = [r for r in rows if r["unit"] == unit and r["closing"]]
        if closing:
            charts.append(Chart(f"Closing stock ({label})", "bar", *_top(closing, "productName", "closing", 12),
                                "Closing", QTY if unit == "bag" else KG))
    return ReportData(
        "Stock Report", _period_text(start, end),
        [Figure("Products in stock", len({r["productName"] for r in on_hand}), INT),
         Figure("Bags in stock", sum(r["quantity"] for r in on_hand if r["unit"] == "bag"), QTY),
         Figure("Kg in stock (bulk)", sum(r["quantity"] for r in on_hand if r["unit"] == "kg"), KG)],
        [
            Table("Movement summary", [Column("Code", "productCode"), Column("Product", "productName"),
                                       Column("Category", "category"), Column("Unit", "unit"),
                                       Column("Opening", "opening", QTY), Column("Received", "received", QTY),
                                       Column("Produced", "produced", QTY), Column("Used", "used", QTY),
                                       Column("Dispatched", "dispatched", QTY), Column("Closing", "closing", QTY)], rows),
            Table("Stock by warehouse and lot", [Column("Product", "productName"), Column("Category", "category"),
                                                 Column("Warehouse", "warehouseName"), Column("Lot", "lotNumber"),
                                                 Column("Unit", "unit"), Column("Quantity", "quantity", QTY)], on_hand,
                  "No stock on hand."),
        ],
        charts,
    )


async def production(erp: ErpClient, start: date, end: date) -> ReportData:
    data = await erp.get("reports/production", {"from": start.isoformat(), "to": end.isoformat()})
    totals = data["totals"]
    daily = [{**d, "date": _day(d["date"])} for d in data["daily"]]
    batches = [{**b, "productionDate": _day(b["productionDate"])} for b in data["batches"]]
    charts = []
    if len(daily) > 1:
        charts.append(Chart("Daily yield (%)", "line", [d["date"] for d in daily], [d["yield"] for d in daily], "Yield", PERCENT))
    if data["outputs"]:
        charts.append(Chart("Output by product (kg)", "bar", *_top(data["outputs"], "productName", "quantityKg", 12),
                            "Kg", KG))
    return ReportData(
        "Production Report", _period_text(start, end),
        [Figure("Batches", totals["batches"], INT), Figure("Wheat milled (kg)", totals["input"], KG),
         Figure("Output (kg)", totals["output"], KG), Figure("Waste (kg)", totals["waste"], KG),
         Figure("Yield", totals["yield"], PERCENT)],
        [
            Table("Daily", [Column("Date", "date", DATE), Column("Batches", "batches", INT), Column("Input (kg)", "input", KG),
                            Column("Output (kg)", "output", KG), Column("Waste (kg)", "waste", KG),
                            Column("Yield", "yield", PERCENT)], daily),
            Table("Batches", [Column("Batch", "batchNumber"), Column("Date", "productionDate", DATE),
                              Column("Shift", "shiftNumber", INT), Column("Operator", "operatorName"),
                              Column("Input (kg)", "input", KG), Column("Output (kg)", "output", KG),
                              Column("Waste (kg)", "waste", KG), Column("Yield", "yield", PERCENT)], batches),
            Table("Output by product", [Column("Product", "productName"), Column("Type", "materialType"),
                                        Column("Quantity (kg)", "quantityKg", KG)], data["outputs"]),
        ],
        charts,
    )


REPORT_TYPES: dict[str, ReportType] = {t.name: t for t in [
    ReportType("sales", "Sales Report", "Sales", True, sales),
    ReportType("receivables", "Receivables Report", "Receivables", False, receivables),
    ReportType("stock", "Stock Report", "Stock", True, stock),
    ReportType("production", "Production Report", "Production", True, production),
]}
