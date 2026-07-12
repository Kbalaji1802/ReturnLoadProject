using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ReturnLoad.Api.Http;
using ReturnLoad.Shared.Api;
using ReturnLoad.Shared.Results;

namespace ReturnLoad.Api.Extensions;

/// <summary>
/// Translates the application's <see cref="Result"/> / <see cref="Result{TValue}"/>
/// outcomes into enveloped HTTP responses (ADR-0008). Controllers stay thin: they
/// return <c>result.ToApiResult()</c> and get the right status code plus a uniform
/// <see cref="ApiResponse{TData}"/> body for free. Success bodies are still wrapped
/// by <see cref="ResponseEnvelopeResultFilter"/>; failures are enveloped here so the
/// status code and error list travel together.
/// </summary>
public static class ResultExtensions
{
    /// <summary>Maps a valueless <see cref="Result"/> to an enveloped action result.</summary>
    public static IActionResult ToApiResult(this Result result, HttpContext httpContext, string successMessage = "") =>
        result.IsSuccess
            ? new OkObjectResult(ApiResponse<object?>.Ok(null, successMessage).WithTraceId(httpContext.GetCorrelationId()))
            : Failure(result.Error, httpContext);

    /// <summary>Maps a <see cref="Result{TValue}"/> to an enveloped action result.</summary>
    public static IActionResult ToApiResult<TValue>(this Result<TValue> result, HttpContext httpContext, string successMessage = "") =>
        result.IsSuccess
            ? new OkObjectResult(ApiResponse<TValue>.Ok(result.Value, successMessage).WithTraceId(httpContext.GetCorrelationId()))
            : Failure(result.Error, httpContext);

    private static ObjectResult Failure(Error error, HttpContext httpContext)
    {
        (int status, string code) = Map(error.Code);

        ApiResponse<object> body = ApiResponse<object>
            .Fail(ApiError.General(code, error.Message), error.Message)
            .WithTraceId(httpContext.GetCorrelationId());

        return new ObjectResult(body) { StatusCode = status };
    }

    /// <summary>
    /// Maps a domain <see cref="Error.Code"/> to an HTTP status and the public
    /// <see cref="ErrorCodes"/> value. Unknown codes are treated as a 400 so a new,
    /// unmapped domain error never accidentally reports success.
    /// </summary>
    private static (int Status, string Code) Map(string errorCode) => errorCode switch
    {
        "validation_error" => (StatusCodes.Status400BadRequest, ErrorCodes.ValidationError),
        "not_found" => (StatusCodes.Status404NotFound, ErrorCodes.NotFound),
        "conflict" => (StatusCodes.Status409Conflict, ErrorCodes.Conflict),
        "unauthorized" => (StatusCodes.Status401Unauthorized, ErrorCodes.Unauthorized),
        _ => (StatusCodes.Status400BadRequest, ErrorCodes.ValidationError),
    };
}
