using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ReturnLoad.Application.Abstractions.Geo;

namespace ReturnLoad.Infrastructure.Geo;

/// <summary>
/// <see cref="IRouteService"/> over OSRM (OpenStreetMap routing; dev provider, no API key).
/// Returns road distance (km) + estimated duration so the platform derives them itself
/// (M4.2 §"Load creation"). Fail-soft: a transport/parse error or a non-"Ok" route yields
/// <c>null</c> so the caller never fabricates a distance.
/// </summary>
public sealed class OsrmRouteService : IRouteService
{
    private readonly HttpClient _http;
    private readonly GeoOptions _options;
    private readonly ILogger<OsrmRouteService> _logger;

    public OsrmRouteService(HttpClient http, GeoOptions options, ILogger<OsrmRouteService> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;
    }

    public async Task<RouteResult?> GetRouteAsync(
        double originLatitude,
        double originLongitude,
        double destinationLatitude,
        double destinationLongitude,
        CancellationToken cancellationToken = default)
    {
        // OSRM expects lon,lat order.
        string origin = FormatCoord(originLongitude, originLatitude);
        string destination = FormatCoord(destinationLongitude, destinationLatitude);
        string url = $"{_options.OsrmBaseUrl}/route/v1/driving/{origin};{destination}?overview=false";

        try
        {
            using HttpResponseMessage response = await _http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OSRM route returned {Status}", (int)response.StatusCode);
                return null;
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            JsonElement root = doc.RootElement;

            if (!root.TryGetProperty("code", out JsonElement code) || code.GetString() != "Ok")
            {
                return null;
            }

            if (!root.TryGetProperty("routes", out JsonElement routes) ||
                routes.ValueKind != JsonValueKind.Array ||
                routes.GetArrayLength() == 0)
            {
                return null;
            }

            JsonElement first = routes[0];
            double metres = first.TryGetProperty("distance", out JsonElement d) ? d.GetDouble() : 0;
            double seconds = first.TryGetProperty("duration", out JsonElement t) ? t.GetDouble() : 0;

            decimal km = Math.Round((decimal)(metres / 1000.0), 2);
            return new RouteResult(km, TimeSpan.FromSeconds(seconds));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "OSRM route failed");
            return null;
        }
    }

    private static string FormatCoord(double lon, double lat) =>
        string.Create(CultureInfo.InvariantCulture, $"{lon},{lat}");
}
