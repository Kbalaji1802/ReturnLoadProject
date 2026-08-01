namespace ReturnLoad.Application.Abstractions.Geo;

/// <summary>
/// A place candidate returned by location autocomplete — a resolved coordinate plus the
/// structured India address fields we persist (never free text alone; M4.2 §4). Ordered
/// best-match-first by the provider.
/// </summary>
public sealed record PlaceResult(
    string DisplayName,
    double Latitude,
    double Longitude,
    string? District,
    string? State,
    string? Pin);

/// <summary>
/// Resolves a user's typed query into ranked, geocoded places (autocomplete). Abstracted so
/// the development provider (OpenStreetMap / Nominatim) can be swapped for Google Places in
/// production without touching business code (M4.2 plan; ADR-0012 storage-style seam).
/// Implemented in Infrastructure. Providers are best-effort: an outage yields an empty list,
/// never an exception the caller must handle as fatal.
/// </summary>
public interface ILocationSearchService
{
    Task<IReadOnlyList<PlaceResult>> SearchAsync(string query, CancellationToken cancellationToken = default);
}
