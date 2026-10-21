using System.ComponentModel.DataAnnotations;

namespace Hisaab.ERP.Models;

public class InboundReceipt
{
    public decimal? ReceivedQuantity { get; set; }
    public string ReceiptType { get; set; } = "Weighed";
    [Key]
    public int ReceiptID { get; set; }
    public string ReceiptNumber { get; set; } = null!;
    public int POID { get; set; }
    public int? POLineItemID { get; set; }

    // Gate Entry info
    public string? TruckNumber { get; set; }
    public string? DriverName { get; set; }
    public string? DriverPhone { get; set; }
    public DateTime? ArrivalDateTime { get; set; }

    // Weighbridge info
    public decimal? GrossWeight { get; set; }
    public decimal? TareWeight { get; set; }
    public decimal? NetWeight { get; set; } // Calculated: Gross - Tare

    // Quality Control info
    public decimal? Moisture { get; set; }
    public decimal? Impurities { get; set; }
    public string? GrainType { get; set; }
    public string? QCResult { get; set; } // Approved, Hold, Rejected

    // Goods Receipt info
    public int? WarehouseID { get; set; }
    public string? BinCode { get; set; }
    public string? LotNumber { get; set; }

    // Status and tracking
    public string Status { get; set; } = "GateEntry"; // GateEntry → Weighed → QCApproved/Rejected → Received, or Voided
    public string? VoidReason { get; set; }
    public int? ReceivedBy { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public PurchaseOrder? PurchaseOrder { get; set; }
    public POLineItem? POLineItem { get; set; }
    public Warehouse? Warehouse { get; set; }
    public User? ReceivedByUser { get; set; }
}
