namespace Hisaab.ERP.Models;

public class StockMovement
{
    public int StockMovementID { get; set; }
    public int InventoryID { get; set; }
    public Inventory Inventory { get; set; } = null!;
    public decimal QuantityChange { get; set; }
    public string Unit { get; set; } = null!;
    public string MovementType { get; set; } = null!;
    public string Reference { get; set; } = null!;
    public string? Reason { get; set; }
    public int? RecordedBy { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.Now;
}
