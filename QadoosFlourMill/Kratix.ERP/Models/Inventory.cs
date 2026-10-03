namespace Kratix.ERP.Models;

public class Inventory
{
    public int InventoryID { get; set; }
    public int ProductID { get; set; }
    public int WarehouseID { get; set; }
    public string? BinCode { get; set; }
    public string? LotNumber { get; set; }
    public decimal QuantityAvailable { get; set; }
    public DateTime LastUpdatedDate { get; set; } = DateTime.Now;

    public Product? Product { get; set; }
    public Warehouse? Warehouse { get; set; }
}
