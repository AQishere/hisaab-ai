namespace Kratix.ERP.DTOs;

public class ProductDTO
{
    public int ProductID { get; set; }
    public string ProductCode { get; set; }
    public string ProductName { get; set; }
    public string Category { get; set; }
    public string Unit { get; set; }
    public decimal? KgPerBag { get; set; }
    public string HSNCode { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class CreateProductDTO
{
    public string ProductCode { get; set; }
    public string ProductName { get; set; }
    public string Category { get; set; }
    public string Unit { get; set; }
    public decimal? KgPerBag { get; set; }
    public string HSNCode { get; set; }
}

public class UpdateProductDTO
{
    public string ProductCode { get; set; }
    public string ProductName { get; set; }
    public string Category { get; set; }
    public string Unit { get; set; }
    public decimal? KgPerBag { get; set; }
    public string HSNCode { get; set; }
}
