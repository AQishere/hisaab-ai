namespace Kratix.ERP.Models;

public class Warehouse
{
    public int WarehouseID { get; set; }
    public string WarehouseName { get; set; } = null!;
    public string? Location { get; set; }
    public decimal Capacity { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public ICollection<InboundReceipt> InboundReceipts { get; set; } = new List<InboundReceipt>();
    public ICollection<Inventory> Inventories { get; set; } = new List<Inventory>();
    public ICollection<BatchMaterial> BatchMaterials { get; set; } = new List<BatchMaterial>();
}
