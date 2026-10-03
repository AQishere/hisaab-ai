namespace Kratix.ERP.DTOs;

public class DashboardMetricsDto
{
    public decimal WheatStockKg { get; set; }
    public int FlourStockBags { get; set; }
    public decimal TodayMillingKg { get; set; }
    public decimal TodayYieldPercent { get; set; }
    public int PendingPOs { get; set; }
    public decimal SalesToday { get; set; }
    public decimal OutstandingReceivables { get; set; }
}
