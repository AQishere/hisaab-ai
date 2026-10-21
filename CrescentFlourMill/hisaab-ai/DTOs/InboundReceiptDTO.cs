namespace Hisaab.ERP.DTOs;

public class InboundReceiptDTO
{
    public string ReceiptType { get; set; } = "Weighed";
    public decimal? ReceivedQuantity { get; set; }
    public int ReceiptID { get; set; }
    public required string ReceiptNumber { get; set; }
    public int POID { get; set; }
    public int? POLineItemID { get; set; }
    public string? ProductName { get; set; }
    public string? PONumber { get; set; }
    public string? TruckNumber { get; set; }
    public string? DriverName { get; set; }
    public string? DriverPhone { get; set; }
    public DateTime? ArrivalDateTime { get; set; }
    public decimal? GrossWeight { get; set; }
    public decimal? TareWeight { get; set; }
    public decimal? NetWeight { get; set; }
    public decimal? Moisture { get; set; }
    public decimal? Impurities { get; set; }
    public string? GrainType { get; set; }
    public string? QCResult { get; set; }
    public int? WarehouseID { get; set; }
    public string? WarehouseName { get; set; }
    public string? BinCode { get; set; }
    public string? LotNumber { get; set; }
    public required string Status { get; set; }
    public string? VoidReason { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class CreateGateEntryDTO
{
    public int POID { get; set; }
    public int POLineItemID { get; set; }
    public required string TruckNumber { get; set; }
    public required string DriverName { get; set; }
    public required string DriverPhone { get; set; }
}

public class UpdateWeighbridgeDTO
{
    public decimal GrossWeight { get; set; }
    public decimal TareWeight { get; set; }
}

public class UpdateQCDTO
{
    public decimal Moisture { get; set; }
    public decimal Impurities { get; set; }
    public required string GrainType { get; set; }
    public required string QCResult { get; set; }
}

public class CreateGRNDTO
{
    public int WarehouseID { get; set; }
    public string? BinCode { get; set; }
    public required string LotNumber { get; set; }
}

public class CountReceiptDTO
{
    public int POID { get; set; }
    public int POLineItemID { get; set; }
    public decimal Quantity { get; set; }
    public int WarehouseID { get; set; }
    public string? BinCode { get; set; }
    public string LotNumber { get; set; } = "";
    public string TruckNumber { get; set; } = "";
    public string DriverName { get; set; } = "";
}

public class ReasonDTO
{
    public string? Reason { get; set; }
}
