using System.ComponentModel.DataAnnotations;

namespace Kratix.ERP.Models;

public class BatchMaterial
{
    [Key]
    public int BatchMaterialID { get; set; }
    public int BatchID { get; set; }
    public int ProductID { get; set; }
    public string MaterialType { get; set; } = null!; // Input, Output, Waste
    public decimal Quantity { get; set; }

    // For inputs: which warehouse/lot was consumed
    public int? SourceWarehouseID { get; set; }
    public string? SourceLotNumber { get; set; }

    // For outputs: quality approval
    public bool IsQualityApproved { get; set; }
    public int? ApprovedBy { get; set; }
    public DateTime? ApprovalDate { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public ProductionBatch? ProductionBatch { get; set; }
    public Product? Product { get; set; }
    public Warehouse? SourceWarehouse { get; set; }
    public User? ApprovedByUser { get; set; }
}
