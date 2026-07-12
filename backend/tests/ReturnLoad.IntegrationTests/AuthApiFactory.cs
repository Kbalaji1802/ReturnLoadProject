using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReturnLoad.Infrastructure.Identity;
using ReturnLoad.Infrastructure.Persistence;

namespace ReturnLoad.IntegrationTests;

/// <summary>
/// Boots the API for authentication tests, swapping the Npgsql database for an EF Core
/// **in-memory** store (no native SQLite dependency, so nothing for the audit gate to
/// flag). Identity validates email uniqueness in code and our refresh logic looks tokens
/// up by hash, so DB-level constraints are not required for these behaviours. Roles are
/// seeded on host build.
/// </summary>
public sealed class AuthApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        // AddInfrastructure requires a connection string to register (it is then replaced).
        builder.UseSetting(
            "ConnectionStrings:ReturnLoadDatabase",
            "Host=localhost;Port=5432;Database=returnload_test;Username=test;Password=test");
        builder.UseSetting("Security:HttpsRedirection", "false");
        builder.UseSetting("Jwt:Issuer", "https://returnload.test");
        builder.UseSetting("Jwt:Audience", "returnload-tests");
        builder.UseSetting("Jwt:SigningKey", "integration-test-signing-key-0123456789-abcdefghij");

        // Raise rate limits so functional auth tests aren't throttled (all in-memory
        // requests share one client-IP partition). Rate-limit behaviour is covered
        // separately in SecurityFoundationTests with a deliberately low limit.
        builder.UseSetting("RateLimiting:Global:PermitLimit", "100000");
        builder.UseSetting("RateLimiting:Sensitive:PermitLimit", "100000");

        builder.ConfigureTestServices(services =>
        {
            RemoveDbContextRegistrations(services);
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase("returnload-auth-tests"));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        IHost host = base.CreateHost(builder);

        using IServiceScope scope = host.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        services.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated();
        IdentitySeeder.SeedRolesAsync(services.GetRequiredService<RoleManager<ApplicationRole>>())
            .GetAwaiter().GetResult();

        return host;
    }

    private static void RemoveDbContextRegistrations(IServiceCollection services)
    {
        List<ServiceDescriptor> toRemove = services
            .Where(descriptor =>
                descriptor.ServiceType == typeof(DbContextOptions<ApplicationDbContext>)
                || descriptor.ServiceType == typeof(ApplicationDbContext)
                || (descriptor.ServiceType.FullName?.Contains("IDbContextOptionsConfiguration", StringComparison.Ordinal) ?? false))
            .ToList();

        foreach (ServiceDescriptor descriptor in toRemove)
        {
            services.Remove(descriptor);
        }
    }
}
