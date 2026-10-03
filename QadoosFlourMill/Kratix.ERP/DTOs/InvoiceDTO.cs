namespace Kratix.ERP.DTOs;

public class InvoiceDTO
{
    public List<InvoiceLineDTO> Lines { get; set; } = new();
    public int InvoiceID { get; set; }
    public string InvoiceNumber { get; set; }
    public int SalesOrderID { get; set; }
    public string SONumber { get; set; }
    public int CustomerID { get; set; }
    public string CustomerName { get; set; }
    public DateTime InvoiceDate { get; set; }
    public decimal TotalQuantity { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal NetAmount { get; set; }
    public string Status { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal BalanceRemaining { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class CreateInvoiceDTO
{
    public int SalesOrderID { get; set; }
    public DateTime InvoiceDate { get; set; }
    public decimal TaxAmount { get; set; }
    public int DueDays { get; set; } = 15;
}

public class PaymentDTO
{
    public int PaymentID { get; set; }
    public int InvoiceID { get; set; }
    public string InvoiceNumber { get; set; }
    public DateTime PaymentDate { get; set; }
    public string PaymentMode { get; set; }
    public decimal PaymentAmount { get; set; }
    public string Reference { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class CreatePaymentDTO
{
    public int InvoiceID { get; set; }
    public DateTime PaymentDate { get; set; }
    public string PaymentMode { get; set; }
    public decimal PaymentAmount { get; set; }
    public string Reference { get; set; }
}

public class InvoiceLineDTO
{
    public int InvoiceLineID { get; set; }
    public string ProductCode { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string Unit { get; set; } = "";
    public decimal? KgPerBag { get; set; }
    public decimal Quantity { get; set; }
    public decimal RatePerUnit { get; set; }
    public decimal TotalAmount { get; set; }
    public bool IsLegacySnapshot { get; set; }
}
