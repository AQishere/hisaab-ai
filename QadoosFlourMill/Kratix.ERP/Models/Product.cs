namespace Kratix.ERP.Models;

public class Product
{
    public int ProductID { get; set; }
    public string ProductCode { get; set; } = null!;
    public string ProductName { get; set; } = null!;
    public string? Category { get; set; } // RawMaterial, FinishedGood, ByProduct, Packaging
    public string? Unit { get; set; } // kg, bag, piece, ton
    public string? HSNCode { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public ICollection<POLineItem> POLineItems { get; set; } = new List<POLineItem>();
    public ICollection<SOLineItem> SOLineItems { get; set; } = new List<SOLineItem>();
    public ICollection<BatchMaterial> BatchMaterials { get; set; } = new List<BatchMaterial>();
    public ICollection<Inventory> Inventories { get; set; } = new List<Inventory>();
}
