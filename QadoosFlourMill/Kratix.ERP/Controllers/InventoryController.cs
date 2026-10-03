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
                Unit = i.Product.Unit,
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
}
