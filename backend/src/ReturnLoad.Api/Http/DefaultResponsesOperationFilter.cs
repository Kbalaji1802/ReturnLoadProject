using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ReturnLoad.Api.Http;

/// <summary>
/// Documents, on every operation, that error responses use the same standard
/// envelope (ADR-0008). Keeps the OpenAPI description honest without each action
/// having to repeat <c>[ProducesResponseType]</c> for the common failure codes.
/// </summary>
public sealed class DefaultResponsesOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Responses.TryAdd(
            "400",
            new OpenApiResponse { Description = "Validation failed — standard error envelope (success=false, errors[])." });
        operation.Responses.TryAdd(
            "500",
            new OpenApiResponse { Description = "Unexpected error — standard error envelope (success=false)." });
    }
}
