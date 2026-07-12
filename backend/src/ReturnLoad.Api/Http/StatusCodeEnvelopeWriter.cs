using Microsoft.AspNetCore.Diagnostics;
using ReturnLoad.Shared.Api;

namespace ReturnLoad.Api.Http;

/// <summary>
/// Writes the standard error envelope for responses that the framework produces
/// <b>outside</b> MVC and would otherwise leave with an empty body — an unmatched
/// route (404), a rejected token (401), a denied authorization (403), a bad method
/// (405). Wired via <c>UseStatusCodePages</c> so these transport-level failures obey
/// the same API contract as everything else (ADR-0008).
/// </summary>
public static class StatusCodeEnvelopeWriter
{
    /// <summary>Handler for <c>app.UseStatusCodePages(...)</c>.</summary>
    public static async Task WriteAsync(StatusCodeContext context)
    {
        HttpContext httpContext = context.HttpContext;
        int status = httpContext.Response.StatusCode;

        (string code, string message) = Describe(status);

        // Surface authn/authz failures as security events for alerting.
        if (status is StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden)
        {
            SecurityLog.Write(
                httpContext,
                status == StatusCodes.Status401Unauthorized ? "AuthenticationFailed" : "AccessForbidden",
                "{Code} on {Method} {Path}",
                code,
                httpContext.Request.Method,
                httpContext.Request.Path);
        }

        ApiResponse<object> body = ApiResponse<object>
            .Fail(ApiError.General(code, message), message)
            .WithTraceId(httpContext.GetCorrelationId());

        await httpContext.Response.WriteAsJsonAsync(body);
    }

    private static (string Code, string Message) Describe(int status) => status switch
    {
        StatusCodes.Status400BadRequest => (ErrorCodes.ValidationError, "The request was malformed."),
        StatusCodes.Status401Unauthorized => (ErrorCodes.Unauthorized, "Authentication is required."),
        StatusCodes.Status403Forbidden => (ErrorCodes.Forbidden, "You do not have permission to access this resource."),
        StatusCodes.Status404NotFound => (ErrorCodes.NotFound, "The requested resource was not found."),
        StatusCodes.Status405MethodNotAllowed => (ErrorCodes.ValidationError, "The HTTP method is not allowed for this resource."),
        StatusCodes.Status413PayloadTooLarge => (ErrorCodes.PayloadTooLarge, "The request body is too large."),
        StatusCodes.Status415UnsupportedMediaType => (ErrorCodes.ValidationError, "The request media type is not supported."),
        StatusCodes.Status429TooManyRequests => (ErrorCodes.TooManyRequests, "Too many requests — please retry later."),
        >= 500 => (ErrorCodes.InternalError, "An unexpected error occurred."),
        _ => (ErrorCodes.ValidationError, "The request could not be processed."),
    };
}
