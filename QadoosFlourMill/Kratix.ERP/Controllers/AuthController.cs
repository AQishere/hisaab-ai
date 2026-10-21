using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Kratix.ERP.Auth;
using Kratix.ERP.Data;
using Kratix.ERP.Demo;
using Kratix.ERP.DTOs;
using Kratix.ERP.Models;

namespace Kratix.ERP.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    public const string LoginRateLimit = "login";
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(8);
    private static readonly TimeSpan RememberedTokenLifetime = TimeSpan.FromDays(7);

    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public AuthController(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(LoginRateLimit)]
    public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Username and password are required" });
        }

        var username = request.Username.Trim().ToLowerInvariant();
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == username && u.IsActive);

        if (user?.PasswordHash is null ||
            new PasswordHasher<User>().VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new { message = "Invalid username or password" });
        }

        return Ok(new LoginResponseDto
        {
            Token = CreateToken(user, request.RememberMe ? RememberedTokenLifetime : TokenLifetime),
            User = new UserDto
            {
                Id = user.UserID.ToString(),
                Username = user.Username,
                Email = user.Email ?? string.Empty,
                Role = user.Role,
            },
        });
    }

    // Public demo only (Demo:Enabled): sign in as one of the demo roles without a password.
    [HttpPost("demo-login")]
    [AllowAnonymous]
    [EnableRateLimiting(LoginRateLimit)]
    public async Task<ActionResult<LoginResponseDto>> DemoLogin([FromBody] DemoLoginRequestDto request)
    {
        if (!DemoData.Enabled(_config))
            return NotFound();

        var username = request.Username.Trim().ToLowerInvariant();
        var user = DemoData.Accounts.Contains(username)
            ? await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == username && u.IsActive)
            : null;
        if (user == null)
            return Unauthorized(new { message = "Unknown demo account" });

        return Ok(new LoginResponseDto
        {
            Token = CreateToken(user, TokenLifetime),
            User = new UserDto { Id = user.UserID.ToString(), Username = user.Username, Email = user.Email ?? string.Empty, Role = user.Role },
        });
    }

    // Requires the current password. Other sessions are signed out; this one gets a fresh token.
    [HttpPost("change-password")]
    [Authorize]
    public async Task<ActionResult<LoginResponseDto>> ChangePassword([FromBody] ChangePasswordDTO request)
    {
        var id = StockLedger.UserId(User);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserID == id && u.IsActive);
        if (user?.PasswordHash is null)
            return Unauthorized();

        var hasher = new PasswordHasher<User>();
        if (string.IsNullOrEmpty(request.CurrentPassword) ||
            hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed)
            return BadRequest(new { message = "Current password is incorrect." });

        var error = AccountRules.PasswordError(request.NewPassword, user.Username);
        if (error != null)
            return BadRequest(new { message = error });
        if (request.NewPassword == request.CurrentPassword)
            return BadRequest(new { message = "Choose a password different from the current one." });

        user.PasswordHash = hasher.HashPassword(user, request.NewPassword!);
        await _db.SaveChangesAsync();
        AccountCache.Forget(user.UserID);

        return Ok(new LoginResponseDto
        {
            Token = CreateToken(user, request.RememberMe ? RememberedTokenLifetime : TokenLifetime),
            User = new UserDto { Id = user.UserID.ToString(), Username = user.Username, Email = user.Email ?? string.Empty, Role = user.Role },
        });
    }

    private string CreateToken(User user, TimeSpan lifetime)
    {
        var jwt = _config.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt["Issuer"],
            Audience = jwt["Audience"],
            Expires = DateTime.UtcNow.Add(lifetime),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim(AccountRules.PasswordStampClaim, AccountRules.PasswordStamp(user.PasswordHash)),
            }),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
