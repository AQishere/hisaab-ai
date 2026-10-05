import asyncio

import pytest

from app.erp_client import ErpError
from datetime import date

from app.tools import MODEL_ROWS, TOOL_SCHEMAS, TOOLS, ToolInputError, resolve_period, run_tool


class FakeErp:
    """Stands in for ErpClient: returns canned JSON per path and records the calls made."""

    def __init__(self, responses: dict, errors: dict | None = None):
        self.responses = responses
        self.errors = errors or {}
        self.calls: list[tuple[str, dict | None]] = []

    async def get(self, path, params=None):
        self.calls.append((path, params))
        if path in self.errors:
            raise self.errors[path]
        return self.responses[path]


def run(coro):
    return asyncio.run(coro)


def call(name, args, erp):
    """What the model reads from a tool."""
    return run(run_tool(name, args, erp)).model


PURCHASE_ORDERS = [
    {"poid": 1, "poNumber": "PO-1", "supplierName": "Sindh Grain Traders", "poDate": "2026-10-01T00:00:00",
     "expectedDeliveryDate": None, "status": "Received", "totalAmount": 2600000.0,
     "lineItems": [{"productName": "Wheat", "quantity": 20000.0, "receivedQuantity": 20000.0, "unit": "kg", "ratePerUnit": 130.0}]},
    {"poid": 2, "poNumber": "PO-2", "supplierName": "Punjab Agro Supplies", "poDate": "2026-10-02T00:00:00",
     "expectedDeliveryDate": None, "status": "Sent", "totalAmount": 100.0, "lineItems": []},
]


def receipt(poid, qc, moisture, status="Received", receipt_type="Weighed", net=1000.0):
    return {"poid": poid, "qcResult": qc, "moisture": moisture, "impurities": 1.0, "status": status,
            "receiptType": receipt_type, "netWeight": net}


def test_every_tool_has_a_valid_schema():
    assert len(TOOL_SCHEMAS) == len(TOOLS) == 11
    for schema in TOOL_SCHEMAS:
        fn = schema["function"]
        assert fn["description"] and fn["parameters"]["type"] == "object"


def test_supplier_quality_aggregates_and_skips_voided_and_counted():
    erp = FakeErp({
        "purchaseorders": PURCHASE_ORDERS,
        "inboundreceipts": [
            receipt(1, "Approved", 10.0), receipt(1, "Rejected", 14.0),
            receipt(1, "Approved", 99.0, status="Voided"), receipt(1, None, None, receipt_type="Counted"),
            receipt(2, "Approved", 11.0),
        ],
    })
    result = call("supplier_quality", {}, erp)
    by_name = {r["supplier"]: r for r in result["suppliers"]}
    sindh = by_name["Sindh Grain Traders"]
    assert (sindh["deliveries"], sindh["qcApproved"], sindh["qcRejected"]) == (2, 1, 1)
    assert sindh["passRatePercent"] == 50.0
    assert sindh["avgMoisturePercent"] == 12.0
    assert result["suppliers"][0]["supplier"] == "Punjab Agro Supplies"  # 100% pass rate sorts first


def test_supplier_filter_is_case_insensitive_partial_match():
    erp = FakeErp({"purchaseorders": PURCHASE_ORDERS,
                   "inboundreceipts": [receipt(1, "Approved", 10.0), receipt(2, "Approved", 11.0)]})
    result = call("supplier_quality", {"supplier": "  sindh "}, erp)
    assert [r["supplier"] for r in result["suppliers"]] == ["Sindh Grain Traders"]


def test_receivables_filters_by_customer_and_hides_company_totals():
    erp = FakeErp({"reports/receivables": {
        "asOf": "2026-10-04T00:00:00",
        "totals": {"total": 500.0},
        "byCustomer": [{"customerName": "Lahore Bakery Group"}, {"customerName": "Ali Sons Retail"}],
        "invoices": [
            {"invoiceNumber": "INV-1", "customerName": "Lahore Bakery Group", "invoiceDate": "2026-09-01T00:00:00",
             "dueDate": "2026-09-30T00:00:00", "netAmount": 300.0, "balance": 300.0, "daysOverdue": 4, "bucket": "Days1To30"},
            {"invoiceNumber": "INV-2", "customerName": "Ali Sons Retail", "invoiceDate": "2026-09-01T00:00:00",
             "dueDate": None, "netAmount": 200.0, "balance": 200.0, "daysOverdue": 0, "bucket": "NotDue"},
        ],
    }})
    result = call("receivables", {"customer": "lahore"}, erp)
    assert "totalsPKR" not in result
    assert [i["invoice"] for i in result["openInvoices"]] == ["INV-1"]
    assert result["openInvoices"][0]["dueDate"] == "2026-09-30"


