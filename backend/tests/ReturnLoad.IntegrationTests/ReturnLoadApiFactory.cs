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

        // In-memory tests run over plain HTTP: disable the HTTPS redirect so requests
        // aren't 307'd. Static security headers remain on for assertions.
        builder.UseSetting("Security:HttpsRedirection", "false");

        // Production requires JWT settings (JwtOptionsValidator fails fast otherwise).
        // Supply test values so the host boots — no tokens are issued here.
        builder.UseSetting("Jwt:Issuer", "https://returnload.test");
        builder.UseSetting("Jwt:Audience", "returnload-tests");
        builder.UseSetting("Jwt:SigningKey", "integration-test-signing-key-0123456789-abcdefghij");

        // A permitted CORS origin so the cross-origin behaviour can be asserted.
        builder.UseSetting("Cors:AllowedOrigins:0", "https://allowed.test");
    }
}
