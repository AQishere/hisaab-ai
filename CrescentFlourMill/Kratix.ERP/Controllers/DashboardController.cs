using Microsoft.AspNetCore.Authorization;
using Kratix.ERP.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Kratix.ERP.Data;
using Kratix.ERP.DTOs;

namespace Kratix.ERP.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _context;

    public DashboardController(AppDbContext context)
    {
        _context = context;
    }

    // Kept as is: the AI agent and scripts/verify-workflows.cjs read this endpoint.
    [HttpGet("metrics")]
    [Authorize(Policy = Policies.Management)]
    public async Task<ActionResult<DashboardMetricsDto>> GetMetrics()
    {
        try
        {
            // Get wheat stock (raw materials in inventory)
            var wheatStock = await _context.Inventories
                .AsNoTracking()
                .Where(i => i.Product!.Category == "Raw Material")
                .SumAsync(i => (decimal?)i.QuantityAvailable) ?? 0;

            // Get flour stock (finished goods)
            var flourStock = await _context.Inventories
                .AsNoTracking()
                .Where(i => i.StockForm == "Standard" && i.Product!.Category == "Finished Good" && i.Product.Unit == "bag")
                .SumAsync(i => (decimal?)i.QuantityAvailable) ?? 0;

            // Milling is wheat consumed by completed batches, measured in kg.
            var todayMilling = await _context.ProductionBatches
                .AsNoTracking()
                .Where(b => b.ProductionDate.Date == DateTime.Today && b.Status == "Completed")
                .SumAsync(b => (decimal?)b.TotalInputQuantity) ?? 0;

            // Get today's yield (average yield from today's batches)
            var todayYield = 0m;
            var todayBatches = await _context.ProductionBatches
                .AsNoTracking()
                .Where(b => b.ProductionDate.Date == DateTime.Today && b.Status == "Completed")
                .ToListAsync();

            var totalInput = todayBatches.Sum(b => b.TotalInputQuantity);
            if (totalInput > 0)
            {
                todayYield = todayBatches.Sum(b => b.TotalOutputQuantity) / totalInput * 100;
            }

            // Get pending purchase orders
            var pendingPOs = await _context.PurchaseOrders
                .AsNoTracking()
                .Where(p => p.Status == "Draft" || p.Status == "Sent")
                .CountAsync();

            // Get sales today
            var salesToday = await _context.SalesOrders
                .AsNoTracking()
                .Where(s => s.SODate.Date == DateTime.Today && s.Status != "Cancelled")
                .SumAsync(s => (decimal?)s.TotalAmount) ?? 0;

            var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var salesThisMonth = await _context.SalesOrders
                .AsNoTracking()
                .Where(s => s.SODate >= monthStart && s.SODate < monthStart.AddMonths(1) && s.Status != "Cancelled")
                .SumAsync(s => (decimal?)s.TotalAmount) ?? 0;

            var invoicedOpen = await _context.Invoices
                .AsNoTracking()
                .Where(i => i.Status != "Paid")
                .SumAsync(i => (decimal?)i.NetAmount) ?? 0;
            var paidOnOpen = await _context.Payments
                .AsNoTracking()
                .Where(p => p.Invoice!.Status != "Paid")
                .SumAsync(p => (decimal?)p.PaymentAmount) ?? 0;
            var outstandingReceivables = invoicedOpen - paidOnOpen;

            var metrics = new DashboardMetricsDto
            {
                WheatStockKg = wheatStock,
                FlourStockBags = (int)flourStock,
                TodayMillingKg = todayMilling,
                TodayYieldPercent = todayYield,
                PendingPOs = pendingPOs,
                SalesToday = salesToday,
                SalesThisMonth = salesThisMonth,
                OutstandingReceivables = outstandingReceivables,
            };

            return Ok(metrics);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // The role dashboard. Every signed-in role gets one; each section is included only when the role may see
    // the pages behind it (same split as Auth/Roles.cs and ReportsController): floor roles get production,
    // stock and wheat; purchasing roles also get open purchase orders; sales/finance roles get sales and money.
    private static readonly TimeSpan OverviewMaxAge = TimeSpan.FromMinutes(2);
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(bool, bool, bool), (long Version, DashboardOverviewDto Dto)> OverviewCache = new();

    [HttpGet("overview")]
    [Authorize]
    public async Task<ActionResult<DashboardOverviewDto>> GetOverview()
    {
        var floor = User.IsInRole(Roles.Admin) || User.IsInRole(Roles.Owner) || User.IsInRole(Roles.Operations) || User.IsInRole(Roles.Worker);
        var purchasing = User.IsInRole(Roles.Admin) || User.IsInRole(Roles.Owner) || User.IsInRole(Roles.Operations);
        var salesFinance = User.IsInRole(Roles.Admin) || User.IsInRole(Roles.Owner) || User.IsInRole(Roles.Sales);

        // About 25 queries; reused until the next business write, or for OverviewMaxAge so "today" and overdue
        // counts move on. Keyed by what the role may see, so no role gets another role's sections.
        var key = (floor, purchasing, salesFinance);
        var version = DataVersion.Current;
        if (OverviewCache.TryGetValue(key, out var hit) && hit.Version == version && DateTime.Now - hit.Dto.AsOf < OverviewMaxAge)
            return Ok(hit.Dto);

        var today = DateTime.Today;
        var dto = new DashboardOverviewDto { AsOf = DateTime.Now };

        if (salesFinance)
        {
            dto.Sales = await BuildSales(today);
            dto.Receivables = await BuildReceivables(today);
            await AddSalesAttention(dto.Attention, dto.Checks, today);
        }
        if (floor)
        {
            dto.Production = await BuildProduction(today);
            dto.Stock = await BuildStock(today, purchasing);
            dto.WheatQuality = await BuildWheatQuality(today);
            await AddFloorAttention(dto.Attention, dto.Checks, purchasing, today);
        }

        dto.Kpis = BuildKpis(dto);
        OverviewCache[key] = (version, dto);
        dto.Activity = await BuildActivity(floor, purchasing, salesFinance);
        return Ok(dto);
    }

    // ---------- sections ----------

    // Sales are sales-order value by order date, the same measure as "Sales this month" on /metrics.
    private async Task<SalesSection> BuildSales(DateTime today)
    {
        var from = today.AddDays(-59);
        var orders = await _context.SalesOrders.AsNoTracking()
            .Where(s => s.SODate >= from && s.SODate < today.AddDays(1) && s.Status != "Cancelled")
            .GroupBy(s => s.SODate.Date)
            .Select(g => new { Date = g.Key, Amount = g.Sum(s => s.TotalAmount), Orders = g.Count() })
            .ToListAsync();
        var byDay = orders.ToDictionary(o => o.Date);

        var days = Enumerable.Range(0, 30).Select(i => today.AddDays(i - 29))
            .Select(d => new DailyAmount(d, byDay.TryGetValue(d, out var o) ? o.Amount : 0, byDay.TryGetValue(d, out var c) ? c.Orders : 0))
            .ToList();

        var monthStart = new DateTime(today.Year, today.Month, 1);
        var lastMonthStart = monthStart.AddMonths(-1);
        // Month to date against the same number of days last month, so the comparison is fair on the 6th as on the 28th.
        var lastMonthSameDay = lastMonthStart.AddDays(Math.Min(today.Day, DateTime.DaysInMonth(lastMonthStart.Year, lastMonthStart.Month)));
        var monthToDate = await SalesBetween(monthStart, today.AddDays(1));
        var lastMonthToDate = await SalesBetween(lastMonthStart, lastMonthSameDay);

        return new SalesSection
        {
            Days = days,
            Last30Days = days.Sum(d => d.Amount),
            Previous30Days = orders.Where(o => o.Date < today.AddDays(-29)).Sum(o => o.Amount),
            MonthToDate = monthToDate,
            LastMonthToDate = lastMonthToDate,
        };
    }

    private async Task<decimal> SalesBetween(DateTime start, DateTime end) =>
        await _context.SalesOrders.AsNoTracking()
            .Where(s => s.SODate >= start && s.SODate < end && s.Status != "Cancelled")
            .SumAsync(s => (decimal?)s.TotalAmount) ?? 0;

    // Same ageing rule as GET /api/reports/receivables: days past the due date (or the invoice date when there is none).
    private async Task<ReceivablesSection> BuildReceivables(DateTime today)
    {
        var open = (await _context.Invoices.AsNoTracking()
                .Select(i => new
                {
                    i.InvoiceNumber, i.InvoiceDate, i.DueDate, CustomerName = i.Customer!.CustomerName,
                    Balance = i.NetAmount - (i.Payments.Sum(p => (decimal?)p.PaymentAmount) ?? 0),
                })
                .Where(i => i.Balance > 0)
                .ToListAsync())
            .Select(i => new { i.CustomerName, i.Balance, Days = (today - (i.DueDate ?? i.InvoiceDate).Date).Days })
            .ToList();

        decimal Bucket(Func<int, bool> match) => open.Where(i => match(i.Days)).Sum(i => i.Balance);
        return new ReceivablesSection
        {
            NotDue = Bucket(d => d <= 0),
            Days1To30 = Bucket(d => d is > 0 and <= 30),
            Days31To60 = Bucket(d => d is > 30 and <= 60),
            Days61Plus = Bucket(d => d > 60),
            Total = open.Sum(i => i.Balance),
            TopDebtors = open.GroupBy(i => i.CustomerName)
                .Select(g => new Debtor(g.Key, g.Sum(i => i.Balance), g.Count(), Math.Max(0, g.Max(i => i.Days))))
                .OrderByDescending(d => d.Balance).Take(5).ToList(),
        };
    }

    private async Task<ProductionSection> BuildProduction(DateTime today)
    {
        var from = today.AddDays(-29);
        var batches = await _context.ProductionBatches.AsNoTracking()
            .Where(b => b.Status == "Completed" && b.ProductionDate >= from && b.ProductionDate < today.AddDays(1))
            .Select(b => new { Date = b.ProductionDate.Date, b.TotalInputQuantity, b.TotalOutputQuantity })
            .ToListAsync();

        static decimal? YieldOf(decimal output, decimal input) => input == 0 ? null : decimal.Round(output / input * 100, 2);
        decimal? YieldSince(DateTime start)
        {
            var set = batches.Where(b => b.Date >= start).ToList();
            return YieldOf(set.Sum(b => b.TotalOutputQuantity), set.Sum(b => b.TotalInputQuantity));
        }

        var byDay = batches.GroupBy(b => b.Date).ToDictionary(g => g.Key);
        var days = Enumerable.Range(0, 14).Select(i => today.AddDays(i - 13)).Select(d =>
        {
            var input = byDay.TryGetValue(d, out var g) ? g.Sum(b => b.TotalInputQuantity) : 0;
            var output = g?.Sum(b => b.TotalOutputQuantity) ?? 0;
            return new DailyMilling(d, input, output, YieldOf(output, input), g?.Count() ?? 0);
        }).ToList();

        return new ProductionSection
        {
            Days = days,
            MilledLast7Days = batches.Where(b => b.Date > today.AddDays(-7)).Sum(b => b.TotalInputQuantity),
            MilledLast30Days = batches.Sum(b => b.TotalInputQuantity),
            YieldToday = YieldSince(today),
            YieldLast7Days = YieldSince(today.AddDays(-6)),
            YieldLast30Days = YieldSince(from),
        };
    }

    // Days of stock left = bags on hand / average bags dispatched per day over the last 30 days.
    private async Task<StockSection> BuildStock(DateTime today, bool purchasing)
    {
        var onHand = await _context.Inventories.AsNoTracking()
            .Where(i => i.QuantityAvailable > 0)
            .GroupBy(i => new { i.ProductID, i.Product!.ProductName, i.Product.Category, i.Product.Unit, i.StockForm })
            .Select(g => new { g.Key.ProductID, g.Key.ProductName, g.Key.Category, g.Key.Unit, g.Key.StockForm, Quantity = g.Sum(i => i.QuantityAvailable) })
            .ToListAsync();

        var since = today.AddDays(-29);
        var dispatched = await _context.StockMovements.AsNoTracking()
            .Where(m => m.MovementType == "Dispatch" && m.RecordedAt >= since)
            .GroupBy(m => m.Inventory.ProductID)
            .Select(g => new { ProductID = g.Key, Quantity = -g.Sum(m => m.QuantityChange) })
            .ToDictionaryAsync(g => g.ProductID, g => g.Quantity);

        var products = onHand
            .Where(p => p.StockForm == "Standard" && (p.Category == "Finished Good" || p.Category == "By-product"))
            .Select(p =>
            {
                var perDay = dispatched.TryGetValue(p.ProductID, out var q) ? decimal.Round(q / 30m, 2) : 0;
                int? daysLeft = perDay > 0 ? (int)Math.Floor(p.Quantity / perDay) : null;
                return new ProductStock(p.ProductName, p.Category ?? "", p.Unit ?? "", p.Quantity, perDay, daysLeft);
            })
            .OrderBy(p => p.Category == "By-product").ThenByDescending(p => p.Quantity)
            .ToList();

        var section = new StockSection
        {
            Products = products,
            WheatKg = onHand.Where(p => p.Category == "Raw Material").Sum(p => p.Quantity),
            BulkKg = onHand.Where(p => p.StockForm == "Bulk").Sum(p => p.Quantity),
            FlourBags = onHand.Where(p => p.StockForm == "Standard" && p.Category == "Finished Good" && p.Unit == "bag").Sum(p => p.Quantity),
        };

        if (purchasing)
        {
            // Wheat still to come on purchase orders that have been sent to the supplier.
            var open = await _context.POLineItems.AsNoTracking()
                .Where(l => l.PurchaseOrder!.Status == "Sent" && l.Product!.Category == "Raw Material" && l.Quantity > l.ReceivedQuantity)
                .Select(l => new { l.POID, Remaining = l.Quantity - l.ReceivedQuantity })
                .ToListAsync();
            section.WheatExpectedKg = open.Sum(l => l.Remaining);
            section.WheatExpectedOrders = open.Select(l => l.POID).Distinct().Count();
        }
        return section;
    }

    // Weighed wheat deliveries checked at QC in the last 30 days.
    private async Task<WheatQualitySection> BuildWheatQuality(DateTime today)
    {
        var since = today.AddDays(-29);
        var checkedLoads = await _context.InboundReceipts.AsNoTracking()
            .Where(r => r.ReceiptType == "Weighed" && r.QCResult != null && (r.ArrivalDateTime ?? r.CreatedDate) >= since)
            .Select(r => new { Supplier = r.PurchaseOrder!.Supplier!.SupplierName, r.QCResult, r.Moisture, r.Impurities, r.NetWeight })
            .ToListAsync();

        static decimal? Avg(IEnumerable<decimal?> values)
        {
            var list = values.Where(v => v.HasValue).Select(v => v!.Value).ToList();
            return list.Count == 0 ? null : decimal.Round(list.Average(), 2);
        }

        return new WheatQualitySection
        {
            Loads = checkedLoads.Count,
            Rejected = checkedLoads.Count(r => r.QCResult == "Rejected"),
            AvgMoisture = Avg(checkedLoads.Select(r => r.Moisture)),
            AvgImpurities = Avg(checkedLoads.Select(r => r.Impurities)),
            BySupplier = checkedLoads.GroupBy(r => r.Supplier)
                .Select(g => new SupplierQuality(g.Key, g.Count(), g.Count(r => r.QCResult == "Rejected"),
                    Avg(g.Select(r => r.Moisture)), Avg(g.Select(r => r.Impurities)), g.Sum(r => r.NetWeight ?? 0)))
                .OrderByDescending(s => s.Loads).ToList(),
        };
    }

    // ---------- needs attention ----------

    private async Task AddSalesAttention(List<AttentionItem> items, List<AttentionCheck> checks, DateTime today)
    {
        checks.AddRange([new("overdue-invoices", "Overdue invoices"), new("over-credit", "Credit limits"), new("to-dispatch", "Orders to dispatch")]);
        var overdue = await _context.Invoices.AsNoTracking()
            .Select(i => new { Due = i.DueDate ?? i.InvoiceDate, Balance = i.NetAmount - (i.Payments.Sum(p => (decimal?)p.PaymentAmount) ?? 0) })
            .Where(i => i.Balance > 0 && i.Due < today)
            .ToListAsync();
        if (overdue.Count > 0)
            items.Add(new AttentionItem("overdue-invoices", "error", Plural(overdue.Count, "invoice") + " overdue",
                "Past their due date and not fully paid", overdue.Count, overdue.Sum(i => i.Balance), "/invoices"));

        var overCredit = await _context.Customers.AsNoTracking()
            .Where(c => c.IsActive && c.CreditLimit > 0 && c.OutstandingBalance > c.CreditLimit)
            .Select(c => c.CustomerName)
            .ToListAsync();
        if (overCredit.Count > 0)
            items.Add(new AttentionItem("over-credit", "warning", Plural(overCredit.Count, "customer") + " over credit limit",
                string.Join(", ", overCredit.Take(3)) + (overCredit.Count > 3 ? " and more" : ""), overCredit.Count, null, "/customers"));

        var toDispatch = await _context.SalesOrders.AsNoTracking()
            .Where(s => s.Status == "Confirmed" || s.Status == "PartiallyDispatched")
            .Select(s => new { Late = s.ExpectedDeliveryDate != null && s.ExpectedDeliveryDate < today })
            .ToListAsync();
        if (toDispatch.Count > 0)
        {
            var late = toDispatch.Count(s => s.Late);
            items.Add(new AttentionItem("to-dispatch", late > 0 ? "warning" : "info", Plural(toDispatch.Count, "order") + " waiting for dispatch",
                late > 0 ? $"{late} past the expected delivery date" : "Confirmed and not yet fully dispatched", toDispatch.Count, null, "/sales-orders"));
        }
    }

    private async Task AddFloorAttention(List<AttentionItem> items, List<AttentionCheck> checks, bool purchasing, DateTime today)
    {
        checks.AddRange([new("trucks-waiting", "Receiving line"), new("batches-open", "Open batches"),
            new("outputs-approval", "Output approvals"), new("bulk-to-pack", "Packing")]);
        if (purchasing)
            checks.Add(new("late-pos", "Purchase order deliveries"));
        var waiting = await _context.InboundReceipts.AsNoTracking()
            .Where(r => r.Status == "GateEntry" || r.Status == "Weighed" || r.Status == "QCApproved")
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Status, g => g.Count);
        var trucks = waiting.Values.Sum();
        if (trucks > 0)
        {
            var parts = new List<string>();
            if (waiting.TryGetValue("GateEntry", out var a)) parts.Add($"{a} to weigh");
            if (waiting.TryGetValue("Weighed", out var b)) parts.Add($"{b} awaiting QC");
            if (waiting.TryGetValue("QCApproved", out var c)) parts.Add($"{c} to receive into stock");
            items.Add(new AttentionItem("trucks-waiting", "warning", Plural(trucks, "truck") + " in the receiving line",
                string.Join(", ", parts), trucks, null, "/inbound-receipts"));
        }

        var running = await _context.ProductionBatches.AsNoTracking()
            .CountAsync(b => b.Status == "InProgress" || b.Status == "Planned" || b.Status == "OnHold");
        if (running > 0)
            items.Add(new AttentionItem("batches-open", "info", Plural(running, "batch", "batches") + " not completed",
                "Planned, in progress or on hold", running, null, "/production"));

        var unapproved = await _context.BatchMaterials.AsNoTracking()
            .CountAsync(m => m.MaterialType == "Output" && !m.IsReversed && !m.IsQualityApproved && m.ProductionBatch!.Status != "Completed");
        if (unapproved > 0)
            items.Add(new AttentionItem("outputs-approval", "warning", Plural(unapproved, "output") + " awaiting approval",
                "Batch outputs need a quality approval before completion", unapproved, null, "/production"));

        var bulkKg = await _context.Inventories.AsNoTracking()
            .Where(i => i.StockForm == "Bulk" && i.QuantityAvailable > 0)
            .SumAsync(i => (decimal?)i.QuantityAvailable) ?? 0;
        if (bulkKg > 0)
            items.Add(new AttentionItem("bulk-to-pack", "info", $"{bulkKg:#,0} kg waiting to be packed",
                "Milled flour still in bulk, not yet in bags", null, bulkKg, "/inventory"));

        if (purchasing)
        {
            var latePOs = await _context.PurchaseOrders.AsNoTracking()
                .CountAsync(p => p.Status == "Sent" && p.ExpectedDeliveryDate != null && p.ExpectedDeliveryDate < today);
            if (latePOs > 0)
                items.Add(new AttentionItem("late-pos", "warning", Plural(latePOs, "purchase order") + " late",
                    "Past the expected delivery date and not fully received", latePOs, null, "/purchase-orders"));
        }
    }

    private static string Plural(int count, string one, string? many = null) => $"{count} {(count == 1 ? one : many ?? one + "s")}";

    // ---------- headline numbers, built from the sections the role can see ----------

    private static List<Kpi> BuildKpis(DashboardOverviewDto d)
    {
        var kpis = new List<Kpi>();
        if (d.Sales is { } s)
            kpis.Add(new Kpi("sales-month", "Sales this month", s.MonthToDate, "money", "Month to date",
                Change(s.MonthToDate, s.LastMonthToDate), "vs same days last month"));
        if (d.Receivables is { } r)
            kpis.Add(new Kpi("receivables", "Outstanding receivables", r.Total, "money",
                r.Total == 0 ? "Nothing owed" : $"PKR {r.Days1To30 + r.Days31To60 + r.Days61Plus:#,0} overdue", null, null));
        if (d.Stock is { } st)
        {
            kpis.Add(new Kpi("flour-stock", "Flour stock", st.FlourBags, "bags", "Packed finished goods", null, null));
            kpis.Add(new Kpi("wheat-stock", "Wheat stock", st.WheatKg, "kg",
                st.WheatExpectedKg is > 0 ? $"{st.WheatExpectedKg:#,0} kg more on order" : "Raw material on hand", null, null));
        }
        if (d.Production is { } p)
        {
            kpis.Add(new Kpi("milled-7d", "Milled, last 7 days", p.MilledLast7Days, "kg", $"{p.MilledLast30Days:#,0} kg in 30 days", null, null));
            kpis.Add(new Kpi("yield-7d", "Yield, last 7 days", p.YieldLast7Days, "percent",
                p.YieldLast30Days is { } y30 ? $"30-day average {y30:0.0}%" : "No completed batches in 30 days",
                p.YieldLast7Days is { } y7 && p.YieldLast30Days is { } avg ? decimal.Round(y7 - avg, 2) : null, "vs 30-day average"));
        }
        return kpis;
    }

    private static decimal? Change(decimal now, decimal before) => before == 0 ? null : decimal.Round((now - before) / before * 100, 1);

    // ---------- recent activity, limited to what the role can see ----------

    private async Task<List<ActivityItem>> BuildActivity(bool floor, bool purchasing, bool salesFinance)
    {
        const int take = 12;
        var items = new List<ActivityItem>();

        if (floor)
        {
            var received = await _context.InboundReceipts.AsNoTracking()
                .Where(r => r.Status == "Received" && r.ReceivedDate != null)
                .OrderByDescending(r => r.ReceivedDate).Take(take)
                .Select(r => new { r.ReceivedDate, r.ReceiptNumber, r.ReceiptType, r.NetWeight, r.ReceivedQuantity,
                    Product = r.POLineItem!.Product!.ProductName, Supplier = r.PurchaseOrder!.Supplier!.SupplierName })
                .ToListAsync();
            items.AddRange(received.Select(r => new ActivityItem(r.ReceivedDate!.Value, "receipt",
                $"{r.Product ?? "Delivery"} received",
                r.ReceiptType == "Weighed" ? $"{r.NetWeight ?? r.ReceivedQuantity:#,0} kg from {r.Supplier} · {r.ReceiptNumber}" : $"{r.ReceivedQuantity:#,0} pieces from {r.Supplier} · {r.ReceiptNumber}",
                "/inbound-receipts")));

            var completed = await _context.ProductionBatches.AsNoTracking()
                .Where(b => b.Status == "Completed")
                .OrderByDescending(b => b.ProductionDate).ThenByDescending(b => b.BatchID).Take(take)
                .Select(b => new { b.BatchNumber, b.ProductionDate, b.TotalInputQuantity, b.YieldPercentage })
                .ToListAsync();
            var numbers = completed.Select(b => b.BatchNumber).ToList();
            var outputAt = await _context.StockMovements.AsNoTracking()
                .Where(m => m.MovementType == "ProductionOutput" && numbers.Contains(m.Reference))
                .GroupBy(m => m.Reference)
                .Select(g => new { Reference = g.Key, At = g.Max(m => m.RecordedAt) })
                .ToDictionaryAsync(g => g.Reference, g => g.At);
            items.AddRange(completed.Select(b => new ActivityItem(outputAt.TryGetValue(b.BatchNumber, out var at) ? at : b.ProductionDate,
                "batch", $"Batch {b.BatchNumber} completed", $"{b.TotalInputQuantity:#,0} kg milled at {b.YieldPercentage:0.#}% yield", "/production")));
        }

        if (purchasing)
        {
            var orders = await _context.PurchaseOrders.AsNoTracking()
                .Where(p => p.Status != "Draft")
                .OrderByDescending(p => p.CreatedDate).Take(take)
                .Select(p => new { p.CreatedDate, p.PONumber, Supplier = p.Supplier!.SupplierName, Total = p.POLineItems.Sum(l => l.TotalAmount) })
                .ToListAsync();
            items.AddRange(orders.Select(p => new ActivityItem(p.CreatedDate, "purchase", $"Purchase order {p.PONumber}",
                $"PKR {p.Total:#,0} to {p.Supplier}", "/purchase-orders")));
        }

        if (salesFinance)
        {
            var dispatches = await _context.Dispatches.AsNoTracking()
                .OrderByDescending(d => d.DispatchedAt).Take(take)
                .Select(d => new { d.DispatchedAt, d.TruckNumber, d.SalesOrder.SONumber, Customer = d.SalesOrder.Customer!.CustomerName })
                .ToListAsync();
            items.AddRange(dispatches.Select(d => new ActivityItem(d.DispatchedAt, "dispatch", $"Truck {d.TruckNumber} dispatched",
                $"{d.SONumber} for {d.Customer}", "/sales-orders")));

            var invoices = await _context.Invoices.AsNoTracking()
                .OrderByDescending(i => i.CreatedDate).Take(take)
                .Select(i => new { i.CreatedDate, i.InvoiceNumber, i.NetAmount, Customer = i.Customer!.CustomerName })
                .ToListAsync();
            items.AddRange(invoices.Select(i => new ActivityItem(i.CreatedDate, "invoice", $"Invoice {i.InvoiceNumber} issued",
                $"PKR {i.NetAmount:#,0} to {i.Customer}", "/invoices")));

            var payments = await _context.Payments.AsNoTracking()
                .OrderByDescending(p => p.CreatedDate).Take(take)
                .Select(p => new { p.CreatedDate, p.PaymentAmount, p.PaymentMode, p.Invoice!.InvoiceNumber, Customer = p.Invoice.Customer!.CustomerName })
                .ToListAsync();
            items.AddRange(payments.Select(p => new ActivityItem(p.CreatedDate, "payment", $"PKR {p.PaymentAmount:#,0} received",
                $"{p.Customer} · {p.InvoiceNumber} · {p.PaymentMode}", "/payments")));
        }

        return items.OrderByDescending(i => i.At).Take(take).ToList();
    }
}
