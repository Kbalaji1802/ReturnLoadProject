using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ReturnLoad.Shared.Api;

namespace ReturnLoad.Api.Http;

/// <summary>
/// Guarantees the API contract by wrapping <b>every</b> controller result in the
/// standard <see cref="ApiResponse{TData}"/> envelope (ADR-0008). Because this runs
/// globally, a developer cannot forget to envelope a response — returning a bare DTO
/// is enough. Values that are already an <see cref="ApiResponse{TData}"/> (the error
/// paths, which set their own status code and errors) are passed through untouched
/// so nothing is double-wrapped. Actions marked <see cref="SkipResponseEnvelopeAttribute"/>
/// are left as-is.
/// </summary>
public sealed class ResponseEnvelopeResultFilter : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (ShouldWrap(context, out ObjectResult? objectResult))
        {
            string traceId = context.HttpContext.GetCorrelationId();
            var envelope = ApiResponse<object>.Ok(objectResult!.Value!).WithTraceId(traceId);

            context.Result = new ObjectResult(envelope)
            {
                // Preserve the action's intended status (e.g. 201 Created), defaulting to 200.
                StatusCode = objectResult.StatusCode ?? StatusCodes.Status200OK,
            };
        }

        await next();
    }

    private static bool ShouldWrap(ResultExecutingContext context, out ObjectResult? objectResult)
    {
        objectResult = null;

        if (HasSkipAttribute(context))
        {
            return false;
        }

        // Only object-carrying results have a body to wrap. Status-only results
        // (NoContent, files, redirects) are left alone.
        if (context.Result is not ObjectResult candidate || candidate.Value is null)
        {
            return false;
        }

        // Already enveloped (error paths, or an action that opted to build its own) —
        // do not wrap a second time.
        if (IsEnvelope(candidate.Value))
        {
            return false;
        }

        objectResult = candidate;
        return true;
    }

    private static bool HasSkipAttribute(ResultExecutingContext context) =>
        context.ActionDescriptor.EndpointMetadata.OfType<SkipResponseEnvelopeAttribute>().Any();

    private static bool IsEnvelope(object value)
    {
        Type type = value.GetType();
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ApiResponse<>);
    }
}
