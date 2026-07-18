using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using ReturnLoad.Api.Configuration;
using ReturnLoad.Api.Extensions;
using ReturnLoad.Api.Http;
using ReturnLoad.Api.Hubs;
using ReturnLoad.Api.Middleware;
using Microsoft.Extensions.DependencyInjection;
using ReturnLoad.Application;
using ReturnLoad.Infrastructure;
using ReturnLoad.Infrastructure.Persistence;
using ReturnLoad.Shared.Diagnostics;
using Serilog;

// A bootstrap logger captures anything that fails before the host is fully built,
// so startup crashes are never silent (01_PROJECT_RULES.md §1).
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    // Structured JSON logging with correlation context (03_TECHNICAL_BIBLE.md §10),
    // fully driven by the "Serilog" configuration section.
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // Hardening options read up front — needed for Kestrel limits and pipeline gating (M1.5).
    SecurityHeaderOptions security = builder.Configuration
        .GetSection(SecurityHeaderOptions.SectionName).Get<SecurityHeaderOptions>() ?? new SecurityHeaderOptions();
    RequestLimitOptions requestLimits = builder.Configuration
        .GetSection(RequestLimitOptions.SectionName).Get<RequestLimitOptions>() ?? new RequestLimitOptions();

    builder.WebHost.ConfigureKestrel(kestrel =>
    {
        kestrel.AddServerHeader = false;                              // don't advertise the server
        kestrel.Limits.MaxRequestBodySize = requestLimits.MaxRequestBodyBytes;
    });

    // Trust the reverse proxy's X-Forwarded-* so scheme/IP are correct behind ingress.
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });

    // ---- Service registration (composition root) ------------------------------
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    builder.Services.AddControllers();
    builder.Services.AddApiFoundation();
    builder.Services.AddSecurityFoundation(builder.Configuration);
    builder.Services.AddApiVersioningConfigured();
    builder.Services.AddSwaggerConfigured();
    builder.Services.AddJwtAuthentication(builder.Configuration);
    builder.Services.AddSignalR();

    // Unhandled exceptions become the standard error envelope (ADR-0008).
    // GlobalExceptionHandler writes the envelope and returns true, so it fully owns
    // the response. AddProblemDetails is registered ONLY because UseExceptionHandler()
    // requires a fallback to exist; it is never emitted on the wire (our handler,
    // the status-code writer, and the validation factory own every error response).
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    WebApplication app = builder.Build();

    // ---- HTTP pipeline --------------------------------------------------------
    // Order matters (M1.5 + ADR-0008): forwarded headers first so scheme/IP are correct;
    // the exception handler wraps everything; transport hardening (HSTS/HTTPS) and
    // security headers apply before app logic; correlation runs so every log line and
    // response carries the ids; the status-code handler envelopes framework errors
    // (401/403/404/429/…); the rate limiter guards downstream work.
    app.UseForwardedHeaders();
    app.UseExceptionHandler();

    if (!app.Environment.IsDevelopment())
    {
        if (security.Hsts)
        {
            app.UseHsts();
        }

        if (security.HttpsRedirection)
        {
            app.UseHttpsRedirection();
        }
    }

    app.UseMiddleware<SecurityHeadersMiddleware>();
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseStatusCodePages(StatusCodeEnvelopeWriter.WriteAsync);
    app.UseRateLimiter();
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseCors(SecurityExtensions.CorsPolicyName);
    app.UseAuthentication();
    app.UseAuthorization();

    // Development convenience: apply migrations and seed demo data so the platform is
    // demoable end-to-end. Never runs outside Development. If the database is unavailable,
    // log loudly and continue so the process can still start for inspection.
    if (app.Environment.IsDevelopment())
    {
        try
        {
            using IServiceScope scope = app.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();
            await DemoDataSeeder.SeedAsync(scope.ServiceProvider);
            Log.Information("Development database migrated and demo data seeded.");
        }
        catch (Exception seedEx)
        {
            Log.Warning(seedEx, "Skipped dev migrate/seed — is PostgreSQL running? (docker compose up)");
        }
    }

    app.MapControllers();
    app.MapHub<NotificationsHub>("/hubs/notifications");

    // Liveness: the process is up. Runs no dependency checks so orchestrators can
    // tell "alive" apart from "not yet ready".
    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = _ => false,
    });

    // Readiness: dependencies (the database) are reachable before accepting traffic.
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains(HealthCheckTags.Ready),
    });

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    // HostAbortedException is the control-flow signal WebApplicationFactory uses to
    // capture the host during integration tests — it must NOT be treated as a crash.
    Log.Fatal(ex, "ReturnLoad API terminated unexpectedly during startup.");
}
finally
{
    Log.CloseAndFlush();
}

// Exposed as a public partial type so the integration-test WebApplicationFactory
// can reference the application's entry point.
public partial class Program;
