namespace Kratix.ERP.DTOs;

public class CustomerDTO
{
    public int CustomerID { get; set; }
    public required string CustomerName { get; set; }
    public string? Type { get; set; }
    public string? ContactPerson { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public decimal CreditLimit { get; set; }
    public decimal OutstandingBalance { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class CreateCustomerDTO
{
    public required string CustomerName { get; set; }
    public required string Type { get; set; }
    public required string ContactPerson { get; set; }
    public required string PhoneNumber { get; set; }
    public required string Address { get; set; }
    public decimal CreditLimit { get; set; }
}

public class UpdateCustomerDTO
{
    public required string CustomerName { get; set; }
    public required string Type { get; set; }
    public required string ContactPerson { get; set; }
    public required string PhoneNumber { get; set; }
    public required string Address { get; set; }
    public decimal CreditLimit { get; set; }
    public bool IsActive { get; set; }
}
