namespace Hisaab.ERP.Models;

public class Payment
{
    public int PaymentID { get; set; }
    public int InvoiceID { get; set; }
    public DateTime PaymentDate { get; set; }
    public string PaymentMode { get; set; } = null!; // Cash, BankTransfer, Cheque
    public decimal PaymentAmount { get; set; }
    public string? Reference { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public Invoice? Invoice { get; set; }
}
