namespace Kratix.ERP.DTOs;

public class InvoiceDTO
{
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
    public int DueDays { get; set; }
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
