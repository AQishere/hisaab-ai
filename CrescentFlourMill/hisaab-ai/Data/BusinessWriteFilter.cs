using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Hisaab.ERP.Auth;

namespace Hisaab.ERP.Data;

// A database-owned lock serializes this single-mill application's business writes,
// including across app instances. Reads stay concurrent. Keep the validation reads
// in the same transaction as the writes; SaveChanges alone is not sufficient.
public sealed class BusinessWriteFilter(AppDbContext db) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var method = context.HttpContext.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) ||
            context.RouteData.Values["controller"]?.ToString() == "Auth")
        {
            await next();
            return;
        }

        // Read committed, not serializable: Postgres would take the serializable snapshot before the lock wait
        // and validate against stale data. With every writer behind the lock, each statement sees the latest commit.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await db.Database.ExecuteSqlRawAsync("""
            SET LOCAL lock_timeout = '15s';
            SELECT pg_advisory_xact_lock(hashtext('Hisaab.BusinessWrite'));
            """);
        var executed = await next();
        if (executed.Exception is BusinessRuleException rule)
        {
            executed.Result = new BadRequestObjectResult(rule.Message);
            executed.ExceptionHandled = true;
        }
        else if (executed.Exception is DbUpdateException)
        {
            executed.Result = new ConflictObjectResult("The change conflicts with existing records. Refresh and try again.");
            executed.ExceptionHandled = true;
        }
        var status = (executed.Result as IStatusCodeActionResult)?.StatusCode ?? 200;
        if (!executed.ExceptionHandled && executed.Exception == null && !executed.Canceled && status < 400)
        {
            await transaction.CommitAsync();
            DataVersion.Bump();
            // Only after the commit, or a request in between could cache the old account again.
            if (context.RouteData.Values["controller"]?.ToString() == "Users")
                AccountCache.Clear();
        }
        // Disposal rolls back failed validation, exceptions, and partial saves.
    }
}
