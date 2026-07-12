using System.Net;

namespace ReturnLoad.IntegrationTests;

public sealed class HealthEndpointsTests : IClassFixture<ReturnLoadApiFactory>
{
    private readonly ReturnLoadApiFactory _factory;

    public HealthEndpointsTests(ReturnLoadApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Liveness_probe_returns_200_ok()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Versioned_health_endpoint_returns_a_healthy_payload_in_the_envelope()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/v1/health");

        response.EnsureSuccessStatusCode();
        string body = await response.Content.ReadAsStringAsync();
        // The bare controller payload must arrive auto-wrapped in the standard envelope.
        Assert.Contains("\"success\":true", body);
        Assert.Contains("Healthy", body);
        Assert.Contains("ReturnLoad.Api", body);
    }
}
