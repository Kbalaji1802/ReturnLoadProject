using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using ReturnLoad.Api.Extensions;
using ReturnLoad.Api.Hubs;
using ReturnLoad.Api.Middleware;
using ReturnLoad.Application;
using ReturnLoad.Infrastructure;
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

    // ---- Service registration (composition root) ------------------------------
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    builder.Services.AddControllers();
    builder.Services.AddApiVersioningConfigured();
    builder.Services.AddSwaggerConfigured();
    builder.Services.AddJwtAuthentication(builder.Configuration);
    builder.Services.AddSignalR();

    // Consistent RFC 7807 error responses for unhandled exceptions.
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    WebApplication app = builder.Build();

    // ---- HTTP pipeline --------------------------------------------------------
    app.UseExceptionHandler();
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseAuthentication();
    app.UseAuthorization();

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
