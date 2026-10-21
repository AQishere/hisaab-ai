using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Kratix.ERP.Auth;
using Kratix.ERP.Data;
using Kratix.ERP.DTOs;
using Kratix.ERP.Models;

namespace Kratix.ERP.Controllers;

// Accounts are never deleted (their names appear in history); deactivate them instead.
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Policies.Management)]
public class UsersController(AppDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<UserAccountDTO>>> GetAll() =>
        Ok(await context.Users.OrderBy(u => u.Username).Select(u => new UserAccountDTO
        {
            UserID = u.UserID, Username = u.Username, Email = u.Email, Role = u.Role, IsActive = u.IsActive, CreatedDate = u.CreatedDate
        }).ToListAsync());

    [HttpPost]
    public async Task<ActionResult<UserAccountDTO>> Create(CreateUserDTO dto)
    {
        var username = dto.Username?.Trim().ToLowerInvariant();
        var error = AccountRules.UsernameError(username) ?? RoleError(dto.Role) ?? EmailError(dto.Email) ?? AccountRules.PasswordError(dto.Password, username!);
        if (error != null)
            return BadRequest(error);

        if (await context.Users.AnyAsync(u => u.Username == username))
            return BadRequest("That username is already taken.");

        var user = new User { Username = username!, Email = Clean(dto.Email), Role = dto.Role!, IsActive = true };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, dto.Password!);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return Created($"/api/users/{user.UserID}", ToDTO(user));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<UserAccountDTO>> Update(int id, UpdateUserDTO dto)
    {
        var user = await context.Users.FindAsync(id);
        if (user == null)
            return NotFound();

        var error = RoleError(dto.Role) ?? EmailError(dto.Email);
        if (error != null)
            return BadRequest(error);

        if (user.UserID == StockLedger.UserId(User) && (dto.Role != user.Role || !dto.IsActive))
            return BadRequest("You cannot change your own role or deactivate your own account.");

        var leavesManagement = AccountRules.ManagementRoles.Contains(user.Role) && user.IsActive &&
            (!dto.IsActive || !AccountRules.ManagementRoles.Contains(dto.Role));
        if (leavesManagement && !await context.Users.AnyAsync(u => u.UserID != id && u.IsActive && AccountRules.ManagementRoles.Contains(u.Role)))
            return BadRequest("At least one active Admin or Owner account must remain.");

        user.Email = Clean(dto.Email);
        user.Role = dto.Role!;
        user.IsActive = dto.IsActive;
        await context.SaveChangesAsync();
        return Ok(ToDTO(user));
    }

    // Resetting the password signs the user out everywhere (see AccountRules.PasswordStamp).
    [HttpPut("{id}/password")]
    public async Task<ActionResult> ResetPassword(int id, ResetPasswordDTO dto)
    {
        var user = await context.Users.FindAsync(id);
        if (user == null)
            return NotFound();

        var error = AccountRules.PasswordError(dto.NewPassword, user.Username);
        if (error != null)
            return BadRequest(error);

        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, dto.NewPassword!);
        await context.SaveChangesAsync();
        return NoContent();
    }

    private static string? RoleError(string? role) =>
        role != null && AccountRules.AllRoles.Contains(role) ? null : "Choose a role: Admin, Owner, Operations, Sales or Worker.";

    private static string? EmailError(string? email) =>
        string.IsNullOrWhiteSpace(email) || (email.Trim().Length <= 200 && email.Contains('@')) ? null : "Enter a valid email address or leave it empty.";

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static UserAccountDTO ToDTO(User u) => new()
    {
        UserID = u.UserID, Username = u.Username, Email = u.Email, Role = u.Role, IsActive = u.IsActive, CreatedDate = u.CreatedDate
    };
}
