using System.Security.Claims;
using Kratix.ERP.Models;
using Microsoft.EntityFrameworkCore;

namespace Kratix.ERP.Data;

public static class StockLedger
{
    public static int? UserId(ClaimsPrincipal user) => int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public static async Task<Inventory> GetOrCreate(AppDbContext db, int productId, int warehouseId,
        string? bin, string? lot, string form = "Standard")
    {
        bin = string.IsNullOrWhiteSpace(bin) ? null : bin.Trim();
        lot = string.IsNullOrWhiteSpace(lot) ? null : lot.Trim();
        var inventory = await db.Inventories.FirstOrDefaultAsync(i => i.ProductID == productId &&
            i.WarehouseID == warehouseId && i.BinCode == bin && i.LotNumber == lot && i.StockForm == form);
        if (inventory != null) return inventory;
        inventory = new Inventory { ProductID = productId, WarehouseID = warehouseId, BinCode = bin, LotNumber = lot, StockForm = form };
        db.Inventories.Add(inventory);
        await db.SaveChangesAsync();
        return inventory;
    }

    public static void Change(AppDbContext db, Inventory inventory, decimal delta, string unit,
        string type, string reference, ClaimsPrincipal user, string? reason = null)
    {
        if (inventory.QuantityAvailable + delta < 0) throw new BusinessRuleException("Stock cannot be negative.");
        // An emptied lot that is refilled is new stock for first-in-first-out dispatch.
        if (inventory.QuantityAvailable == 0 && delta > 0) inventory.ReceivedAt = DateTime.Now;
        inventory.QuantityAvailable += delta;
        inventory.LastUpdatedDate = DateTime.Now;
        db.StockMovements.Add(new StockMovement
        {
            Inventory = inventory, QuantityChange = delta, Unit = unit,
            MovementType = type, Reference = reference, RecordedBy = UserId(user), Reason = reason
        });
    }
}
