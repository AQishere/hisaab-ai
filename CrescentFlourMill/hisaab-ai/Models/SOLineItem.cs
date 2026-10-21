using System.ComponentModel.DataAnnotations;

namespace Hisaab.ERP.Models;

public class SOLineItem
{
    [Key]
    public int LineItemID { get; set; }
    public int SalesOrderID { get; set; }
    public int ProductID { get; set; }
    public decimal Quantity { get; set; }
    public decimal RatePerUnit { get; set; }
    public decimal TotalAmount { get; set; }

    // Dispatch tracking (replaces separate DispatchChallans table)
    public decimal DispatchedQuantity { get; set; } = 0;
    public string? DispatchStatus { get; set; } = "NotDispatched"; // NotDispatched, PartiallyDispatched, Dispatched
    public string? TruckNumber { get; set; }
    public string? DriverName { get; set; }
    public DateTime? DispatchDate { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public SalesOrder? SalesOrder { get; set; }
    public Product? Product { get; set; }
}
