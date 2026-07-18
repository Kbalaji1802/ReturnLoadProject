namespace ReturnLoad.Infrastructure.Geo;

/// <summary>
/// Configuration for the geo providers (M4.2). Base URLs are config-driven (never hard-coded,
/// 03_TECHNICAL_BIBLE.md §8) so the dev OpenStreetMap endpoints can be pointed at a self-hosted
/// Nominatim/OSRM — or replaced by a Google adapter — without a code change.
/// </summary>
public sealed class GeoOptions
{
    public const string SectionName = "Geo";

    /// <summary>Nominatim base URL for geocoding/autocomplete (no trailing slash).</summary>
    public string NominatimBaseUrl { get; set; } = "https://nominatim.openstreetmap.org";

    /// <summary>OSRM base URL for road routing (no trailing slash).</summary>
    public string OsrmBaseUrl { get; set; } = "https://router.project-osrm.org";

    /// <summary>Restricts geocoding to a country (ISO code); empty = worldwide. Default India (ADR-0004).</summary>
    public string CountryCodes { get; set; } = "in";

    /// <summary>Max autocomplete candidates to return.</summary>
    public int SearchLimit { get; set; } = 5;

    /// <summary>Per-request timeout. Public OSM endpoints can be slow; keep it modest and fail-soft.</summary>
    public int TimeoutSeconds { get; set; } = 8;

    /// <summary>User-Agent required by the Nominatim usage policy.</summary>
    public string UserAgent { get; set; } = "ReturnLoadPlatform/1.0 (support@returnload.test)";
}
