using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Kratix.ERP.Data;
using Kratix.ERP.Models;

namespace Kratix.ERP.Auth;

public static class DemoUserSeeder
{
    private sealed class DemoUser
    {
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    private static readonly HashSet<string> KnownRoles = [Roles.Admin, Roles.Owner, Roles.Operations, Roles.Sales, Roles.Worker];

    // Earlier builds seeded every account with this password; upgrade those once.
    private const string LegacySharedPassword = "password";

    // Demo accounts come from the "DemoUsers" config section (appsettings.Development.json, not committed).
    public static async Task SeedAsync(AppDbContext db, IConfiguration config)
    {
        var demoUsers = config.GetSection("DemoUsers").Get<List<DemoUser>>() ?? [];
        var usable = demoUsers.Where(d => !string.IsNullOrWhiteSpace(d.Username) && !string.IsNullOrWhiteSpace(d.Password) && KnownRoles.Contains(d.Role)).ToList();

        if (usable.Count == 0)
        {
            Console.WriteLine("No demo users configured (DemoUsers in appsettings.Development.json); skipping demo account setup.");
            return;
        }

        var hasher = new PasswordHasher<User>();
        var existing = await db.Users.ToDictionaryAsync(u => u.Username);

        foreach (var demo in usable)
        {
            var username = demo.Username.Trim().ToLowerInvariant();
            if (!existing.TryGetValue(username, out var user))
            {
                user = new User { Username = username, Email = $"{username}@qadoos.com", Role = demo.Role, IsActive = true };
                user.PasswordHash = hasher.HashPassword(user, demo.Password);
                db.Users.Add(user);
            }
            else if (user.PasswordHash is not null &&
                     hasher.VerifyHashedPassword(user, user.PasswordHash, LegacySharedPassword) != PasswordVerificationResult.Failed)
            {
                user.PasswordHash = hasher.HashPassword(user, demo.Password);
            }
        }

        await db.SaveChangesAsync();
    }
}
