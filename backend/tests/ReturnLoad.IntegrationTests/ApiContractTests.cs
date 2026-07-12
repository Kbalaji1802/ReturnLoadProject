using System.Net;
using System.Text.Json;
using ReturnLoad.Api.Http;
using ReturnLoad.Shared.Api;

namespace ReturnLoad.IntegrationTests;

/// <summary>
/// End-to-end checks that the API contract (ADR-0008) holds over real HTTP: every
/// response is the standard envelope, correlation ids are present and honoured, and
/// even framework-level errors (404) are enveloped.
/// </summary>
public sealed class ApiContractTests : IClassFixture<ReturnLoadApiFactory>
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private readonly ReturnLoadApiFactory _factory;

    public ApiContractTests(ReturnLoadApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Successful_response_is_a_populated_envelope_with_correlation_headers()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/v1/health");

        ApiResponse<JsonElement> body = await ReadEnvelopeAsync(response);
        Assert.True(body.Success);
        Assert.Empty(body.Errors);
        Assert.False(string.IsNullOrEmpty(body.TraceId));
        Assert.Equal("Healthy", body.Data.GetProperty("status").GetString());

        Assert.True(response.Headers.Contains(CorrelationConstants.CorrelationIdHeader));
        Assert.True(response.Headers.Contains(CorrelationConstants.RequestIdHeader));
    }

    [Fact]
    public async Task Supplied_correlation_id_is_echoed_back_and_used_as_trace_id()
    {
        HttpClient client = _factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/health");
        request.Headers.Add(CorrelationConstants.CorrelationIdHeader, "test-corr-123");

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal("test-corr-123", response.Headers.GetValues(CorrelationConstants.CorrelationIdHeader).Single());
        ApiResponse<JsonElement> body = await ReadEnvelopeAsync(response);
        Assert.Equal("test-corr-123", body.TraceId);
    }

    [Fact]
    public async Task Unknown_route_returns_an_enveloped_404_error()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/v1/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        ApiResponse<JsonElement> body = await ReadEnvelopeAsync(response);
        Assert.False(body.Success);
        Assert.Equal(ErrorCodes.NotFound, Assert.Single(body.Errors).Code);
        Assert.False(string.IsNullOrEmpty(body.TraceId));
    }

    private static async Task<ApiResponse<JsonElement>> ReadEnvelopeAsync(HttpResponseMessage response)
    {
        await using Stream stream = await response.Content.ReadAsStreamAsync();
        return (await JsonSerializer.DeserializeAsync<ApiResponse<JsonElement>>(stream, Web))!;
    }
}
