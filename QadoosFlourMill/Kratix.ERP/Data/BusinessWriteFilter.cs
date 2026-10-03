using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Kratix.ERP.Data;

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

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await db.Database.ExecuteSqlRawAsync("""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource = 'Kratix.BusinessWrite',
                @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000;
            IF @result < 0 THROW 51000, 'Another update is taking too long. Please retry.', 1;
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
            await transaction.CommitAsync();
        // Disposal rolls back failed validation, exceptions, and partial saves.
    }
}