def test_dates_are_validated_and_passed_to_the_report():
    erp = FakeErp({"reports/production": {"from": "2026-10-01T00:00:00", "to": "2026-10-04T00:00:00",
                                          "totals": {}, "outputs": [], "daily": [], "batches": []}})
    call("production_report", {"from_date": "2026-10-01", "to_date": "2026-10-04"}, erp)
    assert erp.calls == [("reports/production", {"from": "2026-10-01", "to": "2026-10-04"})]

    bad = call("production_report", {"from_date": "last week"}, erp)
    assert "YYYY-MM-DD" in bad["error"]


def test_current_stock_sums_rows_per_product_and_warehouse():
    row = {"productName": "Wheat Grade A", "productCode": "WHT-A", "category": "Raw Material", "warehouseName": "Godown A",
           "unit": "kg"}
    erp = FakeErp({"inventory": [
        {**row, "quantityAvailable": 1000.0, "lotNumber": "L1"},
        {**row, "quantityAvailable": 500.0, "lotNumber": "L2"},
        {**row, "productName": "Bran", "productCode": "BRAN", "quantityAvailable": 0.0, "lotNumber": "L3"},
    ], "products": [
        {"productName": "Wheat Grade A", "productCode": "WHT-A", "category": "Raw Material", "unit": "kg"},
        {"productName": "Wheat Grade B", "productCode": "WHT-B", "category": "Raw Material", "unit": "kg"},
        {"productName": "Bran", "productCode": "BRAN", "category": "By-product", "unit": "bag"},
    ]})
    result = call("current_stock", {"product": "wht"}, erp)
    assert result["stock"] == [
        {"product": "Wheat Grade A", "category": "Raw Material", "warehouse": "Godown A", "unit": "kg",
         "quantity": 1500.0, "lots": ["L1", "L2"]},
        {"product": "Wheat Grade B", "category": "Raw Material", "warehouse": None, "unit": "kg", "quantity": 0.0,
         "lots": []},
    ]


def test_wheat_lists_raw_wheat_before_bran_even_when_out_of_stock():
    erp = FakeErp({"inventory": [
        {"productName": "Wheat Bran", "productCode": "BRAN-40", "category": "By-product", "warehouseName": "FG",
         "unit": "bag", "quantityAvailable": 60.0, "lotNumber": "MB-1"},
    ], "products": [
        {"productName": "Wheat Bran", "productCode": "BRAN-40", "category": "By-product", "unit": "bag"},
        {"productName": "Wheat Grade A", "productCode": "WHT-A", "category": "Raw Material", "unit": "kg"},
    ]})
    result = call("current_stock", {"product": "wheat"}, erp)
    assert [(r["product"], r["quantity"]) for r in result["stock"]] == [("Wheat Grade A", 0.0)]
    assert result["otherMatchingProducts"] == [{"product": "Wheat Bran", "category": "By-product", "quantity": 60.0,
                                                "unit": "bag"}]
    assert "out of stock" in result["guide"]


def test_bran_alone_is_listed_normally():
    erp = FakeErp({"inventory": [
        {"productName": "Wheat Bran", "productCode": "BRAN-40", "category": "By-product", "warehouseName": "FG",
         "unit": "bag", "quantityAvailable": 60.0, "lotNumber": "MB-1"},
    ], "products": [{"productName": "Wheat Bran", "productCode": "BRAN-40", "category": "By-product", "unit": "bag"}]})
    result = call("current_stock", {"product": "bran"}, erp)
    assert [r["product"] for r in result["stock"]] == ["Wheat Bran"] and "otherMatchingProducts" not in result


def test_model_reads_the_first_rows_and_the_app_gets_them_all():
    orders = [{**PURCHASE_ORDERS[1], "poNumber": f"PO-{n}", "poDate": f"2026-01-{n % 28 + 1:02d}T00:00:00"} for n in range(30)]
    output = run(run_tool("purchase_orders", {}, FakeErp({"purchaseorders": orders})))
    assert output.model["count"] == 30 and len(output.model["purchaseOrders"]) == MODEL_ROWS
    assert "all 30" in output.model["note"]
    assert output.data["count"] == 30 and len(output.data["rows"]) == 30 and output.data["tool"] == "purchase_orders"


def test_short_lists_are_not_trimmed_and_errors_have_no_table():
    output = run(run_tool("purchase_orders", {}, FakeErp({"purchaseorders": PURCHASE_ORDERS})))
    assert len(output.model["purchaseOrders"]) == 2 and "note" not in output.model and output.data["count"] == 2
    assert run(run_tool("drop_tables", {}, FakeErp({}))).data is None


def test_null_arguments_from_the_model_are_ignored():
    erp = FakeErp({"purchaseorders": PURCHASE_ORDERS})
    assert call("purchase_orders", {"supplier": None, "po_number": "", "status": None}, erp)["count"] == 2


def test_forbidden_becomes_a_message_but_expired_session_is_raised():
    forbidden = FakeErp({}, {"reports/sales": ErpError(403, "Your role does not have access to this information.")})
    assert "role" in call("sales_report", {}, forbidden)["error"]

    expired = FakeErp({}, {"reports/sales": ErpError(401, "Your session has expired. Sign in again.")})
    with pytest.raises(ErpError):
        call("sales_report", {}, expired)


