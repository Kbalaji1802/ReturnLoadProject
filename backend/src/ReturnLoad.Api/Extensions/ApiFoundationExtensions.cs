using Microsoft.AspNetCore.Mvc;
using ReturnLoad.Api.Http;
using ReturnLoad.Shared.Api;

namespace ReturnLoad.Api.Extensions;

/// <summary>
/// Wires the API contract (ADR-0008): every response goes out through the standard
/// <see cref="ApiResponse{TData}"/> envelope. Call after <c>AddControllers()</c>.
/// </summary>
public static class ApiFoundationExtensions
{
    public static IServiceCollection AddApiFoundation(this IServiceCollection services)
    {
        // Auto-wrap every successful controller result in the envelope.
        services.Configure<MvcOptions>(options =>
            options.Filters.Add<ResponseEnvelopeResultFilter>());

        // Replace the framework's default model-validation 400 (a ProblemDetails)
        // with the standard envelope, carrying one ApiError per invalid field.
        services.Configure<ApiBehaviorOptions>(options =>
            options.InvalidModelStateResponseFactory = BuildValidationResponse);

        return services;
    }

    private static IActionResult BuildValidationResponse(ActionContext context)
    {
        ApiError[] errors = context.ModelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .SelectMany(entry => entry.Value!.Errors.Select(error =>
            {
                string message = string.IsNullOrWhiteSpace(error.ErrorMessage)
                    ? "The value is invalid."
                    : error.ErrorMessage;

                // A blank key means a model-level (non-field) error.
                return string.IsNullOrEmpty(entry.Key)
                    ? ApiError.General(ErrorCodes.ValidationError, message)
                    : ApiError.Validation(entry.Key, message);
            }))
            .ToArray();

        ApiResponse<object> body = ApiResponse<object>
            .Fail(errors, "Validation failed.")
            .WithTraceId(context.HttpContext.GetCorrelationId());

        return new BadRequestObjectResult(body);
    }
}
