using Microsoft.AspNetCore.Authorization;

namespace Hisaab.ERP.Auth;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Owner = "Owner";
    public const string Operations = "Operations";
    public const string Sales = "Sales";
    public const string Worker = "Worker";
}

// Keep in sync with kratix-ui/src/auth/permissions.ts
public static class Policies
{
    public const string Management = nameof(Management);
    public const string Purchasing = nameof(Purchasing);
    public const string Floor = nameof(Floor);
    public const string SalesFinance = nameof(SalesFinance);

    public static void Register(AuthorizationOptions options)
    {
        options.AddPolicy(Management, p => p.RequireRole(Roles.Admin, Roles.Owner));
        options.AddPolicy(Purchasing, p => p.RequireRole(Roles.Admin, Roles.Owner, Roles.Operations));
        options.AddPolicy(Floor, p => p.RequireRole(Roles.Admin, Roles.Owner, Roles.Operations, Roles.Worker));
        options.AddPolicy(SalesFinance, p => p.RequireRole(Roles.Admin, Roles.Owner, Roles.Sales));

        options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    }
}
