using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Kratix.ERP.Auth;
using Kratix.ERP.Controllers;
using Kratix.ERP.Data;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=(localdb)\\mssqllocaldb;Database=Kratix_ERP;Trusted_Connection=true;";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

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
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();

app.Run();
