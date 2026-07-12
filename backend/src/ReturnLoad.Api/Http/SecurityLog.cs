namespace ReturnLoad.Api.Http;

/// <summary>
/// Emits security-relevant events (auth failures, forbidden access, rate-limit hits)
/// as structured logs tagged with a <c>SecurityEvent</c> property, so they are trivially
/// filtered and alerted on (03_TECHNICAL_BIBLE.md §7, §10). Correlation ids are already
/// on the Serilog LogContext via <see cref="CorrelationIdMiddleware"/>, so each event is
/// tied to its request.
/// </summary>
public static class SecurityLog
{
    private const string Category = "ReturnLoad.Security";

    /// <summary>Logs a security event at Warning against the given context's logger factory.</summary>
    public static void Write(HttpContext context, string securityEvent, string messageTemplate, params object?[] args)
    {
        ILoggerFactory factory = context.RequestServices.GetRequiredService<ILoggerFactory>();
        ILogger logger = factory.CreateLogger(Category);

        using (logger.BeginScope(new Dictionary<string, object> { ["SecurityEvent"] = securityEvent }))
        {
            logger.LogWarning(messageTemplate, args);
        }
    }
}
