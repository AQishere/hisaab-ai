namespace Hisaab.ERP.Models;

public class Inventory
{
    public int InventoryID { get; set; }
    public int ProductID { get; set; }
    public int WarehouseID { get; set; }
    public string? BinCode { get; set; }
    public string? LotNumber { get; set; }
    public decimal QuantityAvailable { get; set; }
    public string StockForm { get; set; } = "Standard"; // Bulk is always measured in kg.
    public DateTime ReceivedAt { get; set; } = DateTime.Now;
    public DateTime LastUpdatedDate { get; set; } = DateTime.Now;

    public Product? Product { get; set; }
    public Warehouse? Warehouse { get; set; }
}
