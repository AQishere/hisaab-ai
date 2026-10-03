using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Kratix.ERP.Auth;
using Kratix.ERP.Data;
using Kratix.ERP.DTOs;

namespace Kratix.ERP.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Policies.Floor)]
public class InventoryController(AppDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<InventoryDTO>>> GetAll([FromQuery] bool inStockOnly = false)
    {
        var query = context.Inventories.AsNoTracking();
        if (inStockOnly)
            query = query.Where(i => i.QuantityAvailable > 0);

        var items = await query
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
            })
            .ToListAsync();

        return Ok(items);
    }
    [HttpGet("{id}/movements")]
    public async Task<ActionResult> Movements(int id) => Ok(await context.StockMovements.Where(m => m.InventoryID == id)
        .OrderByDescending(m => m.RecordedAt).ThenByDescending(m => m.StockMovementID)
        .Select(m => new { m.StockMovementID, m.RecordedAt, m.MovementType, m.Reference, m.QuantityChange, m.Unit, m.Reason }).ToListAsync());
}
