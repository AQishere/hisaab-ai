"""Read-only tools the model can call. Each one wraps ERP API endpoints and returns rounded numbers and only the
fields that answer questions.

Every result is split in two (rule 2 in DESIGN.md section 8): the model reads a short summary with the first few rows
of the main list, and the app gets the full list as a table. Writing a table costs the model seconds; drawing it costs
nothing."""

import asyncio
import re
from collections import defaultdict
from collections.abc import Awaitable, Callable
from dataclasses import dataclass
from datetime import date, timedelta
from typing import Any

from .erp_client import ErpClient, ErpError

# Rows of the main list the model reads; the app shows all of them.
MODEL_ROWS = 10
# Rows sent to the app as a table.
DATA_ROWS = 500

ToolFn = Callable[[ErpClient, dict[str, Any]], Awaitable[Any]]


@dataclass(frozen=True)
class Tool:
    name: str
    description: str
    parameters: dict[str, Any]
    run: ToolFn
    # Key of the main list in the result: summarised for the model, sent in full to the app.
    table: str | None = None
    # Receives the ToolContext as a third argument (tools that act for the signed-in user, such as reports).
    wants_context: bool = False

    def schema(self) -> dict[str, Any]:
        return {
            "type": "function",
            "function": {"name": self.name, "description": self.description, "parameters": self.parameters},
        }


class ToolInputError(ValueError):
    pass


@dataclass
class ToolOutput:
    model: Any                            # what the model reads
    data: dict[str, Any] | None = None    # full table for the app: {tool, title, count, rows}
    report: dict[str, Any] | None = None  # a report file made by create_report


@dataclass
class ToolContext:
    user: Any      # auth.CurrentUser
    reports: Any   # reports.service.ReportService


# ---------------------------------------------------------------- helpers

def _round(value: Any) -> Any:
    """Round floats to 2 places, recursively, so the model sees clean figures."""
    if isinstance(value, float):
        return round(value, 2)
    if isinstance(value, dict):
        return {k: _round(v) for k, v in value.items()}
    if isinstance(value, list):
        return [_round(v) for v in value]
    return value


def _day(value: str | None) -> str | None:
    """Keep only the date part of an ERP timestamp."""
    return value[:10] if value else None


def _matches(text: str | None, needle: str | None) -> bool:
    return not needle or (text is not None and needle.strip().lower() in text.lower())


def _product_matches(row: dict[str, Any], product: str | None) -> bool:
    """Product name or code contains any of the products asked for. The model sometimes sends a list as one
    filter ("wheat, flour, bran") and then reported no stock at all."""
    if not product:
        return True
    terms = [t for t in re.split(r",|/|\band\b|\+", product.lower()) if t.strip()]
    return any(_matches(row.get("productName"), t) or _matches(row.get("productCode"), t) for t in terms)


def date_arg(args: dict[str, Any], key: str) -> str | None:
    value = args.get(key)
    if value in (None, ""):
        return None
    try:
        return date.fromisoformat(str(value)).isoformat()
    except ValueError as exc:
        raise ToolInputError(f"{key} must be a date in YYYY-MM-DD format, got {value!r}.") from exc


def _listing(rows: list[Any], label: str) -> dict[str, Any]:
    result: dict[str, Any] = {label: rows, "count": len(rows)}
    if not rows:
        result["note"] = "Nothing matched. Tell the user nothing was found; do not guess."
    return result


def split(tool: Tool, result: Any) -> ToolOutput:
    """Short version for the model, full table for the app."""
    if not tool.table or not isinstance(result, dict) or not isinstance(result.get(tool.table), list):
        return ToolOutput(result)
    rows = result[tool.table]
    # Other lists (by customer, by product, daily) are cut too; totals carry the overall figures.
    model = {k: v[:MODEL_ROWS] if isinstance(v, list) and k != "lots" else v for k, v in result.items()}
    if len(rows) > MODEL_ROWS:
        model["note"] = (f"Only the first {MODEL_ROWS} of {len(rows)} are listed here; the app shows the user all "
                         f"{len(rows)} under your answer. Use count and totals for overall figures.")
    return ToolOutput(model, {"tool": tool.name, "title": tool.table, "count": len(rows), "rows": rows[:DATA_ROWS]})


PERIODS = ["today", "yesterday", "this_week", "last_week", "this_month", "last_month", "this_year"]


