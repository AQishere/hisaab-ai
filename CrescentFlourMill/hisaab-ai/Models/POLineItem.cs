using System.ComponentModel.DataAnnotations;

namespace Hisaab.ERP.Models;

public class POLineItem
{
    [Key]
    public int LineItemID { get; set; }
    public int POID { get; set; }
    public int ProductID { get; set; }
    public decimal Quantity { get; set; }
    public decimal RatePerUnit { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal ReceivedQuantity { get; set; } = 0;

    public PurchaseOrder? PurchaseOrder { get; set; }
    public Product? Product { get; set; }
}
