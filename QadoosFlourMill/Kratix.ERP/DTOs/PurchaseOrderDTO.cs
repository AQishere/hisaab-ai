namespace Kratix.ERP.DTOs;

public class PurchaseOrderDTO
{
    public int POID { get; set; }
    public required string PONumber { get; set; }
    public int SupplierID { get; set; }
    public string? SupplierName { get; set; }
    public DateTime PODate { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public required string Status { get; set; }
    public string? ClosedReason { get; set; }
    public decimal TotalAmount { get; set; }
    public List<POLineItemDTO> LineItems { get; set; } = new();
    public DateTime CreatedDate { get; set; }
}

public class POLineItemDTO
{
    public int LineItemID { get; set; }
    public int ProductID { get; set; }
    public string? ProductName { get; set; }
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
    public decimal RatePerUnit { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal ReceivedQuantity { get; set; }
}

public class CreatePurchaseOrderDTO
{
    public int SupplierID { get; set; }
    public DateTime PODate { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public List<CreatePOLineItemDTO> LineItems { get; set; } = new();
}

public class CreatePOLineItemDTO
{
    public int ProductID { get; set; }
    public decimal Quantity { get; set; }
    public decimal RatePerUnit { get; set; }
}

public class UpdatePurchaseOrderDTO
{
    public DateTime? ExpectedDeliveryDate { get; set; }
    public required string Status { get; set; }
}
