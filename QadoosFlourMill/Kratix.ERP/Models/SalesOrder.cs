namespace Kratix.ERP.Models;

public class SalesOrder
{
    public int SalesOrderID { get; set; }
    public string SONumber { get; set; } = null!;
    public int CustomerID { get; set; }
    public DateTime SODate { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public string Status { get; set; } = "Quotation"; // Quotation, Confirmed, PartiallyDispatched, Dispatched, Delivered
    public decimal TotalAmount { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public Customer? Customer { get; set; }
    public User? CreatedByUser { get; set; }
    public ICollection<SOLineItem> SOLineItems { get; set; } = new List<SOLineItem>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
