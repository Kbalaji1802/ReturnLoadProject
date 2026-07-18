using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ReturnLoad.Application.Abstractions.Geo;

namespace ReturnLoad.Infrastructure.Geo;

/// <summary>
/// <see cref="ILocationSearchService"/> over OpenStreetMap's Nominatim geocoder (dev provider;
/// no API key). Parses the structured address so we persist district/state/PIN, not free text
/// (M4.2 §4). Fail-soft: any transport/parse error yields an empty result and a logged warning —
/// autocomplete degrades, it never throws into the request pipeline.
/// </summary>
public sealed class NominatimLocationSearchService : ILocationSearchService
{
    private readonly HttpClient _http;
    private readonly GeoOptions _options;
    private readonly ILogger<NominatimLocationSearchService> _logger;

    public NominatimLocationSearchService(HttpClient http, GeoOptions options, ILogger<NominatimLocationSearchService> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PlaceResult>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        string url =
            $"{_options.NominatimBaseUrl}/search?q={Uri.EscapeDataString(query)}&format=jsonv2" +
            $"&addressdetails=1&limit={_options.SearchLimit}";
        if (!string.IsNullOrWhiteSpace(_options.CountryCodes))
        {
            url += $"&countrycodes={Uri.EscapeDataString(_options.CountryCodes)}";
        }

        try
        {
            using HttpResponseMessage response = await _http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Nominatim search returned {Status} for {Query}", (int)response.StatusCode, query);
                return [];
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            List<PlaceResult> results = [];
            foreach (JsonElement item in doc.RootElement.EnumerateArray())
            {
                if (TryParsePlace(item, out PlaceResult? place))
                {
                    results.Add(place!);
                }
            }

            return results;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Fail-soft: a geocoder outage must not break the flow (T&S "fail closed" applies to
            // verification, not to convenience autocomplete — we simply return no suggestions).
            _logger.LogWarning(ex, "Nominatim search failed for {Query}", query);
            return [];
        }
    }

    private static bool TryParsePlace(JsonElement item, out PlaceResult? place)
    {
        place = null;
        if (!item.TryGetProperty("lat", out JsonElement latEl) || !item.TryGetProperty("lon", out JsonElement lonEl))
        {
            return false;
        }

        if (!double.TryParse(latEl.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double lat) ||
            !double.TryParse(lonEl.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double lon))
        {
            return false;
        }

        string displayName = item.TryGetProperty("display_name", out JsonElement dn) ? dn.GetString() ?? string.Empty : string.Empty;

        string? district = null, state = null, pin = null;
        if (item.TryGetProperty("address", out JsonElement addr) && addr.ValueKind == JsonValueKind.Object)
        {
            district = FirstString(addr, "state_district", "county", "district");
            state = FirstString(addr, "state");
            pin = FirstString(addr, "postcode");
        }

        place = new PlaceResult(displayName, lat, lon, district, state, pin);
        return true;
    }

    private static string? FirstString(JsonElement obj, params string[] keys)
    {
        foreach (string key in keys)
        {
            if (obj.TryGetProperty(key, out JsonElement el) && el.ValueKind == JsonValueKind.String)
            {
                string? value = el.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return null;
    }
}
