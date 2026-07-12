using Microsoft.Extensions.Options;
using ReturnLoad.Api.Configuration;

namespace ReturnLoad.Api.Http;

/// <summary>
/// Adds hardening response headers to every response (03_TECHNICAL_BIBLE.md §7):
/// no MIME sniffing, no framing, minimal referrer leakage, and a restrictive
/// Content-Security-Policy. The API returns JSON, so a near-empty CSP is correct —
/// except the Swagger UI (Development only) needs inline script/style, so CSP is
/// skipped for that path.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SecurityHeaderOptions _options;
    private readonly bool _isDevelopment;

    public SecurityHeadersMiddleware(
        RequestDelegate next,
        IOptions<SecurityHeaderOptions> options,
        IHostEnvironment environment)
    {
        _next = next;
        _options = options.Value;
        _isDevelopment = environment.IsDevelopment();
    }

    public Task InvokeAsync(HttpContext context)
    {
        if (_options.SecurityHeaders)
        {
            context.Response.OnStarting(() =>
            {
                IHeaderDictionary headers = context.Response.Headers;
                headers["X-Content-Type-Options"] = "nosniff";
                headers["X-Frame-Options"] = "DENY";
                headers["Referrer-Policy"] = "no-referrer";
                headers["X-Permitted-Cross-Domain-Policies"] = "none";

                // Swagger UI (Development only) needs a relaxed CSP to render.
                bool isSwagger = _isDevelopment
                    && context.Request.Path.StartsWithSegments("/swagger");
                if (!isSwagger && !string.IsNullOrWhiteSpace(_options.ContentSecurityPolicy))
                {
                    headers["Content-Security-Policy"] = _options.ContentSecurityPolicy;
                }

                return Task.CompletedTask;
            });
        }

        return _next(context);
    }
}
