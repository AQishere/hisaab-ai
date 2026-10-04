namespace Kratix.ERP.DTOs;

public class ProductDTO
{
    public int ProductID { get; set; }
    public required string ProductCode { get; set; }
    public required string ProductName { get; set; }
    public string? Category { get; set; }
    public string? Unit { get; set; }
    public decimal? KgPerBag { get; set; }
    public string? HSNCode { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class CreateProductDTO
{
    public required string ProductCode { get; set; }
    public required string ProductName { get; set; }
    public required string Category { get; set; }
    public required string Unit { get; set; }
    public decimal? KgPerBag { get; set; }
    public required string HSNCode { get; set; }
}

public class UpdateProductDTO
{
    public required string ProductCode { get; set; }
    public required string ProductName { get; set; }
    public required string Category { get; set; }
    public required string Unit { get; set; }
    public decimal? KgPerBag { get; set; }
    public required string HSNCode { get; set; }
}
