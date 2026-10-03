namespace Kratix.ERP.DTOs;

public class InventoryDTO
{
    public int InventoryID { get; set; }
    public int ProductID { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public int WarehouseID { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public string? BinCode { get; set; }
    public string? LotNumber { get; set; }
    public decimal QuantityAvailable { get; set; }
    public DateTime LastUpdatedDate { get; set; }
}

public class FinalizeBatchDTO
{
    public int WarehouseID { get; set; }
}
