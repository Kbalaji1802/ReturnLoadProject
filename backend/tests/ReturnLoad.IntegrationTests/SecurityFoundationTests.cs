using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ReturnLoad.Shared.Api;

namespace ReturnLoad.IntegrationTests;

/// <summary>
/// Verifies the M1.5 hardening over real HTTP: security headers, no server banner,
/// CORS allowlist behaviour, and enveloped rate limiting.
/// </summary>
public sealed class SecurityFoundationTests : IClassFixture<ReturnLoadApiFactory>
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private readonly ReturnLoadApiFactory _factory;

    public SecurityFoundationTests(ReturnLoadApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Responses_carry_the_hardening_headers_and_no_server_banner()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/v1/health");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.True(response.Headers.Contains("Content-Security-Policy"));
        Assert.False(response.Headers.Contains("Server"));
    }

    [Fact]
    public async Task Cors_allows_a_permitted_origin()
    {
        HttpClient client = _factory.CreateClient();
        using HttpRequestMessage preflight = new(HttpMethod.Options, "/api/v1/health");
        preflight.Headers.Add("Origin", "https://allowed.test");
        preflight.Headers.Add("Access-Control-Request-Method", "GET");

        HttpResponseMessage response = await client.SendAsync(preflight);

        Assert.Equal(
            "https://allowed.test",
            response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task Cors_denies_an_unlisted_origin()
    {
        HttpClient client = _factory.CreateClient();
        using HttpRequestMessage preflight = new(HttpMethod.Options, "/api/v1/health");
        preflight.Headers.Add("Origin", "https://evil.test");
        preflight.Headers.Add("Access-Control-Request-Method", "GET");

        HttpResponseMessage response = await client.SendAsync(preflight);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Exceeding_the_rate_limit_returns_an_enveloped_429()
    {
        // A dedicated host with a tiny global limit so the test is fast and isolated.
        using ReturnLoadApiFactory factory = new();
        HttpClient client = factory
            .WithWebHostBuilder(builder => builder.UseSetting("RateLimiting:Global:PermitLimit", "2"))
            .CreateClient();

        HttpResponseMessage? limited = null;
        for (int i = 0; i < 5; i++)
        {
            HttpResponseMessage response = await client.GetAsync("/api/v1/health");
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                limited = response;
                break;
            }
        }

        Assert.NotNull(limited);
        await using Stream stream = await limited!.Content.ReadAsStreamAsync();
        ApiResponse<JsonElement> body = (await JsonSerializer.DeserializeAsync<ApiResponse<JsonElement>>(stream, Web))!;
        Assert.False(body.Success);
        Assert.Equal(ErrorCodes.TooManyRequests, Assert.Single(body.Errors).Code);
    }
}
