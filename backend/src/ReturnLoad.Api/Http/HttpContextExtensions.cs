using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ReturnLoad.Application.Identity;

namespace ReturnLoad.Api.Http;

/// <summary>
/// Convenience accessors for per-request correlation data, so the result filter,
/// exception handler, and status-code handler can stamp the response envelope's
/// <c>traceId</c> without re-reading headers.
/// </summary>
public static class HttpContextExtensions
{
    /// <summary>
    /// The correlation id established by <see cref="CorrelationIdMiddleware"/> for this
    /// request. Falls back to <see cref="HttpContext.TraceIdentifier"/> if the middleware
    /// has not run (e.g. a failure very early in the pipeline).
    /// </summary>
    public static string GetCorrelationId(this HttpContext context) =>
        context.Items.TryGetValue(CorrelationConstants.CorrelationIdItemKey, out object? value)
        && value is string correlationId
            ? correlationId
            : context.TraceIdentifier;

    /// <summary>
    /// Extracts the authenticated user's id from the <c>userId</c> (or <c>sub</c>) claim.
    /// Returns false when unauthenticated or the claim is missing/malformed.
    /// </summary>
    public static bool TryGetUserId(this HttpContext context, out Guid userId)
    {
        string? value = context.User.FindFirstValue(AppClaims.UserId)
            ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");

        return Guid.TryParse(value, out userId);
    }
}
