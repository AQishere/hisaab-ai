namespace Kratix.ERP.DTOs;

public class SupplierDTO
{
    public int SupplierID { get; set; }
    public required string SupplierName { get; set; }
    public string? ContactPerson { get; set; }
    public string? PhoneNumber { get; set; }
    public string? NTN { get; set; }
    public string? Address { get; set; }
    public string? PaymentTerms { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class CreateSupplierDTO
{
    public required string SupplierName { get; set; }
    public required string ContactPerson { get; set; }
    public required string PhoneNumber { get; set; }
    public required string NTN { get; set; }
    public required string Address { get; set; }
    public required string PaymentTerms { get; set; }
}

public class UpdateSupplierDTO
{
    public required string SupplierName { get; set; }
    public required string ContactPerson { get; set; }
    public required string PhoneNumber { get; set; }
    public required string NTN { get; set; }
    public required string Address { get; set; }
    public required string PaymentTerms { get; set; }
    public bool IsActive { get; set; }
}
