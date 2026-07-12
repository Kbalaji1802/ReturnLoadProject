namespace ReturnLoad.Shared.Api;

/// <summary>
/// A single page of a larger result set for list endpoints
/// (03_TECHNICAL_BIBLE.md §6: "Pagination, filtering, sorting standardized across
/// list endpoints"). Page numbering is 1-based.
/// </summary>
/// <typeparam name="TItem">The type of the items on the page.</typeparam>
public sealed record PagedResult<TItem>(
    IReadOnlyList<TItem> Items,
    int Page,
    int PageSize,
    long TotalCount)
{
    /// <summary>Total number of pages given <see cref="PageSize"/>.</summary>
    public int TotalPages => PageSize <= 0
        ? 0
        : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPreviousPage => Page > 1;

    public bool HasNextPage => Page < TotalPages;
}
