namespace ReturnLoad.Shared.Api;

/// <summary>
/// The consistent success envelope returned by API endpoints
/// (03_TECHNICAL_BIBLE.md §6). A stable, predictable shape means every client
/// (mobile, admin) can deserialise responses uniformly. Errors use RFC 7807
/// ProblemDetails instead of this envelope.
/// </summary>
/// <typeparam name="TData">The payload type.</typeparam>
public sealed record ApiResponse<TData>(TData Data)
{
    /// <summary>Wraps <paramref name="data"/> in a success envelope.</summary>
    public static ApiResponse<TData> Ok(TData data) => new(data);
}
