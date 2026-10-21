namespace Hisaab.ERP.Models;

public class Customer
{
    public int CustomerID { get; set; }
    public string CustomerName { get; set; } = null!;
    public string? Type { get; set; } // Distributor, Wholesaler, Bakery, Retailer
    public string? ContactPerson { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public decimal CreditLimit { get; set; }
    public decimal OutstandingBalance { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public ICollection<SalesOrder> SalesOrders { get; set; } = new List<SalesOrder>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
