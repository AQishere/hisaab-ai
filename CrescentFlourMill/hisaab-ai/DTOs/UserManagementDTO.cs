namespace Hisaab.ERP.DTOs;

public class UserAccountDTO
{
    public int UserID { get; set; }
    public string Username { get; set; } = "";
    public string? Email { get; set; }
    public string Role { get; set; } = "";
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class CreateUserDTO
{
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; }
    public string? Password { get; set; }
}

public class UpdateUserDTO
{
    public string? Email { get; set; }
    public string? Role { get; set; }
    public bool IsActive { get; set; }
}

public class ResetPasswordDTO
{
    public string? NewPassword { get; set; }
}

public class ChangePasswordDTO
{
    public string? CurrentPassword { get; set; }
    public string? NewPassword { get; set; }
    public bool RememberMe { get; set; }
}