def test_unknown_tool_returns_error():
    assert "Unknown tool" in call("drop_tables", {}, FakeErp({}))["error"]


def test_purchase_order_found_by_number_in_either_field():
    erp = FakeErp({"purchaseorders": PURCHASE_ORDERS})
    by_number = call("purchase_orders", {"po_number": "po-1"}, erp)
    in_name_field = call("purchase_orders", {"supplier": "PO-1"}, erp)
    assert [r["po"] for r in by_number["purchaseOrders"]] == [r["po"] for r in in_name_field["purchaseOrders"]] == ["PO-1"]


def test_pending_orders_are_the_expected_deliveries():
    orders = [
        {**PURCHASE_ORDERS[0]},  # Received in full
        {**PURCHASE_ORDERS[1], "poNumber": "PO-3", "expectedDeliveryDate": "2026-10-06T00:00:00",
         "lineItems": [{"productName": "Wheat", "quantity": 1000.0, "receivedQuantity": 0.0, "unit": "kg", "ratePerUnit": 130.0}]},
        {**PURCHASE_ORDERS[1], "poNumber": "PO-4", "status": "Draft",
         "lineItems": [{"productName": "Wheat", "quantity": 1000.0, "receivedQuantity": 0.0, "unit": "kg", "ratePerUnit": 130.0}]},
    ]
    result = call("purchase_orders", {"pending": True, "status": "Received"}, FakeErp({"purchaseorders": orders}))
    assert [r["po"] for r in result["purchaseOrders"]] == ["PO-3"] and "expectedDelivery" in result["guide"]
    by_number = call("purchase_orders", {"pending": True, "po_number": "PO-1"}, FakeErp({"purchaseorders": orders}))
    assert [r["po"] for r in by_number["purchaseOrders"]] == ["PO-1"]


def test_empty_result_says_nothing_matched():
    result = call("purchase_orders", {"supplier": "Nobody"}, FakeErp({"purchaseorders": PURCHASE_ORDERS}))
    assert result["count"] == 0 and "do not guess" in result["note"]


def test_sales_order_lines_get_their_unit_from_products():
    erp = FakeErp({
        "salesorders": [{"soNumber": "SO-1", "customerName": "Ali Sons Retail", "soDate": "2026-10-04T00:00:00",
                         "expectedDeliveryDate": None, "status": "Dispatched", "totalAmount": 7840.0,
                         "lineItems": [{"productID": 5, "productName": "Suji 20kg", "quantity": 56.0,
                                        "dispatchedQuantity": 56.0, "ratePerUnit": 140.0}]}],
        "products": [{"productID": 5, "unit": "bag"}],
    })
    result = call("sales_orders", {"so_number": "SO-1"}, erp)
    assert result["salesOrders"][0]["lines"][0]["unit"] == "bag"


@pytest.mark.parametrize("name, expected", [
    ("today", ("2026-10-04", "2026-10-04")),
    ("yesterday", ("2026-10-03", "2026-10-03")),
    ("this_week", ("2026-09-28", "2026-10-04")),      # 4 Oct 2026 is a Sunday; the week starts Monday 28 Sep
    ("last_week", ("2026-09-21", "2026-09-27")),
    ("this_month", ("2026-10-01", "2026-10-04")),
    ("last_month", ("2026-09-01", "2026-09-30")),
    ("this_year", ("2026-01-01", "2026-10-04")),
])
def test_named_periods(name, expected):
    start, end = resolve_period(name, date(2026, 10, 4))
    assert (start.isoformat(), end.isoformat()) == expected


def test_last_month_in_january_is_previous_december():
    assert resolve_period("last_month", date(2027, 1, 15)) == (date(2026, 12, 1), date(2026, 12, 31))


def test_unknown_period_is_an_input_error():
    with pytest.raises(ToolInputError):
        resolve_period("next_decade", date(2026, 10, 4))


def test_a_list_of_products_matches_any_of_them():
    erp = FakeErp({"inventory": [
        {"productName": "Chakki Atta 10kg", "productCode": "ATTA-10", "category": "Finished Good", "warehouseName": "FG",
         "unit": "bag", "quantityAvailable": 700.0, "lotNumber": "MB-1"},
        {"productName": "Wheat Bran 40kg", "productCode": "BRAN-40", "category": "By-product", "warehouseName": "FG",
         "unit": "bag", "quantityAvailable": 60.0, "lotNumber": "MB-1"},
    ], "products": [
        {"productName": "Chakki Atta 10kg", "productCode": "ATTA-10", "category": "Finished Good", "unit": "bag"},
        {"productName": "Wheat Bran 40kg", "productCode": "BRAN-40", "category": "By-product", "unit": "bag"},
    ]})
    result = call("current_stock", {"product": "atta, flour and bran"}, erp)
    assert [r["product"] for r in result["stock"]] == ["Chakki Atta 10kg", "Wheat Bran 40kg"]
