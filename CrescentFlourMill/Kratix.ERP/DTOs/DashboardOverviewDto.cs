namespace Kratix.ERP.DTOs;

// GET /api/dashboard/overview. Sections the signed-in role may not see are null (and left out of the attention
// list and the activity feed), so the frontend shows whatever comes back.
public class DashboardOverviewDto
{
    public DateTime AsOf { get; set; }
    public List<Kpi> Kpis { get; set; } = [];
    public List<AttentionItem> Attention { get; set; } = [];
    // Every attention check that ran for this role; a check with no matching Attention item (same Key) is clear.
    public List<AttentionCheck> Checks { get; set; } = [];
    public SalesSection? Sales { get; set; }
    public ReceivablesSection? Receivables { get; set; }
    public ProductionSection? Production { get; set; }
    public StockSection? Stock { get; set; }
    public WheatQualitySection? WheatQuality { get; set; }
    public List<ActivityItem> Activity { get; set; } = [];
}

// Format: money, kg, bags or percent. Change is a percent (or points for percent values); null when there is
// nothing to compare against.
public record Kpi(string Key, string Label, decimal? Value, string Format, string Caption, decimal? Change, string? ChangeLabel);

// Severity: error, warning or info. Link is the ERP page that resolves it.
public record AttentionItem(string Key, string Severity, string Title, string Detail, int? Count, decimal? Amount, string Link);

public record AttentionCheck(string Key, string Label);

public record ActivityItem(DateTime At, string Kind, string Title, string Detail, string Link);

public class SalesSection
{
    public List<DailyAmount> Days { get; set; } = [];
    public decimal Last30Days { get; set; }
    public decimal Previous30Days { get; set; }
    public decimal MonthToDate { get; set; }
    public decimal LastMonthToDate { get; set; }
}

public record DailyAmount(DateTime Date, decimal Amount, int Orders);

public class ReceivablesSection
{
    public decimal NotDue { get; set; }
    public decimal Days1To30 { get; set; }
    public decimal Days31To60 { get; set; }
    public decimal Days61Plus { get; set; }
    public decimal Total { get; set; }
    public List<Debtor> TopDebtors { get; set; } = [];
}

public record Debtor(string CustomerName, decimal Balance, int Invoices, int OldestDaysOverdue);

public class ProductionSection
{
    public List<DailyMilling> Days { get; set; } = [];
    public decimal MilledLast7Days { get; set; }
    public decimal MilledLast30Days { get; set; }
    public decimal? YieldToday { get; set; }
    public decimal? YieldLast7Days { get; set; }
    public decimal? YieldLast30Days { get; set; }
}

public record DailyMilling(DateTime Date, decimal InputKg, decimal OutputKg, decimal? Yield, int Batches);

public class StockSection
{
    public List<ProductStock> Products { get; set; } = [];
    public decimal WheatKg { get; set; }
    public decimal BulkKg { get; set; }
    public decimal FlourBags { get; set; }
    // Purchasing roles only.
    public decimal? WheatExpectedKg { get; set; }
    public int? WheatExpectedOrders { get; set; }
}

// DaysLeft is null when nothing was dispatched in the last 30 days.
public record ProductStock(string ProductName, string Category, string Unit, decimal Quantity, decimal DispatchedPerDay, int? DaysLeft);

public class WheatQualitySection
{
    public int Loads { get; set; }
    public int Rejected { get; set; }
    public decimal? AvgMoisture { get; set; }
    public decimal? AvgImpurities { get; set; }
    public List<SupplierQuality> BySupplier { get; set; } = [];
}

public record SupplierQuality(string SupplierName, int Loads, int Rejected, decimal? AvgMoisture, decimal? AvgImpurities, decimal NetKg);
