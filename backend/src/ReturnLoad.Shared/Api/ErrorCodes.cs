namespace ReturnLoad.Shared.Api;

/// <summary>
/// Stable, machine-readable error codes carried by <see cref="ApiError.Code"/>.
/// Clients branch on these; they are part of the public API contract, so treat
/// changes as breaking. Business modules add their own domain-specific codes
/// (e.g. <c>"LOAD_ALREADY_BOOKED"</c>) alongside these cross-cutting ones.
/// </summary>
public static class ErrorCodes
{
    /// <summary>Input failed validation (HTTP 400).</summary>
    public const string ValidationError = "VALIDATION_ERROR";

    /// <summary>A requested resource does not exist (HTTP 404).</summary>
    public const string NotFound = "NOT_FOUND";

    /// <summary>Authentication is missing or invalid (HTTP 401).</summary>
    public const string Unauthorized = "UNAUTHORIZED";

    /// <summary>Authenticated but not permitted (HTTP 403).</summary>
    public const string Forbidden = "FORBIDDEN";

    /// <summary>The request conflicts with the current state (HTTP 409).</summary>
    public const string Conflict = "CONFLICT";

    /// <summary>An unexpected, unhandled error (HTTP 500).</summary>
    public const string InternalError = "INTERNAL_ERROR";
}
