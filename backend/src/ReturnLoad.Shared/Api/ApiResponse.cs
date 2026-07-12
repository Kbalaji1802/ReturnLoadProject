namespace ReturnLoad.Shared.Api;

/// <summary>
/// The single, consistent envelope returned by <b>every</b> API endpoint — for
/// success <b>and</b> error alike (03_TECHNICAL_BIBLE.md §6, ADR-0008). One stable
/// shape means every client (mobile, admin) deserialises and handles responses
/// uniformly, and a failure is never structurally different from a success.
/// <para>
/// Success: <see cref="Success"/> = true, <see cref="Data"/> populated,
/// <see cref="Errors"/> empty. Error: <see cref="Success"/> = false,
/// <see cref="Data"/> null, <see cref="Errors"/> populated. Serialised camelCase
/// (<c>success</c>, <c>message</c>, <c>data</c>, <c>errors</c>, <c>traceId</c>).
/// </para>
/// </summary>
/// <typeparam name="TData">The payload type; <c>object</c> for error-only responses.</typeparam>
public sealed record ApiResponse<TData>
{
    /// <summary>True when the operation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>A human-readable summary of the outcome (may be empty on success).</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>The payload on success; <see langword="null"/> on error.</summary>
    public TData? Data { get; init; }

    /// <summary>Structured errors on failure; empty on success.</summary>
    public IReadOnlyList<ApiError> Errors { get; init; } = Array.Empty<ApiError>();

    /// <summary>
    /// The correlation id for this response — the id a client quotes to support so
    /// the exact request can be found in the logs. Set at the API boundary.
    /// </summary>
    public string TraceId { get; init; } = string.Empty;

    /// <summary>A successful envelope wrapping <paramref name="data"/>.</summary>
    public static ApiResponse<TData> Ok(TData data, string message = "") =>
        new() { Success = true, Message = message, Data = data };

    /// <summary>A failed envelope carrying one or more <see cref="ApiError"/>s.</summary>
    public static ApiResponse<TData> Fail(IReadOnlyList<ApiError> errors, string message) =>
        new() { Success = false, Message = message, Errors = errors };

    /// <summary>A failed envelope carrying a single <see cref="ApiError"/>.</summary>
    public static ApiResponse<TData> Fail(ApiError error, string message) =>
        Fail(new[] { error }, message);

    /// <summary>Returns a copy stamped with the given correlation id.</summary>
    public ApiResponse<TData> WithTraceId(string traceId) => this with { TraceId = traceId };
}