def resolve_period(name: str, today: date) -> tuple[date, date]:
    """Named period to (first day, last day). The server does the date arithmetic: it is cheaper than the model
    writing dates (about 25 tokens) and the model got it wrong before (31 October for "this month" on 4 October)."""
    monday = today - timedelta(days=today.weekday())
    first_of_month = today.replace(day=1)
    match name:
        case "today":
            return today, today
        case "yesterday":
            return today - timedelta(days=1), today - timedelta(days=1)
        case "this_week":
            return monday, today
        case "last_week":
            return monday - timedelta(days=7), monday - timedelta(days=1)
        case "this_month":
            return first_of_month, today
        case "last_month":
            last = first_of_month - timedelta(days=1)
            return last.replace(day=1), last
        case "this_year":
            return today.replace(month=1, day=1), today
    raise ToolInputError(f"period must be one of {', '.join(PERIODS)}, got {name!r}.")


def _period_params(args: dict[str, Any], today: date | None = None) -> dict[str, str | None]:
    if args.get("period"):
        start, end = resolve_period(str(args["period"]), today or date.today())
        return {"from": start.isoformat(), "to": end.isoformat()}
    return {"from": date_arg(args, "from_date"), "to": date_arg(args, "to_date")}


_PERIOD = {
    "period": {"type": "string", "enum": PERIODS,
               "description": "Preferred way to give dates. Defaults to this_month."},
    "from_date": {"type": "string", "description": "Only for specific dates the periods cannot express: start, YYYY-MM-DD."},
    "to_date": {"type": "string", "description": "Only with from_date: end, YYYY-MM-DD."},
}


def _params(properties: dict[str, Any] | None = None) -> dict[str, Any]:
    return {"type": "object", "properties": properties or {}, "required": []}


# ---------------------------------------------------------------- tools

async def business_overview(erp: ErpClient, args: dict[str, Any]) -> Any:
    return _round(await erp.get("dashboard/metrics"))


async def receivables(erp: ErpClient, args: dict[str, Any]) -> Any:
    data = await erp.get("reports/receivables")
    customer = args.get("customer")
    invoices = [
        {
            "invoice": i["invoiceNumber"], "customer": i["customerName"], "invoiceDate": _day(i["invoiceDate"]),
            "dueDate": _day(i["dueDate"]), "netAmount": i["netAmount"], "balance": i["balance"],
            "daysOverdue": i["daysOverdue"], "bucket": i["bucket"],
        }
        for i in data["invoices"]
        if _matches(i["customerName"], customer)
    ]
    by_customer = [c for c in data["byCustomer"] if _matches(c["customerName"], customer)]
    result = {
        "asOf": _day(data["asOf"]),
        "totalsPKR": data["totals"] if not customer else None,
        "byCustomerPKR": by_customer,
        **_listing(invoices, "openInvoices"),
    }
    return _round({k: v for k, v in result.items() if v is not None})


async def sales_report(erp: ErpClient, args: dict[str, Any]) -> Any:
    data = await erp.get("reports/sales", _period_params(args))
    customer = args.get("customer")
    invoices = [
        {**i, "invoiceDate": _day(i["invoiceDate"])}
        for i in data["invoices"]
        if _matches(i["customerName"], customer)
    ]
    result = {
        "from": _day(data["from"]), "to": _day(data["to"]),
        "totalsPKR": data["totals"],
        "byCustomer": [c for c in data["byCustomer"] if _matches(c["customerName"], customer)],
        **_listing(invoices, "invoices"),
        # After the invoices and named as a combined total: placed next to byCustomer, the model credited the bran
        # sold to one customer to another. Tested against other layouts on 2026-10-05.
        "productTotalsAcrossAllCustomers": data["byProduct"] if not customer else None,
    }
    if not customer:
        result["guide"] = "Do not say which customer bought which product: the data does not show it."
    if customer:
        result["note"] = "totalsPKR covers all customers; byCustomer and invoices are filtered to the customer asked about."
    return _round({k: v for k, v in result.items() if v is not None})


# Raw wheat first: asked "how much wheat", the model reported the 60 bags of wheat bran before saying raw wheat was out.
CATEGORY_ORDER = {"Raw Material": 0, "Finished Good": 1, "By-product": 2, "Packaging": 3, "Waste": 4}


def _category_rank(category: str | None) -> int:
    return CATEGORY_ORDER.get(category or "", len(CATEGORY_ORDER))


