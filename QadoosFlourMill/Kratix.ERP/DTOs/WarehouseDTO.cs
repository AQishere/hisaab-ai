namespace Kratix.ERP.DTOs;

public class WarehouseDTO
{
    public int WarehouseID { get; set; }
    public string WarehouseName { get; set; }
    public string Location { get; set; }
    public decimal Capacity { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; }
}

public class CreateWarehouseDTO
{
    public string WarehouseName { get; set; }
    public string Location { get; set; }
    public decimal Capacity { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateWarehouseDTO
{
    public string WarehouseName { get; set; }
    public string Location { get; set; }
    public decimal Capacity { get; set; }
    public bool IsActive { get; set; } = true;
}
