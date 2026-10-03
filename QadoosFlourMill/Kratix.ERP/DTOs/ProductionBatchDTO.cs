namespace Kratix.ERP.DTOs;

public class ProductionBatchDTO
{
    public int BatchID { get; set; }
    public string BatchNumber { get; set; }
    public DateTime ProductionDate { get; set; }
    public int ShiftNumber { get; set; }
    public string Status { get; set; }
    public decimal TotalInputQuantity { get; set; }
    public decimal TotalOutputQuantity { get; set; }
    public decimal TotalWasteQuantity { get; set; }
    public decimal YieldPercentage { get; set; }
    public string OperatorName { get; set; }
    public List<BatchMaterialDTO> Materials { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class BatchMaterialDTO
{
    public int BatchMaterialID { get; set; }
    public int ProductID { get; set; }
    public string ProductName { get; set; }
    public string MaterialType { get; set; }
    public decimal Quantity { get; set; }
    public string SourceLotNumber { get; set; }
    public bool IsQualityApproved { get; set; }
}

public class CreateProductionBatchDTO
{
    public DateTime ProductionDate { get; set; }
    public int ShiftNumber { get; set; }
    public string OperatorName { get; set; }
}

public class AddBatchInputDTO
{
    public int ProductID { get; set; }
    public decimal Quantity { get; set; }
    public string SourceLotNumber { get; set; }
    public int SourceWarehouseID { get; set; }
}

public class AddBatchOutputDTO
{
    public int ProductID { get; set; }
    public decimal Quantity { get; set; }
    public string MaterialType { get; set; }
}
