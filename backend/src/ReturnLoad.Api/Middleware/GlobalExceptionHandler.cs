using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using ReturnLoad.Api.Http;
using ReturnLoad.Domain.Common;
using ReturnLoad.Shared.Api;

namespace ReturnLoad.Api.Middleware;

/// <summary>
/// Translates any unhandled exception into the standard <see cref="ApiResponse{TData}"/>
/// error envelope (ADR-0008), so even failures follow the one API contract. The
/// exception is always logged — we never swallow errors silently
/// (01_PROJECT_RULES.md §1) — but internal details are kept out of the client
/// response to avoid leaking implementation information.
/// <list type="bullet">
/// <item><see cref="ValidationException"/> (FluentValidation) → 400 with a
/// per-field <see cref="ApiError"/> for each failure.</item>
/// <item>everything else → 500 with a single <c>INTERNAL_ERROR</c>.</item>
/// </list>
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        string traceId = httpContext.GetCorrelationId();

        (int status, ApiResponse<object> body) = exception switch
        {
            ValidationException validation => (
                StatusCodes.Status400BadRequest,
                BuildValidationResponse(validation, httpContext)),
            DomainException domain => (
                StatusCodes.Status400BadRequest,
                BuildDomainResponse(domain, httpContext)),
            _ => (
                StatusCodes.Status500InternalServerError,
                BuildUnexpectedResponse(exception, httpContext)),
        };

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(body.WithTraceId(traceId), cancellationToken);
        return true;
    }

    private ApiResponse<object> BuildValidationResponse(ValidationException exception, HttpContext httpContext)
    {
        // Expected, client-caused failure — log at Warning, not Error.
        _logger.LogWarning(
            exception,
            "Validation failed while processing {Method} {Path}",
            httpContext.Request.Method,
            httpContext.Request.Path);

        ApiError[] errors = exception.Errors
            .Select(failure => ApiError.Validation(
                failure.PropertyName,
                failure.ErrorMessage,
                string.IsNullOrWhiteSpace(failure.ErrorCode) ? ErrorCodes.ValidationError : failure.ErrorCode))
            .ToArray();

        return ApiResponse<object>.Fail(errors, "Validation failed.");
    }

    private ApiResponse<object> BuildDomainResponse(DomainException exception, HttpContext httpContext)
    {
        // A broken domain invariant that slipped past validation — client-caused, log at Warning.
        _logger.LogWarning(
            exception,
            "Domain rule '{Code}' violated while processing {Method} {Path}",
            exception.Code,
            httpContext.Request.Method,
            httpContext.Request.Path);

        return ApiResponse<object>.Fail(
            ApiError.General(exception.Code.ToUpperInvariant(), exception.Message),
            exception.Message);
    }

    private ApiResponse<object> BuildUnexpectedResponse(Exception exception, HttpContext httpContext)
    {
        _logger.LogError(
            exception,
            "Unhandled exception while processing {Method} {Path}",
            httpContext.Request.Method,
            httpContext.Request.Path);

        return ApiResponse<object>.Fail(
            ApiError.General(ErrorCodes.InternalError, "An unexpected error occurred."),
            "An unexpected error occurred.");
    }
}
