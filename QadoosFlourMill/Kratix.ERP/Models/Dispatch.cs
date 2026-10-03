namespace Kratix.ERP.Models;

public class Dispatch
{
    public int DispatchID { get; set; }
    public int SalesOrderID { get; set; }
    public SalesOrder SalesOrder { get; set; } = null!;
    public string TruckNumber { get; set; } = null!;
    public string DriverName { get; set; } = null!;
    public DateTime DispatchedAt { get; set; } = DateTime.Now;
    public int? RecordedBy { get; set; }
    public ICollection<DispatchLine> Lines { get; set; } = new List<DispatchLine>();
}

public class DispatchLine
{
    public int DispatchLineID { get; set; }
    public int DispatchID { get; set; }
    public Dispatch Dispatch { get; set; } = null!;
    public int SOLineItemID { get; set; }
    public SOLineItem SOLineItem { get; set; } = null!;
    public int InventoryID { get; set; }
    public Inventory Inventory { get; set; } = null!;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = null!;
}
