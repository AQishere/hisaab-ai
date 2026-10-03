using Microsoft.EntityFrameworkCore;

namespace Kratix.ERP.Data;

// A rule violation found while writing; BusinessWriteFilter returns its message as a 400.
public sealed class BusinessRuleException(string message) : Exception(message);

public static class BusinessRules
{
    // Weighbridge loads rarely match the order exactly. Keep in sync with CK_POLine_Quantities.
    public const decimal OverReceiptTolerance = 0.05m;
    public static decimal MaxReceivable(decimal ordered) => decimal.Round(ordered * (1 + OverReceiptTolerance), 3, MidpointRounding.ToZero);

    public static string? Reason(string? reason) => string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500 ? null : reason.Trim();

    public static bool Quantity(decimal value) => value > 0 && value <= 999999999999.999m && decimal.Round(value, 3) == value;
    public static bool Money(decimal value, bool allowZero = false) =>
        (allowZero ? value >= 0 : value > 0) && value <= 9999999999999.99m && decimal.Round(value, 2) == value;
    public static decimal Amount(decimal quantity, decimal rate) => decimal.Round(quantity * rate, 2, MidpointRounding.AwayFromZero);

    public static async Task<string> NextNumber(AppDbContext db, string prefix)
    {
        // Fixed allowlist: identifiers cannot be passed as SQL parameters.
        var sql = prefix switch
        {
            "PO" => "SELECT NEXT VALUE FOR dbo.PurchaseOrderNumbers AS Value",
            "SO" => "SELECT NEXT VALUE FOR dbo.SalesOrderNumbers AS Value",
            "GE" => "SELECT NEXT VALUE FOR dbo.ReceiptNumbers AS Value",
            "MB" => "SELECT NEXT VALUE FOR dbo.BatchNumbers AS Value",
            "INV" => "SELECT NEXT VALUE FOR dbo.InvoiceNumbers AS Value",
            _ => throw new ArgumentOutOfRangeException(nameof(prefix))
        };
        var values = await db.Database.SqlQueryRaw<long>(sql).ToListAsync();
        return $"{prefix}-{DateTime.Now.Year}-{values.Single():D3}";
    }

    public static async Task RecalculateCustomerBalance(AppDbContext db, int customerId)
    {
        var customer = await db.Customers.FindAsync(customerId);
        if (customer == null) return;
        var billed = await db.Invoices.Where(i => i.CustomerID == customerId).SumAsync(i => (decimal?)i.NetAmount) ?? 0;
        var paid = await db.Payments.Where(p => p.Invoice!.CustomerID == customerId).SumAsync(p => (decimal?)p.PaymentAmount) ?? 0;
        customer.OutstandingBalance = billed - paid;
        await db.SaveChangesAsync();
    }
}
