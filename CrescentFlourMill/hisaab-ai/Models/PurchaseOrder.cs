using System.ComponentModel.DataAnnotations;

namespace Hisaab.ERP.Models;

public class PurchaseOrder
{
    [Key]
    public int POID { get; set; }
    public string PONumber { get; set; } = null!;
    public int SupplierID { get; set; }
    public DateTime PODate { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public string Status { get; set; } = "Draft"; // Draft, Sent, Received, Closed (short), Cancelled
    public string? ClosedReason { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime? LastModifiedDate { get; set; }

    public Supplier? Supplier { get; set; }
    public User? CreatedByUser { get; set; }
    public ICollection<POLineItem> POLineItems { get; set; } = new List<POLineItem>();
    public ICollection<InboundReceipt> InboundReceipts { get; set; } = new List<InboundReceipt>();
}
