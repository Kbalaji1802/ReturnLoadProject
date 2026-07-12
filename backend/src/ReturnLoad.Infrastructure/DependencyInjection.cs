using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReturnLoad.Application.Abstractions.Identity;
using ReturnLoad.Application.Abstractions.Persistence;
using ReturnLoad.Application.Abstractions.Security;
using ReturnLoad.Application.Abstractions.Storage;
using ReturnLoad.Infrastructure.Identity;
using ReturnLoad.Infrastructure.Identity.Tokens;
using ReturnLoad.Infrastructure.Persistence;
using ReturnLoad.Infrastructure.Security;
using ReturnLoad.Infrastructure.Storage;
using ReturnLoad.Shared.Configuration;
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

        // Field encryption for sensitive columns at rest (Aadhaar) — ADR-0015.
        services.Configure<EncryptionOptions>(configuration.GetSection(EncryptionOptions.SectionName));
        services.AddSingleton<IFieldEncryptor, AesFieldEncryptor>();

        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));

        // Persistence contracts (ADR-0014/0015): generic repository + unit of work.
        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<ApplicationDbContext>());

        // Readiness probe: the database must be reachable before we accept traffic.
        services.AddHealthChecks()
            .AddDbContextCheck<ApplicationDbContext>(
                name: "database",
                tags: [HealthCheckTags.Ready]);

        // File storage (ADR-0012): local disk by default; a cloud provider swaps in here
        // later without touching business code.
        services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.SectionName));
        services.AddSingleton<IFileStorageService, LocalDiskFileStorageService>();

        AddIdentity(services, configuration);

        return services;
    }

    private static void AddIdentity(IServiceCollection services, IConfiguration configuration)
    {
        PasswordPolicyOptions password =
            configuration.GetSection(PasswordPolicyOptions.SectionName).Get<PasswordPolicyOptions>() ?? new PasswordPolicyOptions();

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;

                options.Password.RequiredLength = password.MinLength;
                options.Password.RequireUppercase = password.RequireUppercase;
                options.Password.RequireLowercase = password.RequireLowercase;
                options.Password.RequireDigit = password.RequireDigit;
                options.Password.RequireNonAlphanumeric = password.RequireNonAlphanumeric;

                // Lockout: 5 failed attempts → 15-minute lock (ADR-0013).
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();
        // Default token providers (email-confirmation / password-reset tokens) are added
        // when those flows ship — they are deliberately out of the M2 core (ADR-0013).

        // Token signing is abstracted so HS256 → RS256/JWKS is an implementation swap (ADR-0013).
        services.AddSingleton<ITokenSigner, HmacTokenSigner>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IAuthService, AuthService>();
    }
}
