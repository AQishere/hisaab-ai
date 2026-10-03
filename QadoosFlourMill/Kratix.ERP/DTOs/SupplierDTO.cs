namespace Kratix.ERP.DTOs;

public class SupplierDTO
{
    public int SupplierID { get; set; }
    public string SupplierName { get; set; }
    public string ContactPerson { get; set; }
    public string PhoneNumber { get; set; }
    public string NTN { get; set; }
    public string Address { get; set; }
    public string PaymentTerms { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class CreateSupplierDTO
{
    public string SupplierName { get; set; }
    public string ContactPerson { get; set; }
    public string PhoneNumber { get; set; }
    public string NTN { get; set; }
    public string Address { get; set; }
    public string PaymentTerms { get; set; }
}

public class UpdateSupplierDTO
{
    public string SupplierName { get; set; }
    public string ContactPerson { get; set; }
    public string PhoneNumber { get; set; }
    public string NTN { get; set; }
    public string Address { get; set; }
    public string PaymentTerms { get; set; }
    public bool IsActive { get; set; }
}
