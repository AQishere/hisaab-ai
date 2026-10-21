namespace Hisaab.ERP.Models;

public class Invoice
{
    public int InvoiceID { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public int SalesOrderID { get; set; }
    public int CustomerID { get; set; }
    public DateTime InvoiceDate { get; set; }

    public decimal TotalQuantity { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal NetAmount { get; set; }

    public string Status { get; set; } = "Unpaid"; // Unpaid, PartiallyPaid, Paid
    public DateTime? DueDate { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public SalesOrder? SalesOrder { get; set; }
    public Customer? Customer { get; set; }
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();
}
