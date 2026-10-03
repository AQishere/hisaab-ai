using Microsoft.EntityFrameworkCore;

namespace Kratix.ERP.Data;

public static class BusinessRules
{
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
