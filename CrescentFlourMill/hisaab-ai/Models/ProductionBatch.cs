using System.ComponentModel.DataAnnotations;

namespace Hisaab.ERP.Models;

public class ProductionBatch
{
    [Key]
    public int BatchID { get; set; }
    public string BatchNumber { get; set; } = null!;
    public DateTime ProductionDate { get; set; }
    public int ShiftNumber { get; set; } // 1, 2, 3
    public string Status { get; set; } = "Planned"; // Planned, InProgress, Completed, OnHold

    // Input & Output tracking
    public decimal TotalInputQuantity { get; set; } // Calculated from inputs
    public decimal TotalOutputQuantity { get; set; } // Calculated from outputs
    public decimal TotalWasteQuantity { get; set; } // Waste line
    public decimal YieldPercentage { get; set; } // (Output / Input) * 100

    public string? OperatorName { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public ICollection<BatchMaterial> BatchMaterials { get; set; } = new List<BatchMaterial>();
}
