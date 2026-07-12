using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ReturnLoad.IntegrationTests;

/// <summary>
/// Boots the API in-memory for HTTP-level tests. A placeholder database connection
/// string is supplied so composition succeeds without a live PostgreSQL — the
/// endpoints exercised here perform no database access. Readiness (which does hit
/// the database) is intentionally not asserted in this offline test suite.
/// </summary>
public sealed class ReturnLoadApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting(
            "ConnectionStrings:ReturnLoadDatabase",
            "Host=localhost;Port=5432;Database=returnload_test;Username=test;Password=test");
    }
}
