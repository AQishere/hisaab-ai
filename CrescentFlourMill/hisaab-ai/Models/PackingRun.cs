namespace Hisaab.ERP.Models;

public class PackingRun
{
    public int PackingRunID { get; set; }
    public int BatchMaterialID { get; set; }
    public BatchMaterial BatchMaterial { get; set; } = null!;
    public int BulkInventoryID { get; set; }
    public Inventory BulkInventory { get; set; } = null!;
    public int FinishedInventoryID { get; set; }
    public Inventory FinishedInventory { get; set; } = null!;
    public int PackagingInventoryID { get; set; }
    public Inventory PackagingInventory { get; set; } = null!;
    public decimal QuantityKg { get; set; }
    public decimal KgPerBag { get; set; }
    public decimal BagsPacked { get; set; }
    public int? RecordedBy { get; set; }
    public DateTime PackedAt { get; set; } = DateTime.Now;
}
