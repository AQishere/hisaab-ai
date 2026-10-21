using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Kratix.ERP.Auth;
using Kratix.ERP.Data;

namespace Kratix.ERP.Controllers;

// Read-only summaries. Stock and production follow the floor roles; sales and receivables follow sales/finance.
[ApiController]
[Route("api/[controller]")]
public class ReportsController(AppDbContext context) : ControllerBase
{
    // IReadOnlyList, not arrays: array.Contains binds to a span overload that EF Core cannot translate.
    private static readonly IReadOnlyList<string> ProducedTypes = ["ProductionOutput", "PackingOutput", "LegacyBulkOpening"];
    private static readonly IReadOnlyList<string> UsedTypes = ["ProductionInput", "ProductionReversal", "PackingBulk", "PackingSupply"];

    // Opening + received + produced + used + dispatched = closing, per product and stock form, from the stock ledger.
    // Pre-ledger stock (OpeningBalance from the history upgrade) always counts as opening stock.
    [HttpGet("stock")]
    [Authorize(Policy = Policies.Floor)]
    public async Task<ActionResult> Stock(DateTime? from, DateTime? to)
    {
        var (start, end, error) = Period(from, to);
        if (error != null)
            return BadRequest(error);

        var groups = await context.StockMovements
            .Where(m => m.RecordedAt < end)
            .GroupBy(m => new { m.Inventory.ProductID, m.Inventory.StockForm, m.Unit })
            .Select(g => new
            {
                g.Key.ProductID, g.Key.StockForm, g.Key.Unit,
                Opening = g.Sum(m => m.RecordedAt < start || m.MovementType == "OpeningBalance" ? m.QuantityChange : 0),
                Received = g.Sum(m => m.RecordedAt >= start && m.MovementType == "Receipt" ? m.QuantityChange : 0),
                Produced = g.Sum(m => m.RecordedAt >= start && ProducedTypes.Contains(m.MovementType) ? m.QuantityChange : 0),
                Used = g.Sum(m => m.RecordedAt >= start && UsedTypes.Contains(m.MovementType) ? m.QuantityChange : 0),
                Dispatched = g.Sum(m => m.RecordedAt >= start && m.MovementType == "Dispatch" ? m.QuantityChange : 0),
                Closing = g.Sum(m => m.QuantityChange),
            })
            .ToListAsync();

        var products = await context.Products.ToDictionaryAsync(p => p.ProductID);
        var rows = groups
            .Where(g => g.Opening != 0 || g.Closing != 0 || g.Received != 0 || g.Produced != 0 || g.Used != 0 || g.Dispatched != 0)
            .Select(g => new
            {
                products[g.ProductID].ProductCode, products[g.ProductID].ProductName, products[g.ProductID].Category,
                g.StockForm, g.Unit, g.Opening, g.Received, g.Produced, g.Used, g.Dispatched, g.Closing,
            })
            .OrderBy(r => r.Category).ThenBy(r => r.ProductName).ThenBy(r => r.StockForm)
            .ToList();
        return Ok(new { From = start, To = end.AddDays(-1), Rows = rows });
    }

    [HttpGet("production")]
    [Authorize(Policy = Policies.Floor)]
    public async Task<ActionResult> Production(DateTime? from, DateTime? to)
    {
        var (start, end, error) = Period(from, to);
        if (error != null)
            return BadRequest(error);

        var batches = await context.ProductionBatches
            .Where(b => b.Status == "Completed" && b.ProductionDate >= start && b.ProductionDate < end)
            .OrderBy(b => b.ProductionDate).ThenBy(b => b.ShiftNumber)
            .Select(b => new
            {
                b.BatchID, b.BatchNumber, b.ProductionDate, b.ShiftNumber, b.OperatorName,
                Input = b.TotalInputQuantity, Output = b.TotalOutputQuantity, Waste = b.TotalWasteQuantity, Yield = b.YieldPercentage,
            })
            .ToListAsync();
        var batchIds = batches.Select(b => b.BatchID).ToList();
        var outputs = await context.BatchMaterials
            .Where(m => batchIds.Contains(m.BatchID) && !m.IsReversed && m.MaterialType != "Input")
            .GroupBy(m => new { m.Product!.ProductName, m.MaterialType })
            .Select(g => new { g.Key.ProductName, g.Key.MaterialType, QuantityKg = g.Sum(m => m.Quantity) })
            .OrderBy(o => o.MaterialType).ThenByDescending(o => o.QuantityKg)
            .ToListAsync();

        static decimal YieldOf(decimal output, decimal input) => input == 0 ? 0 : decimal.Round(output / input * 100, 2);
        var daily = batches.GroupBy(b => b.ProductionDate.Date).Select(g => new
        {
            Date = g.Key, Batches = g.Count(), Input = g.Sum(b => b.Input), Output = g.Sum(b => b.Output), Waste = g.Sum(b => b.Waste),
            Yield = YieldOf(g.Sum(b => b.Output), g.Sum(b => b.Input)),
        }).ToList();
        var totals = new
        {
            Batches = batches.Count, Input = batches.Sum(b => b.Input), Output = batches.Sum(b => b.Output), Waste = batches.Sum(b => b.Waste),
            Yield = YieldOf(batches.Sum(b => b.Output), batches.Sum(b => b.Input)),
        };
        return Ok(new { From = start, To = end.AddDays(-1), Totals = totals, Daily = daily, Batches = batches, Outputs = outputs });
    }

