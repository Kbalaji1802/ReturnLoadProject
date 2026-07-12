namespace ReturnLoad.Shared.Api;

/// <summary>
/// The standard paging request for list endpoints. Callers supply <c>?page=</c> and
/// <c>?pageSize=</c>; the normalised values are always safe to use directly against
/// the database — <see cref="Page"/> is at least 1 and <see cref="PageSize"/> is
/// clamped to <see cref="MinPageSize"/>..<see cref="MaxPageSize"/> so a client can
/// never request an unbounded page (03_TECHNICAL_BIBLE.md §6).
/// </summary>
public sealed record PaginationQuery
{
    /// <summary>Smallest permitted page size.</summary>
    public const int MinPageSize = 1;

    /// <summary>Largest permitted page size — a hard cap against oversized queries.</summary>
    public const int MaxPageSize = 100;

    /// <summary>Default page size when the caller does not specify one.</summary>
    public const int DefaultPageSize = 20;

    private readonly int _page = 1;
    private readonly int _pageSize = DefaultPageSize;

    /// <summary>The requested page (1-based). Values below 1 are normalised to 1.</summary>
    public int Page
    {
        get => _page;
        init => _page = value < 1 ? 1 : value;
    }

    /// <summary>The requested page size, clamped to <see cref="MinPageSize"/>..<see cref="MaxPageSize"/>.</summary>
    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = Math.Clamp(value <= 0 ? DefaultPageSize : value, MinPageSize, MaxPageSize);
    }

    /// <summary>The number of records to skip to reach <see cref="Page"/>.</summary>
    public int Skip => (Page - 1) * PageSize;

    /// <summary>The number of records to take for <see cref="Page"/> (alias of <see cref="PageSize"/>).</summary>
    public int Take => PageSize;
}
