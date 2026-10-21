using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Hisaab.ERP.Auth;

public static partial class AccountRules
{
    // IReadOnlyList, not arrays: array.Contains binds to a span overload that EF Core cannot translate in queries.
    public static readonly IReadOnlyList<string> AllRoles = [Roles.Admin, Roles.Owner, Roles.Operations, Roles.Sales, Roles.Worker];
    public static readonly IReadOnlyList<string> ManagementRoles = [Roles.Admin, Roles.Owner];

    // Claim carrying a fingerprint of the password hash. A token stops working as soon as the
    // password changes, so a reset signs out every existing session without storing anything extra.
    public const string PasswordStampClaim = "pwd";

    public static string PasswordStamp(string? passwordHash) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(passwordHash ?? string.Empty)))[..16];

    public static string? UsernameError(string? username) =>
        username is not null && UsernamePattern().IsMatch(username) ? null
            : "Username must be 3-50 characters: lowercase letters, digits, dot, dash or underscore.";

    public static string? PasswordError(string? password, string username) =>
        password is null || password.Length < 8 || password.Length > 128 ? "Password must be 8-128 characters."
        : password.Equals(username, StringComparison.OrdinalIgnoreCase) ? "Password must not be the username."
        : null;

    [GeneratedRegex("^[a-z0-9._-]{3,50}$")]
    private static partial Regex UsernamePattern();
}
