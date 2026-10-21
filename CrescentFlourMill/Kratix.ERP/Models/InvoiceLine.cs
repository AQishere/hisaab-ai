namespace Kratix.ERP.Models;

public class InvoiceLine
{
    public int InvoiceLineID { get; set; }
    public int InvoiceID { get; set; }
    public Invoice Invoice { get; set; } = null!;
    public int ProductID { get; set; }
    public Product Product { get; set; } = null!;
    public string ProductCode { get; set; } = null!;
    public string ProductName { get; set; } = null!;
    public string Unit { get; set; } = null!;
    public decimal? KgPerBag { get; set; }
    public decimal Quantity { get; set; }
    public decimal RatePerUnit { get; set; }
    public decimal TotalAmount { get; set; }
    public bool IsLegacySnapshot { get; set; }
}
