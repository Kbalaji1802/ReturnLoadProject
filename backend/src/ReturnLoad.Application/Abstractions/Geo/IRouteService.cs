namespace ReturnLoad.Application.Abstractions.Geo;

/// <summary>The road route between two points: distance in km (ADR-0004) and estimated drive time.</summary>
public sealed record RouteResult(decimal DistanceKm, TimeSpan EstimatedDuration);

/// <summary>
/// Computes the road distance and estimated duration between two coordinates so the platform
/// derives them itself — the user is never asked to calculate distance or ETA (M4.2 §"Load
/// creation"). Abstracted so the dev provider (OSRM over OpenStreetMap) can be swapped for
/// Google Routes in production without business changes. Implemented in Infrastructure.
/// <para><b>Fail-soft:</b> returns <c>null</c> when the provider cannot resolve a route — the
/// caller stores the coordinates and leaves distance unknown rather than fabricating one.</para>
/// </summary>
public interface IRouteService
{
    Task<RouteResult?> GetRouteAsync(
        double originLatitude,
        double originLongitude,
        double destinationLatitude,
        double destinationLongitude,
        CancellationToken cancellationToken = default);
}
