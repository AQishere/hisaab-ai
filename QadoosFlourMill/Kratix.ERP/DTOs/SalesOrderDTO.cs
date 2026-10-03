namespace Kratix.ERP.DTOs;

public class SalesOrderDTO
{
    public int SalesOrderID { get; set; }
    public string SONumber { get; set; }
    public int CustomerID { get; set; }
    public string CustomerName { get; set; }
    public DateTime SODate { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public string Status { get; set; }
    public decimal TotalAmount { get; set; }
    public List<SOLineItemDTO> LineItems { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class SOLineItemDTO
{
    public int LineItemID { get; set; }
    public int ProductID { get; set; }
    public string ProductName { get; set; }
    public decimal Quantity { get; set; }
    public decimal RatePerUnit { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal DispatchedQuantity { get; set; }
    public string DispatchStatus { get; set; }
    public string TruckNumber { get; set; }
    public string DriverName { get; set; }
    public DateTime? DispatchDate { get; set; }
}

public class CreateSalesOrderDTO
{
    public int CustomerID { get; set; }
    public DateTime SODate { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public List<CreateSOLineItemDTO> LineItems { get; set; }
}

public class CreateSOLineItemDTO
{
    public int ProductID { get; set; }
    public decimal Quantity { get; set; }
    public decimal RatePerUnit { get; set; }
}

public class DispatchSOLineItemDTO
{
    public decimal DispatchedQuantity { get; set; }
    public string TruckNumber { get; set; }
    public string DriverName { get; set; }
}