async def current_stock(erp: ErpClient, args: dict[str, Any]) -> Any:
    product, warehouse = args.get("product"), args.get("warehouse")
    if product:
        rows, products = await asyncio.gather(erp.get("inventory"), erp.get("products"))
    else:
        rows, products = await erp.get("inventory"), []
    totals: dict[tuple[str, str, str, str], dict[str, Any]] = {}
    for r in rows:
        if not _product_matches(r, product):
            continue
        if not _matches(r["warehouseName"], warehouse):
            continue
        key = (r["productName"], r["category"], r["warehouseName"], r["unit"])
        entry = totals.setdefault(key, {
            "product": r["productName"], "category": r["category"], "warehouse": r["warehouseName"],
            "unit": r["unit"], "quantity": 0.0, "lots": set(),
        })
        entry["quantity"] += r["quantityAvailable"]
        if r.get("lotNumber"):
            entry["lots"].add(r["lotNumber"])
    stock = [
        {**{k: v for k, v in e.items() if k != "lots"}, "lots": sorted(e["lots"])}
        for e in totals.values()
        if e["quantity"] != 0
    ]
    if product:
        # Products asked about that have none are listed with quantity 0, so the model says so instead of
        # substituting a similar name (wheat bran for wheat).
        in_stock = {e["product"] for e in stock}
        stock += [
            {"product": p["productName"], "category": p.get("category"), "warehouse": None,
             "unit": p.get("unit"), "quantity": 0.0, "lots": []}
            for p in products
            if _product_matches(p, product)
            and p["productName"] not in in_stock
        ]
    stock.sort(key=lambda e: (_category_rank(e["category"]), e["product"], e["warehouse"] or ""))
    # "Wheat" means raw wheat: when the search matches raw material, other matches (wheat bran) are kept apart,
    # otherwise the model leads with the bran.
    others = []
    if product and any(e["category"] == "Raw Material" for e in stock):
        others = [{"product": e["product"], "category": e["category"], "quantity": e["quantity"], "unit": e["unit"]}
                  for e in stock if e["category"] != "Raw Material"]
        stock = [e for e in stock if e["category"] == "Raw Material"]
    result = _listing(stock, "stock")
    if others:
        result["otherMatchingProducts"] = others
    if any(e["quantity"] == 0 for e in stock):
        result["guide"] = ("Quantity 0 means out of stock" + (" in this warehouse" if warehouse else "") +
                           ". Answer about stock first; otherMatchingProducts are not the product asked about, "
                           "mention them only briefly at the end.")
    return _round(result)


async def stock_movement_report(erp: ErpClient, args: dict[str, Any]) -> Any:
    data = await erp.get("reports/stock", _period_params(args))
    product = args.get("product")
    rows = [r for r in data["rows"] if _product_matches(r, product)]
    return _round({"from": _day(data["from"]), "to": _day(data["to"]), **_listing(rows, "rows")})


async def production_report(erp: ErpClient, args: dict[str, Any]) -> Any:
    data = await erp.get("reports/production", _period_params(args))
    batches = [
        {
            "batch": b["batchNumber"], "date": _day(b["productionDate"]), "shift": b["shiftNumber"],
            "operator": b["operatorName"], "inputKg": b["input"], "outputKg": b["output"],
            "wasteKg": b["waste"], "yieldPercent": b["yield"],
        }
        for b in data["batches"]
    ]
    daily = [{**d, "date": _day(d["date"])} for d in data["daily"]]
    return _round({
        "from": _day(data["from"]), "to": _day(data["to"]),
        "totals": data["totals"], "outputsByProductKg": data["outputs"],
        "daily": daily[-31:],
        **_listing(batches, "batches"),
    })


