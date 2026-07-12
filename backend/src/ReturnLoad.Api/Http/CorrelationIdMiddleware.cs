using Serilog.Context;

namespace ReturnLoad.Api.Http;

/// <summary>
/// Establishes request correlation for every request (03_TECHNICAL_BIBLE.md §10):
/// <list type="bullet">
/// <item>reads an inbound <c>X-Correlation-ID</c>, or mints one if absent, so a
/// caller can trace a logical operation across services;</item>
/// <item>exposes the correlation id and a per-request id as response headers;</item>
/// <item>pushes both onto the Serilog <see cref="LogContext"/> so every log line for
/// this request carries them.</item>
/// </list>
/// Registered early in the pipeline so downstream components (logging, the response
/// envelope, the exception handler) can all rely on the ids being present.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId = ResolveCorrelationId(context);
        string requestId = context.TraceIdentifier;

        context.Items[CorrelationConstants.CorrelationIdItemKey] = correlationId;

        // Headers must be set before the response body starts streaming.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationConstants.CorrelationIdHeader] = correlationId;
            context.Response.Headers[CorrelationConstants.RequestIdHeader] = requestId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty(CorrelationConstants.CorrelationIdLogProperty, correlationId))
        using (LogContext.PushProperty(CorrelationConstants.RequestIdLogProperty, requestId))
        {
            await _next(context);
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationConstants.CorrelationIdHeader, out var header))
        {
            string? incoming = header.ToString();
            if (!string.IsNullOrWhiteSpace(incoming))
            {
                return incoming.Trim();
            }
        }

        return Guid.NewGuid().ToString("N");
    }
}
