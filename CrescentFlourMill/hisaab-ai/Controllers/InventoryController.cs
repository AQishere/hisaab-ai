using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Hisaab.ERP.Auth;
using Hisaab.ERP.Data;
using Hisaab.ERP.DTOs;

namespace Hisaab.ERP.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Policies.Floor)]
public class InventoryController(AppDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> GetAll([FromQuery] ListQuery list, [FromQuery] bool inStockOnly = false)
    {
        var query = context.Inventories.AsNoTracking();
        if (inStockOnly)
            query = query.Where(i => i.QuantityAvailable > 0);
        if (list.Pattern is { } like)
            query = query.Where(i => EF.Functions.ILike(i.Product!.ProductName, like, @"\") || EF.Functions.ILike(i.Product.ProductCode, like, @"\") ||
                EF.Functions.ILike(i.Warehouse!.WarehouseName, like, @"\") || EF.Functions.ILike(i.LotNumber!, like, @"\") ||
                EF.Functions.ILike(i.BinCode!, like, @"\") || EF.Functions.ILike(i.StockForm, like, @"\"));

        var rows = query
            .OrderBy(i => i.Product!.ProductName)
            .ThenBy(i => i.LotNumber)
            .Select(i => new InventoryDTO
            {
                InventoryID = i.InventoryID,
                ProductID = i.ProductID,
                ProductCode = i.Product!.ProductCode,
                ProductName = i.Product.ProductName,
                Category = i.Product.Category,
                Unit = i.StockForm == "Bulk" ? "kg" : i.Product.Unit,
                StockForm = i.StockForm,
                WarehouseID = i.WarehouseID,
                WarehouseName = i.Warehouse!.WarehouseName,
                BinCode = i.BinCode,
                LotNumber = i.LotNumber,
                QuantityAvailable = i.QuantityAvailable,
                LastUpdatedDate = i.LastUpdatedDate,
            });

        if (!list.Paged)
            return Ok(await rows.ToListAsync());
        // Ordered by product and lot above, so one product's lots stay together across pages.
        return Ok(Paging.Result(await Paging.PageAsync(rows, list), i => i));
    }
    [HttpGet("{id}/movements")]
    public async Task<ActionResult> Movements(int id) => Ok(await context.StockMovements.Where(m => m.InventoryID == id)
        .OrderByDescending(m => m.RecordedAt).ThenByDescending(m => m.StockMovementID)
        .Select(m => new { m.StockMovementID, m.RecordedAt, m.MovementType, m.Reference, m.QuantityChange, m.Unit, m.Reason }).ToListAsync());
}