async def supplier_quality(erp: ErpClient, args: dict[str, Any]) -> Any:
    orders, receipts = await asyncio.gather(erp.get("purchaseorders"), erp.get("inboundreceipts"))
    supplier_of_po = {po["poid"]: po["supplierName"] for po in orders}
    supplier = args.get("supplier")

    stats: dict[str, dict[str, Any]] = defaultdict(lambda: {
        "deliveries": 0, "qcApproved": 0, "qcRejected": 0, "netKg": 0.0, "moisture": [], "impurities": [],
    })
    for r in receipts:
        if r["status"] == "Voided" or r.get("receiptType") == "Counted":
            continue
        name = supplier_of_po.get(r["poid"], "Unknown supplier")
        if not _matches(name, supplier):
            continue
        s = stats[name]
        s["deliveries"] += 1
        if r.get("qcResult") == "Approved":
            s["qcApproved"] += 1
        elif r.get("qcResult"):
            s["qcRejected"] += 1
        s["netKg"] += r.get("netWeight") or 0
        if r.get("moisture") is not None:
            s["moisture"].append(r["moisture"])
        if r.get("impurities") is not None:
            s["impurities"].append(r["impurities"])

    def avg(values: list[float]) -> float | None:
        return sum(values) / len(values) if values else None

    rows = []
    for name, s in stats.items():
        checked = s["qcApproved"] + s["qcRejected"]
        rows.append({
            "supplier": name, "deliveries": s["deliveries"], "qcApproved": s["qcApproved"], "qcRejected": s["qcRejected"],
            "passRatePercent": 100 * s["qcApproved"] / checked if checked else None,
            "avgMoisturePercent": avg(s["moisture"]), "avgImpuritiesPercent": avg(s["impurities"]), "totalNetKg": s["netKg"],
        })
    rows.sort(key=lambda r: (-(r["passRatePercent"] or 0), r["avgMoisturePercent"] or 0))
    return _round({
        **_listing(rows, "suppliers"),
        "guide": "Higher pass rate is better; lower moisture and impurities are better. Voided receipts are excluded.",
    })


PO_STATUSES = ["Draft", "Sent", "Received", "Closed", "Cancelled"]
SO_STATUSES = ["Quotation", "Confirmed", "PartiallyDispatched", "Dispatched", "Closed", "Cancelled"]


def _status(value: Any, allowed: list[str]) -> str | None:
    """A real ERP status, or no status filter. The model sent "any" for SO-2026-002 and then found nothing."""
    text = str(value or "").replace(" ", "").lower()
    return next((s for s in allowed if s.lower() == text), None)


async def purchase_orders(erp: ErpClient, args: dict[str, Any]) -> Any:
    orders = await erp.get("purchaseorders")
    supplier, status, number = args.get("supplier"), _status(args.get("status"), PO_STATUSES), args.get("po_number")
    # A PO asked for by number is shown whatever its state: with pending on, "Has PO-2026-001 been fully received?"
    # hid the received order and the answer was "not received".
    pending = str(args.get("pending", "")).lower() in ("true", "1", "yes") and not number
    if pending:
        # Asked for "expected deliveries today", the model filtered by status Received and found none.
        status = None
    rows = [
        {
            "po": po["poNumber"], "supplier": po["supplierName"], "date": _day(po["poDate"]),
            "expectedDelivery": _day(po["expectedDeliveryDate"]), "status": po["status"], "totalAmountPKR": po["totalAmount"],
            "lines": [
                {"product": l["productName"], "ordered": l["quantity"], "received": l["receivedQuantity"],
                 "unit": l["unit"], "rate": l["ratePerUnit"]}
                for l in po["lineItems"]
            ],
        }
        for po in orders
        if (_matches(po["supplierName"], supplier) or _matches(po["poNumber"], supplier))
        and _matches(po["poNumber"], number) and _matches(po["status"], status)
        and (not pending or (po["status"] not in ("Draft", "Closed", "Cancelled")
                             and any(l["receivedQuantity"] < l["quantity"] for l in po["lineItems"])))
    ]
    if pending:
        # Soonest expected delivery first; orders without a date last.
        rows.sort(key=lambda r: r["expectedDelivery"] or "9999")
    else:
        rows.sort(key=lambda r: r["date"] or "", reverse=True)
    extra = {"today": date.today().isoformat(),
             "guide": "These orders are still to be delivered; compare expectedDelivery with today."} if pending else {}
    return _round({
        **_listing(rows, "purchaseOrders"),
        **extra,
        "supplierPayments": "The ERP does not record payments to suppliers; it only tracks what was ordered and received.",
    })


async def sales_orders(erp: ErpClient, args: dict[str, Any]) -> Any:
    # Sales order lines carry no unit; without one the model guesses kg for bagged products.
    orders, products = await asyncio.gather(erp.get("salesorders"), erp.get("products"))
    unit_of = {p["productID"]: p["unit"] for p in products}
    customer, status, number = args.get("customer"), _status(args.get("status"), SO_STATUSES), args.get("so_number")
    rows = [
        {
            "so": so["soNumber"], "customer": so["customerName"], "date": _day(so["soDate"]),
            "expectedDelivery": _day(so["expectedDeliveryDate"]), "status": so["status"], "totalAmountPKR": so["totalAmount"],
            "lines": [
                {"product": l["productName"], "ordered": l["quantity"], "dispatched": l["dispatchedQuantity"],
                 "unit": unit_of.get(l["productID"]), "rate": l["ratePerUnit"]}
                for l in so["lineItems"]
            ],
        }
        for so in orders
        if (_matches(so["customerName"], customer) or _matches(so["soNumber"], customer))
        and _matches(so["soNumber"], number) and _matches(so["status"], status)
    ]
    rows.sort(key=lambda r: r["date"] or "", reverse=True)
    return _round(_listing(rows, "salesOrders"))


