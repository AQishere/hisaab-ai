namespace Hisaab.ERP.DTOs;

public class LoginRequestDto
{
    public required string Username { get; set; }
    public required string Password { get; set; }
    public bool RememberMe { get; set; }
}

public class DemoLoginRequestDto
{
    public required string Username { get; set; }
}
