namespace ReturnLoad.Shared.Api;

/// <summary>
/// One standard page of a larger result set — the <see cref="ApiResponse{TData}.Data"/>
/// payload of every list endpoint (03_TECHNICAL_BIBLE.md §6: "Pagination … standardised
/// across list endpoints"). Page numbering is 1-based. Serialised as
/// <c>page</c>, <c>pageSize</c>, <c>totalRecords</c>, <c>totalPages</c>, <c>items</c>.
/// </summary>
/// <typeparam name="TItem">The type of the items on the page.</typeparam>
public sealed record PagedResult<TItem>
{
    public required int Page { get; init; }

    public required int PageSize { get; init; }

    public required long TotalRecords { get; init; }

    public required IReadOnlyList<TItem> Items { get; init; }

    /// <summary>Total number of pages given <see cref="PageSize"/>.</summary>
    public int TotalPages => PageSize <= 0
        ? 0
        : (int)Math.Ceiling(TotalRecords / (double)PageSize);

    public bool HasPreviousPage => Page > 1;

    public bool HasNextPage => Page < TotalPages;

    /// <summary>Assembles a page from its items and paging metadata.</summary>
    public static PagedResult<TItem> Create(
        IReadOnlyList<TItem> items,
        int page,
        int pageSize,
        long totalRecords) =>
        new()
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalRecords = totalRecords,
        };
}