async def customer_details(erp: ErpClient, args: dict[str, Any]) -> Any:
    customers = await erp.get("customers")
    name = args.get("customer")
    rows = [
        {
            "customer": c["customerName"], "type": c["type"], "contactPerson": c["contactPerson"],
            "phone": c["phoneNumber"], "address": c["address"], "creditLimitPKR": c["creditLimit"],
            "outstandingBalancePKR": c["outstandingBalance"], "active": c["isActive"],
        }
        for c in customers
        if _matches(c["customerName"], name)
    ]
    return _round(_listing(rows, "customers"))


FILE_FORMATS = {"xlsx": "xlsx", "excel": "xlsx", "sheet": "xlsx", "spreadsheet": "xlsx", "csv": "xlsx",
                "pptx": "pptx", "ppt": "pptx", "powerpoint": "pptx", "slides": "pptx", "presentation": "pptx"}
# Data tools whose report the user may want as a file.
REPORT_OF_TOOL = {"sales_report": "sales", "receivables": "receivables", "current_stock": "stock",
                  "stock_movement_report": "stock", "production_report": "production"}


def redirect_file_request(name: str, args: dict[str, Any]) -> tuple[str, dict[str, Any]]:
    """Asked for "this month's sales report in Excel", the model sometimes called sales_report with a format argument
    instead of create_report, then said the file was ready when none was made. A data tool called with a file format
    is turned into the create_report call the user asked for."""
    fmt = FILE_FORMATS.get(str(args.get("format", "")).strip().lower())
    if name not in REPORT_OF_TOOL or fmt is None:
        return name, args
    dates = {k: args[k] for k in ("period", "from_date", "to_date") if k in args}
    return "create_report", {"report_type": REPORT_OF_TOOL[name], "format": fmt, **dates}


async def create_report(erp: ErpClient, args: dict[str, Any], context: ToolContext | None) -> Any:
    from .reports.service import ReportError  # here, not at the top: the report service imports this module

    if context is None:
        return {"error": "Reports cannot be made here."}
    try:
        report = await context.reports.create(
            context.user, erp, str(args.get("report_type", "")),
            FILE_FORMATS.get(str(args.get("format") or "xlsx").strip().lower(), str(args.get("format"))),
            period=args.get("period"), from_date=args.get("from_date"), to_date=args.get("to_date"))
    except ReportError as exc:
        if exc.status == 401:
            raise ErpError(401, exc.message) from exc
        return {"error": exc.message}
    return {"_report": report, "fileName": report["fileName"],
            "note": "The app shows a download link under your answer. Say in one short sentence that the report is ready."}


