using System.Threading.RateLimiting;
using ReturnLoad.Api.Configuration;
using ReturnLoad.Api.Http;
using ReturnLoad.Shared.Api;
using ReturnLoad.Shared.Configuration;

namespace ReturnLoad.Api.Extensions;

/// <summary>
/// Wires the M1.5 Security Foundation (ADR-0010): options binding, CORS, and rate
/// limiting. Security headers and HTTPS policy are applied in the pipeline
/// (<see cref="SecurityHeadersMiddleware"/> and <c>Program.cs</c>). No business logic.
/// </summary>
public static class SecurityExtensions
{
    /// <summary>The single named CORS policy applied to the API.</summary>
    public const string CorsPolicyName = "ReturnLoadCors";

    /// <summary>Rate-limit policy for high-risk endpoints (login, OTP) — applied in M2.</summary>
    public const string SensitiveRateLimitPolicy = "sensitive";

    public static IServiceCollection AddSecurityFoundation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        BindOptions(services, configuration);

        CorsOptions cors = configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();
        AddCors(services, cors);

        RateLimitOptions rateLimits = configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>() ?? new RateLimitOptions();
        AddRateLimiting(services, rateLimits);

        return services;
    }

    private static void BindOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SecurityHeaderOptions>(configuration.GetSection(SecurityHeaderOptions.SectionName));
        services.Configure<RequestLimitOptions>(configuration.GetSection(RequestLimitOptions.SectionName));
        services.Configure<FileUploadOptions>(configuration.GetSection(FileUploadOptions.SectionName));
        services.Configure<CorsOptions>(configuration.GetSection(CorsOptions.SectionName));
        services.Configure<RateLimitOptions>(configuration.GetSection(RateLimitOptions.SectionName));
        services.Configure<PasswordPolicyOptions>(configuration.GetSection(PasswordPolicyOptions.SectionName));
    }

    private static void AddCors(IServiceCollection services, CorsOptions cors) =>
        services.AddCors(options => options.AddPolicy(CorsPolicyName, policy =>
        {
            // An empty allowlist = no cross-origin browser access (safe default).
            policy.WithOrigins(cors.AllowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .WithExposedHeaders(
                    CorrelationConstants.CorrelationIdHeader,
                    CorrelationConstants.RequestIdHeader);

            if (cors.AllowCredentials)
            {
                policy.AllowCredentials();
            }
        }));

    private static void AddRateLimiting(IServiceCollection services, RateLimitOptions rateLimits) =>
        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Global: one fixed window per client IP.
            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => ToWindowOptions(rateLimits.Global)));

            // Reserved for sensitive endpoints (login/OTP) — attached per-endpoint in M2.
            limiter.AddPolicy(SensitiveRateLimitPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => ToWindowOptions(rateLimits.Sensitive)));

            // Rejections obey the standard envelope + emit a security event.
            limiter.OnRejected = async (context, cancellationToken) =>
            {
                HttpContext http = context.HttpContext;
                http.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                SecurityLog.Write(
                    http,
                    "RateLimited",
                    "Rate limit exceeded on {Method} {Path}",
                    http.Request.Method,
                    http.Request.Path);

                const string message = "Too many requests — please retry later.";
                ApiResponse<object> body = ApiResponse<object>
                    .Fail(ApiError.General(ErrorCodes.TooManyRequests, message), message)
                    .WithTraceId(http.GetCorrelationId());

                await http.Response.WriteAsJsonAsync(body, cancellationToken);
            };
        });

    private static FixedWindowRateLimiterOptions ToWindowOptions(RateLimitWindow window) => new()
    {
        PermitLimit = window.PermitLimit,
        Window = TimeSpan.FromSeconds(window.WindowSeconds),
        QueueLimit = window.QueueLimit,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
    };
}
