namespace Hisaab.ERP.Models;

public class Supplier
{
    public int SupplierID { get; set; }
    public string SupplierName { get; set; } = null!;
    public string? ContactPerson { get; set; }
    public string? PhoneNumber { get; set; }
    public string? NTN { get; set; }
    public string? Address { get; set; }
    public string? PaymentTerms { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();
}
