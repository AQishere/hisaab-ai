namespace Kratix.ERP.DTOs;

public class WarehouseDTO
{
    public int WarehouseID { get; set; }
    public required string WarehouseName { get; set; }
    public string? Location { get; set; }
    public decimal Capacity { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; }
}

public class CreateWarehouseDTO
{
    public required string WarehouseName { get; set; }
    public required string Location { get; set; }
    public decimal Capacity { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateWarehouseDTO
{
    public required string WarehouseName { get; set; }
    public required string Location { get; set; }
    public decimal Capacity { get; set; }
    public bool IsActive { get; set; } = true;
}
