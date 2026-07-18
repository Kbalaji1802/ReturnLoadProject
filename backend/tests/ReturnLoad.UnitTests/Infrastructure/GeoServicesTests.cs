using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using ReturnLoad.Application.Abstractions.Geo;
using ReturnLoad.Infrastructure.Geo;

namespace ReturnLoad.UnitTests.Infrastructure;

/// <summary>
/// The OpenStreetMap geo adapters (M4.2), exercised against a canned <see cref="HttpMessageHandler"/>
/// so parsing and the fail-soft contract are verified without hitting the network.
/// </summary>
public sealed class GeoServicesTests
{
    private static readonly GeoOptions Options = new();

    [Fact]
    public async Task Nominatim_parses_places_with_structured_address()
    {
        const string json = """
        [
          { "lat": "13.0827", "lon": "80.2707", "display_name": "Chennai, Tamil Nadu, India",
            "address": { "state_district": "Chennai", "state": "Tamil Nadu", "postcode": "600001" } }
        ]
        """;
        var service = new NominatimLocationSearchService(Client(HttpStatusCode.OK, json), Options, NullLogger<NominatimLocationSearchService>.Instance);

        IReadOnlyList<PlaceResult> places = await service.SearchAsync("Chennai");

        PlaceResult place = Assert.Single(places);
        Assert.Equal(13.0827, place.Latitude, 3);
        Assert.Equal(80.2707, place.Longitude, 3);
        Assert.Equal("Chennai", place.District);
        Assert.Equal("Tamil Nadu", place.State);
        Assert.Equal("600001", place.Pin);
    }

    [Fact]
    public async Task Nominatim_returns_empty_when_the_query_is_blank()
    {
        var service = new NominatimLocationSearchService(Client(HttpStatusCode.OK, "[]"), Options, NullLogger<NominatimLocationSearchService>.Instance);

        Assert.Empty(await service.SearchAsync("   "));
    }

    [Fact]
    public async Task Nominatim_fails_soft_on_provider_error()
    {
        var service = new NominatimLocationSearchService(Client(HttpStatusCode.InternalServerError, "boom"), Options, NullLogger<NominatimLocationSearchService>.Instance);

        Assert.Empty(await service.SearchAsync("Chennai"));
    }

    [Fact]
    public async Task Osrm_parses_distance_in_km_and_duration()
    {
        const string json = """{ "code": "Ok", "routes": [ { "distance": 12345.6, "duration": 900.0 } ] }""";
        var service = new OsrmRouteService(Client(HttpStatusCode.OK, json), Options, NullLogger<OsrmRouteService>.Instance);

        RouteResult? route = await service.GetRouteAsync(13.0827, 80.2707, 11.0168, 76.9558);

        Assert.NotNull(route);
        Assert.Equal(12.35m, route!.DistanceKm);
        Assert.Equal(TimeSpan.FromMinutes(15), route.EstimatedDuration);
    }

    [Fact]
    public async Task Osrm_returns_null_when_no_route_is_found()
    {
        const string json = """{ "code": "NoRoute", "routes": [] }""";
        var service = new OsrmRouteService(Client(HttpStatusCode.OK, json), Options, NullLogger<OsrmRouteService>.Instance);

        Assert.Null(await service.GetRouteAsync(13.0827, 80.2707, 11.0168, 76.9558));
    }

    [Fact]
    public async Task Osrm_fails_soft_on_provider_error()
    {
        var service = new OsrmRouteService(Client(HttpStatusCode.BadGateway, "boom"), Options, NullLogger<OsrmRouteService>.Instance);

        Assert.Null(await service.GetRouteAsync(13.0827, 80.2707, 11.0168, 76.9558));
    }

    private static HttpClient Client(HttpStatusCode status, string body) => new(new StubHandler(status, body));

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public StubHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            });
    }
}
