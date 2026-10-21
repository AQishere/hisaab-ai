using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Hisaab.ERP.Data;
using Hisaab.ERP.Models;

namespace Hisaab.ERP.Auth;

// First sign-in on a new server, or recovery when every Admin/Owner is locked out:
// set InitialAdmin__Username and InitialAdmin__Password, start the backend once, sign in, change the password,
// then remove both settings. Does nothing while any active Admin or Owner exists.
public static class InitialAdmin
{
    public static async Task EnsureAsync(AppDbContext db, IConfiguration config)
    {
        var username = config["InitialAdmin:Username"]?.Trim().ToLowerInvariant();
        var password = config["InitialAdmin:Password"];
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            return;

        if (await db.Users.AnyAsync(u => u.IsActive && AccountRules.ManagementRoles.Contains(u.Role)))
        {
            Console.WriteLine("InitialAdmin is set but an active Admin/Owner already exists; nothing changed. Remove the InitialAdmin settings.");
            return;
        }

        var error = AccountRules.UsernameError(username) ?? AccountRules.PasswordError(password, username);
        if (error != null)
            throw new InvalidOperationException($"InitialAdmin: {error}");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null)
        {
            user = new User { Username = username };
            db.Users.Add(user);
        }
        user.Role = Roles.Admin;
        user.IsActive = true;
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password);
        await db.SaveChangesAsync();
        Console.WriteLine($"InitialAdmin: '{username}' can now sign in as Admin. Change the password, then remove the InitialAdmin settings.");
    }
}
