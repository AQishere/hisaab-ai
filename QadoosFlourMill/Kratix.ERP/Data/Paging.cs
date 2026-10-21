using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kratix.ERP.Data;

// Optional paging for the lists that grow every day. Without ?page the endpoints return the whole list as before
// (the AI assistant and scripts read them that way); with ?page they return one page, newest first.
public sealed class ListQuery
{
    [FromQuery(Name = "page")] public int? Page { get; set; }
    [FromQuery(Name = "pageSize")] public int PageSize { get; set; } = 25;
    // Matches document numbers, names and statuses, ignoring case.
    [FromQuery(Name = "search")] public string? Search { get; set; }
    [FromQuery(Name = "status")] public string? Status { get; set; }

    public const int MaxPageSize = 100;

    public bool Paged => Page.HasValue;

    // A LIKE pattern for Search, or null when there is nothing to search for. % and _ typed by the user match
    // themselves, not "anything".
    public string? Pattern => string.IsNullOrWhiteSpace(Search)
        ? null
        : "%" + Search.Trim().Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_") + "%";
}

public sealed class PagedResult<T>
{
    public List<T> Items { get; init; } = [];
    public int Total { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    // For lists whose header shows a money total over every matching row (payments received).
    public decimal? TotalAmount { get; init; }
}

public static class Paging
{
    // The query must already be ordered. Returns the page's entities and the count of all matching rows.
    public static async Task<(List<T> Items, int Total, int Page, int PageSize)> PageAsync<T>(IQueryable<T> query, ListQuery list)
    {
        var size = Math.Clamp(list.PageSize, 1, ListQuery.MaxPageSize);
        var total = await query.CountAsync();
        var lastPage = Math.Max(1, (total + size - 1) / size);
        var page = Math.Clamp(list.Page ?? 1, 1, lastPage);
        var items = await query.Skip((page - 1) * size).Take(size).ToListAsync();
        return (items, total, page, size);
    }

    public static PagedResult<TOut> Result<TIn, TOut>((List<TIn> Items, int Total, int Page, int PageSize) page,
        Func<TIn, TOut> map, decimal? totalAmount = null) => new()
    {
        Items = page.Items.Select(map).ToList(),
        Total = page.Total,
        Page = page.Page,
        PageSize = page.PageSize,
        TotalAmount = totalAmount,
    };
}