    // Sales are invoiced sales (what was billed), by invoice date.
    [HttpGet("sales")]
    [Authorize(Policy = Policies.SalesFinance)]
    public async Task<ActionResult> Sales(DateTime? from, DateTime? to)
    {
        var (start, end, error) = Period(from, to);
        if (error != null)
            return BadRequest(error);

        var invoices = await context.Invoices
            .Where(i => i.InvoiceDate >= start && i.InvoiceDate < end)
            .OrderBy(i => i.InvoiceDate).ThenBy(i => i.InvoiceNumber)
            .Select(i => new
            {
                i.InvoiceID, i.InvoiceNumber, i.InvoiceDate, i.CustomerID, CustomerName = i.Customer!.CustomerName, i.Status,
                i.TotalAmount, i.TaxAmount, i.NetAmount, Paid = i.Payments.Sum(p => (decimal?)p.PaymentAmount) ?? 0,
            })
            .ToListAsync();
        var invoiceIds = invoices.Select(i => i.InvoiceID).ToList();
        var byProduct = await context.InvoiceLines
            .Where(l => invoiceIds.Contains(l.InvoiceID))
            .GroupBy(l => new { l.ProductName, l.Unit })
            .Select(g => new { g.Key.ProductName, g.Key.Unit, Quantity = g.Sum(l => l.Quantity), Amount = g.Sum(l => l.TotalAmount) })
            .OrderByDescending(p => p.Amount)
            .ToListAsync();
        var byCustomer = invoices.GroupBy(i => i.CustomerName).Select(g => new
        {
            CustomerName = g.Key, Invoices = g.Count(), Net = g.Sum(i => i.NetAmount), Paid = g.Sum(i => i.Paid), Balance = g.Sum(i => i.NetAmount - i.Paid),
        }).OrderByDescending(c => c.Net).ToList();
        var totals = new
        {
            Invoices = invoices.Count, Subtotal = invoices.Sum(i => i.TotalAmount), Tax = invoices.Sum(i => i.TaxAmount),
            Net = invoices.Sum(i => i.NetAmount), Paid = invoices.Sum(i => i.Paid), Balance = invoices.Sum(i => i.NetAmount - i.Paid),
        };
        return Ok(new
        {
            From = start, To = end.AddDays(-1), Totals = totals, ByCustomer = byCustomer, ByProduct = byProduct,
            Invoices = invoices.Select(i => new { i.InvoiceNumber, i.InvoiceDate, i.CustomerName, i.Status, i.NetAmount, i.Paid, Balance = i.NetAmount - i.Paid }),
        });
    }

    // Unpaid invoices as of today, aged by days past their due date.
    [HttpGet("receivables")]
    [Authorize(Policy = Policies.SalesFinance)]
    public async Task<ActionResult> Receivables()
    {
        var today = DateTime.Today;
        var open = (await context.Invoices
                .Select(i => new
                {
                    i.InvoiceNumber, i.InvoiceDate, i.DueDate, CustomerName = i.Customer!.CustomerName, i.NetAmount,
                    Balance = i.NetAmount - (i.Payments.Sum(p => (decimal?)p.PaymentAmount) ?? 0),
                })
                .Where(i => i.Balance > 0)
                .ToListAsync())
            .Select(i =>
            {
                var daysOverdue = (today - (i.DueDate ?? i.InvoiceDate).Date).Days;
                var bucket = daysOverdue <= 0 ? "NotDue" : daysOverdue <= 30 ? "Days1To30" : daysOverdue <= 60 ? "Days31To60" : "Days61Plus";
                return new AgedInvoice(i.InvoiceNumber, i.InvoiceDate, i.DueDate, i.CustomerName, i.NetAmount, i.Balance, Math.Max(daysOverdue, 0), bucket);
            })
            .OrderByDescending(i => i.DaysOverdue).ThenBy(i => i.InvoiceNumber)
            .ToList();

        var byCustomer = open.GroupBy(i => i.CustomerName)
            .Select(g => new { CustomerName = g.Key, Invoices = g.Count(), Aging = Aging.Of(g) })
            .OrderBy(c => c.CustomerName)
            .ToList();
        return Ok(new { AsOf = today, Totals = Aging.Of(open), ByCustomer = byCustomer, Invoices = open });
    }

    private sealed record AgedInvoice(string InvoiceNumber, DateTime InvoiceDate, DateTime? DueDate, string CustomerName,
        decimal NetAmount, decimal Balance, int DaysOverdue, string Bucket);

    private sealed record Aging(decimal NotDue, decimal Days1To30, decimal Days31To60, decimal Days61Plus, decimal Total)
    {
        public static Aging Of(IEnumerable<AgedInvoice> invoices)
        {
            var list = invoices.ToList();
            decimal Sum(string bucket) => list.Where(i => i.Bucket == bucket).Sum(i => i.Balance);
            return new Aging(Sum("NotDue"), Sum("Days1To30"), Sum("Days31To60"), Sum("Days61Plus"), list.Sum(i => i.Balance));
        }
    }

    private static (DateTime Start, DateTime End, string? Error) Period(DateTime? from, DateTime? to)
    {
        var today = DateTime.Today;
        var start = (from ?? new DateTime(today.Year, today.Month, 1)).Date;
        var last = (to ?? today).Date;
        if (last < start)
            return (start, last, "The end date must be on or after the start date.");
        if ((last - start).TotalDays > 366 * 3)
            return (start, last, "Choose a period of at most three years.");
        return (start, last.AddDays(1), null);
    }
}
