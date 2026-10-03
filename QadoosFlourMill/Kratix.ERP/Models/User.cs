namespace Kratix.ERP.Models;

public class User
{
    public int UserID { get; set; }
    public string Username { get; set; } = null!;
    public string? Email { get; set; }
    public string? PasswordHash { get; set; }
    public string Role { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