TOOLS: dict[str, Tool] = {t.name: t for t in [
    Tool(
        "business_overview",
        "Today's snapshot for the owner: wheat stock (kg), flour stock (bags), today's milling and yield, "
        "pending purchase orders, sales today and this month, total outstanding receivables.",
        _params(), business_overview,
    ),
    Tool(
        "receivables",
        "Unpaid customer invoices as of today, with balance and days overdue, aged into NotDue, 1-30, 31-60 and 61+ days. "
        "Use for: who owes money, overdue payments, a customer's unpaid balance.",
        _params({"customer": {"type": "string", "description": "Optional part of a customer name to filter by."}}),
        receivables, table="openInvoices",
    ),
    Tool(
        "sales_report",
        "Invoiced sales for a date range: totals, paid and balance, by customer, by product, and the invoice list. "
        "Answers in chat; for an Excel or PowerPoint file use create_report.",
        _params({**_PERIOD, "customer": {"type": "string", "description": "Optional part of a customer name."}}),
        sales_report, table="invoices",
    ),
    Tool(
        "current_stock",
        "Stock on hand right now by product and warehouse, with lot numbers. Use for: how much wheat/atta/bran do we have. "
        "Raw wheat is category Raw Material; wheat bran is a by-product and is not wheat.",
        _params({
            "product": {"type": "string", "description": "Optional part of a product name or code, e.g. wheat, atta, bran."},
            "warehouse": {"type": "string", "description": "Optional part of a warehouse name."},
        }),
        current_stock, table="stock",
    ),
    Tool(
        "stock_movement_report",
        "Stock ledger summary for a date range per product: opening, received, produced, used, dispatched, closing. "
        "Answers in chat; for an Excel or PowerPoint file use create_report.",
        _params({**_PERIOD, "product": {"type": "string", "description": "Optional part of a product name or code."}}),
        stock_movement_report, table="rows",
    ),
    Tool(
        "production_report",
        "Completed milling batches for a date range (answers in chat; for a file use create_report): input, output, "
        "waste and yield percent in total, per day and per batch, "
        "plus output by product.",
        _params(_PERIOD), production_report, table="batches",
    ),
    Tool(
        "supplier_quality",
        "Wheat delivery quality per supplier: deliveries, QC approved/rejected, pass rate, average moisture and impurities.",
        _params({"supplier": {"type": "string", "description": "Optional part of a supplier name."}}),
        supplier_quality, table="suppliers",
    ),
    Tool(
        "purchase_orders",
        "Purchase orders to suppliers with status, expected delivery date and ordered vs received quantities. "
        "Use for expected or pending deliveries of wheat and packaging, and to check whether a PO has been received: "
        "compare ordered with received on each line.",
        _params({
            "po_number": {"type": "string", "description": "Optional PO number, e.g. PO-2026-001."},
            "supplier": {"type": "string", "description": "Optional part of a supplier name."},
            "status": {"type": "string", "description": "Only when the user asks for a status: Draft, Sent, Received, Closed or Cancelled."},
            "pending": {"type": "boolean", "description": "true for expected, pending or upcoming deliveries: orders sent to suppliers and not yet fully received."},
        }),
        purchase_orders, table="purchaseOrders",
    ),
    Tool(
        "sales_orders",
        "Customer sales orders with status and ordered vs dispatched quantities.",
        _params({
            "so_number": {"type": "string", "description": "Optional sales order number, e.g. SO-2026-001."},
            "customer": {"type": "string", "description": "Optional part of a customer name."},
            "status": {"type": "string", "description": "Only when the user asks for a status: Quotation, Confirmed, PartiallyDispatched, Dispatched, Closed or Cancelled."},
        }),
        sales_orders, table="salesOrders",
    ),
    Tool(
        "customer_details",
        "Customer contact details, type, credit limit and outstanding balance.",
        _params({"customer": {"type": "string", "description": "Optional part of a customer name."}}),
        customer_details, table="customers",
    ),
    Tool(
        "create_report",
        "Make a downloadable report file: sales, receivables, stock or production, as Excel (xlsx) or PowerPoint (pptx). "
        "Use only when the user asks for a report, file, Excel, sheet, PowerPoint, slides or presentation.",
        _params({
            "report_type": {"type": "string", "enum": ["sales", "receivables", "stock", "production"]},
            "format": {"type": "string", "enum": ["xlsx", "pptx"],
                       "description": "xlsx for Excel or a sheet, pptx for PowerPoint, slides or a presentation. Default xlsx."},
            **_PERIOD,
        }),
        create_report, wants_context=True,
    ),
]}

TOOL_SCHEMAS = [t.schema() for t in TOOLS.values()]


async def run_tool(name: str, args: dict[str, Any], erp: ErpClient, context: ToolContext | None = None) -> ToolOutput:
    """Run a tool and return its result, or an {"error": ...} the model can explain to the user.

    A 401 is re-raised: the user must sign in again, which the model cannot fix.
    """
    tool = TOOLS.get(name)
    if tool is None:
        return ToolOutput({"error": f"Unknown tool {name!r}. Available tools: {', '.join(TOOLS)}."})
    # The model sometimes sends unused filters as null.
    args = {k: v for k, v in (args or {}).items() if v not in (None, "")}
    try:
        result = await (tool.run(erp, args, context) if tool.wants_context else tool.run(erp, args))
        report = result.pop("_report", None) if isinstance(result, dict) else None
        output = split(tool, result)
        output.report = report
        return output
    except ToolInputError as exc:
        return ToolOutput({"error": str(exc)})
    except ErpError as exc:
        if exc.status == 401:
            raise
        return ToolOutput({"error": exc.message})
