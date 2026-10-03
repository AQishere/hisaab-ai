using Microsoft.AspNetCore.Authorization;
using Kratix.ERP.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Kratix.ERP.Data;
using Kratix.ERP.DTOs;

namespace Kratix.ERP.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Policies.Management)]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _context;

    public DashboardController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("metrics")]
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
                .Where(p => p.Status != "Received" && p.Status != "Cancelled")
                .CountAsync();

            // Get sales today
            var salesToday = await _context.SalesOrders
                .AsNoTracking()
                .Where(s => s.SODate.Date == DateTime.Today)
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
                OutstandingReceivables = outstandingReceivables,
            };

            return Ok(metrics);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
