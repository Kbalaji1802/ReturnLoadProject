using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReturnLoad.Application.Abstractions.Storage;
using ReturnLoad.Infrastructure.Persistence;
using ReturnLoad.Infrastructure.Storage;
using ReturnLoad.Shared.Diagnostics;

namespace ReturnLoad.Infrastructure;

/// <summary>
/// Infrastructure-layer composition root: database connectivity and other
/// outbound adapters. All configuration is read from <see cref="IConfiguration"/>
/// (environment variables / appsettings) and never hard-coded
/// (03_TECHNICAL_BIBLE.md §8, 01_PROJECT_RULES.md §1).
/// </summary>
public static class DependencyInjection
{
    /// <summary>The connection-string name the platform binds its PostgreSQL database to.</summary>
    public const string DatabaseConnectionName = "ReturnLoadDatabase";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString(DatabaseConnectionName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // Fail loudly (01_PROJECT_RULES.md §1): a missing database connection
            // is a misconfiguration, not something to paper over with a default.
            throw new InvalidOperationException(
                $"Missing connection string '{DatabaseConnectionName}'. Provide it via " +
                "configuration or the ConnectionStrings__ReturnLoadDatabase environment variable.");
        }

        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));

        // Readiness probe: the database must be reachable before we accept traffic.
        services.AddHealthChecks()
            .AddDbContextCheck<ApplicationDbContext>(
                name: "database",
                tags: [HealthCheckTags.Ready]);

        // File storage (ADR-0012): local disk by default; a cloud provider swaps in here
        // later without touching business code.
        services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.SectionName));
        services.AddSingleton<IFileStorageService, LocalDiskFileStorageService>();

        return services;
    }
}
