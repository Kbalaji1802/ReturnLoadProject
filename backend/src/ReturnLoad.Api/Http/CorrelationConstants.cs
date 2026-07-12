namespace ReturnLoad.Api.Http;

/// <summary>
/// Well-known names for request correlation (03_TECHNICAL_BIBLE.md §10). Centralised
/// so the middleware, log enrichment, and response envelope all agree on one spelling.
/// </summary>
public static class CorrelationConstants
{
    /// <summary>
    /// Header carrying the correlation id — the id that ties together every log line
    /// and downstream call for one logical operation. Echoed back on the response.
    /// </summary>
    public const string CorrelationIdHeader = "X-Correlation-ID";

    /// <summary>Header carrying the per-HTTP-request id (unique to this single request).</summary>
    public const string RequestIdHeader = "X-Request-ID";

    /// <summary><see cref="Microsoft.AspNetCore.Http.HttpContext.Items"/> key for the correlation id.</summary>
    public const string CorrelationIdItemKey = "ReturnLoad.CorrelationId";

    /// <summary>Serilog log property name for the correlation id.</summary>
    public const string CorrelationIdLogProperty = "CorrelationId";

    /// <summary>Serilog log property name for the request id.</summary>
    public const string RequestIdLogProperty = "RequestId";
}
