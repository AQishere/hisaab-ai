using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Kratix.ERP.Auth;
using Kratix.ERP.Controllers;
using Kratix.ERP.Data;
using Kratix.ERP.Demo;

// DateTime values are the mill's local wall-clock time, as they were on SQL Server: store them as Postgres
// "timestamp without time zone" instead of requiring UTC.
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

var connection = DatabaseUrl.ToConnectionString(builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing (see README.md)."));
// The database session runs in the server's time zone; otherwise "today" filters compare local dates with UTC ones.
if (string.IsNullOrEmpty(connection.Timezone))
    connection.Timezone = TimeZoneInfo.Local.HasIanaId ? TimeZoneInfo.Local.Id
        : TimeZoneInfo.TryConvertWindowsIdToIanaId(TimeZoneInfo.Local.Id, out var iana) ? iana : "UTC";
var connectionString = connection.ConnectionString;

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// Only these website addresses may call the API (Cors:AllowedOrigins; override per server with Cors__AllowedOrigins__0, ...).
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (allowedOrigins.Length == 0)
{
    throw new InvalidOperationException("Cors:AllowedOrigins is empty. List the website address(es) allowed to call the API (see DEPLOYMENT.md).");
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

var jwt = builder.Configuration.GetSection("Jwt");
var jwtKey = jwt["Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
{
    throw new InvalidOperationException("Jwt:Key is missing or shorter than 32 characters. Copy appsettings.Development.example.json to appsettings.Development.json and set Jwt:Key (see README.md); on a server set the Jwt__Key environment variable (see DEPLOYMENT.md).");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwt["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role,
        };
        // A token is only honoured while its account is active, keeps the same role, and keeps the same password.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var principal = context.Principal!;
                var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var account = int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
                    ? await db.Users.AsNoTracking().Where(u => u.UserID == userId).Select(u => new { u.IsActive, u.Role, u.PasswordHash }).FirstOrDefaultAsync()
                    : null;
                if (account == null || !account.IsActive || account.Role != principal.FindFirstValue(ClaimTypes.Role) ||
                    AccountRules.PasswordStamp(account.PasswordHash) != principal.FindFirstValue(AccountRules.PasswordStampClaim))
                {
                    context.Fail("The account was deactivated or its role or password changed. Sign in again.");
                }
            },
        };
    });

builder.Services.AddAuthorization(Policies.Register);

// Slows down password guessing: 10 login attempts per minute per client address.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AuthController.LoginRateLimit, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

var demo = DemoData.Enabled(builder.Configuration);
if (demo)
    builder.Services.AddHostedService<DemoResetService>();

builder.Services.AddScoped<BusinessWriteFilter>();
builder.Services.AddControllers(options => options.Filters.AddService<BusinessWriteFilter>());
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.Migrate();
    // Demo accounts (with their known passwords) exist only on development machines.
    if (app.Environment.IsDevelopment())
    {
        await DemoUserSeeder.SeedAsync(dbContext, app.Configuration);
    }
    if (demo)
        await DemoData.ResetIfDueAsync(dbContext);
    else
        await InitialAdmin.EnsureAsync(dbContext, app.Configuration);
    Console.WriteLine("Database migrated and ready.");
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

app.UseCors("AllowFrontend");
// The demo host ends HTTPS at its proxy and forwards plain HTTP, so redirecting there would loop.
if (!demo)
    app.UseHttpsRedirection();
if (demo)
{
    // Visitors share the demo accounts: one must not lock the others out by changing passwords or users.
    app.Use(async (context, next) =>
    {
        var path = context.Request.Path;
        if (!HttpMethods.IsGet(context.Request.Method) &&
            (path.StartsWithSegments("/api/users") || path.StartsWithSegments("/api/auth/change-password")))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { message = "Account changes are turned off in the public demo." });
            return;
        }
        await next();
    });
}
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.Run();
