using ReturnLoad.Application.Abstractions.Persistence;
using ReturnLoad.Application.Abstractions.Realtime;
using ReturnLoad.Domain.Identity;
using ReturnLoad.Domain.Loads;
using ReturnLoad.Domain.Tracking;
using ReturnLoad.Domain.Trips;
using ReturnLoad.Domain.ValueObjects;
using ReturnLoad.Shared.Results;

namespace ReturnLoad.Application.UseCases.Tracking;

// ---- Contracts --------------------------------------------------------------

/// <summary>A location update uploaded by the driver's device (M6).</summary>
public sealed record RecordLocationRequest(
    double Latitude, double Longitude, DateTimeOffset CapturedAtUtc,
    double? SpeedKph = null, double? HeadingDegrees = null, double? AccuracyMetres = null, double? BatteryLevel = null);

public sealed record TrackingPointView(
    Guid Id, double Latitude, double Longitude, DateTimeOffset CapturedAtUtc,
    double? SpeedKph, double? HeadingDegrees, double? AccuracyMetres);

/// <summary>The owner/admin live view of a trip: latest position + distance remaining + ETA.</summary>
public sealed record TripLiveView(
    Guid TripId, TripStatus Status, bool HasLocation,
    double? Latitude, double? Longitude, DateTimeOffset? LastUpdateUtc,
    double? DistanceRemainingKm, int? EtaMinutes);

// ---- Service ----------------------------------------------------------------

/// <summary>
/// Live GPS tracking (M6). Ingests driver location updates, serves the trip breadcrumb, the live
/// position (with distance-remaining + ETA), and trip analytics. Authorization is enforced here:
/// <b>only the assigned driver may upload</b>; the trip's driver, the load owner, and privileged
/// staff may read. Provider-agnostic — the business layer knows only lat/lng/time/speed/etc.
/// </summary>
public interface ITrackingService
{
    Task<Result> RecordLocationAsync(Guid authUserId, Guid tripId, RecordLocationRequest request, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<TrackingPointView>>> GetHistoryAsync(Guid authUserId, Guid tripId, bool privileged, CancellationToken cancellationToken = default);

    Task<Result<TripLiveView>> GetLiveAsync(Guid authUserId, Guid tripId, bool privileged, CancellationToken cancellationToken = default);

    Task<Result<TripTrackingSummary>> GetSummaryAsync(Guid authUserId, Guid tripId, bool privileged, CancellationToken cancellationToken = default);
}

internal sealed class TrackingService : ITrackingService
{
    private readonly IRepository<Trip> _trips;
    private readonly IRepository<TrackingEvent> _tracking;
    private readonly IRepository<UserProfile> _users;
    private readonly IRepository<DriverProfile> _drivers;
    private readonly IRepository<Load> _loads;
    private readonly ILiveTrackingNotifier _liveNotifier;
    private readonly IUnitOfWork _uow;

    public TrackingService(
        IRepository<Trip> trips,
        IRepository<TrackingEvent> tracking,
        IRepository<UserProfile> users,
        IRepository<DriverProfile> drivers,
        IRepository<Load> loads,
        ILiveTrackingNotifier liveNotifier,
        IUnitOfWork uow)
    {
        _trips = trips;
        _tracking = tracking;
        _users = users;
        _drivers = drivers;
        _loads = loads;
        _liveNotifier = liveNotifier;
        _uow = uow;
    }

    public async Task<Result> RecordLocationAsync(Guid authUserId, Guid tripId, RecordLocationRequest request, CancellationToken cancellationToken = default)
    {
        UserProfile? profile = (await _users.ListAsync(u => u.AuthUserId == authUserId, cancellationToken)).FirstOrDefault();
        DriverProfile? driver = profile is null
            ? null
            : (await _drivers.ListAsync(d => d.UserProfileId == profile.Id, cancellationToken)).FirstOrDefault();
        if (driver is null)
        {
            return Result.Failure(Error.Validation("Only a driver can send location updates."));
        }

        Trip? trip = await _trips.GetByIdAsync(tripId, cancellationToken);
        if (trip is null)
        {
            return Result.Failure(Error.NotFound("Trip not found."));
        }

        // Security: only the assigned driver may upload tracking for a trip.
        if (trip.DriverProfileId != driver.Id)
        {
            return Result.Failure(Error.Unauthorized("You are not the assigned driver on this trip."));
        }

        if (!trip.IsActive)
        {
            return Result.Failure(Error.Conflict("Location can only be recorded while the trip is active."));
        }

        // Coordinate validity is enforced by the value object (throws -> 400).
        LocationPoint point = LocationPoint.Create(
            GeoCoordinate.Create(request.Latitude, request.Longitude),
            request.SpeedKph, request.HeadingDegrees, request.AccuracyMetres);

        TrackingEvent evt = TrackingEvent.Capture(
            tripId, TrackingEventType.LocationPing, point, request.CapturedAtUtc, DateTimeOffset.UtcNow,
            request.BatteryLevel, TrackingSource.Device);

        await _tracking.AddAsync(evt, cancellationToken);

        // Keep the driver's last-known location current so the load owner sees live distance/ETA
        // to pickup and matching honours the pickup radius (Part 7 / ADR-0019).
        driver.RecordLocation(request.Latitude, request.Longitude, request.CapturedAtUtc);
        _drivers.Update(driver);

        await _uow.SaveChangesAsync(cancellationToken);

        // Push the new position to everyone watching this trip so the map marker moves in real time
        // instead of by polling (Part 6). Ingestion only happens while the trip is active, so this
        // naturally pauses on completion.
        double distanceRemaining = TrackingAnalytics.HaversineKm(
            request.Latitude, request.Longitude,
            trip.Destination.Coordinate.Latitude, trip.Destination.Coordinate.Longitude);
        IReadOnlyList<TrackingEvent> points = await OrderedPointsAsync(tripId, cancellationToken);
        double recentSpeed = TrackingAnalytics.RecentSpeedKph(points);
        int? eta = recentSpeed > 0 ? (int)Math.Round(distanceRemaining / recentSpeed * 60) : null;
        await _liveNotifier.PositionRecordedAsync(
            new TripLivePush(
                tripId, request.Latitude, request.Longitude, request.CapturedAtUtc,
                request.SpeedKph, request.HeadingDegrees, Math.Round(distanceRemaining, 1), eta),
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<TrackingPointView>>> GetHistoryAsync(Guid authUserId, Guid tripId, bool privileged, CancellationToken cancellationToken = default)
    {
        Result<Trip> access = await AuthorizeReadAsync(authUserId, tripId, privileged, cancellationToken);
        if (access.IsFailure)
        {
            return access.Error;
        }

        IReadOnlyList<TrackingEvent> points = await OrderedPointsAsync(tripId, cancellationToken);
        return Result<IReadOnlyList<TrackingPointView>>.Success(points.Select(MapPoint).ToList());
    }

    public async Task<Result<TripLiveView>> GetLiveAsync(Guid authUserId, Guid tripId, bool privileged, CancellationToken cancellationToken = default)
    {
        Result<Trip> access = await AuthorizeReadAsync(authUserId, tripId, privileged, cancellationToken);
        if (access.IsFailure)
        {
            return access.Error;
        }

        Trip trip = access.Value;
        IReadOnlyList<TrackingEvent> points = await OrderedPointsAsync(tripId, cancellationToken);
        if (points.Count == 0)
        {
            return new TripLiveView(trip.Id, trip.Status, false, null, null, null, null, null);
        }

        TrackingEvent latest = points[^1];
        double distanceRemaining = TrackingAnalytics.HaversineKm(
            latest.Point.Coordinate.Latitude, latest.Point.Coordinate.Longitude,
            trip.Destination.Coordinate.Latitude, trip.Destination.Coordinate.Longitude);

        // ETA from the driver's recent speed (Part 6) — not the whole-trip average, which collapsed
        // to null whenever the trip was momentarily idle.
        double recentSpeed = TrackingAnalytics.RecentSpeedKph(points);
        int? eta = recentSpeed > 0 ? (int)Math.Round(distanceRemaining / recentSpeed * 60) : null;

        return new TripLiveView(
            trip.Id, trip.Status, true,
            latest.Point.Coordinate.Latitude, latest.Point.Coordinate.Longitude, latest.CapturedAtUtc,
            Math.Round(distanceRemaining, 1), eta);
    }

    public async Task<Result<TripTrackingSummary>> GetSummaryAsync(Guid authUserId, Guid tripId, bool privileged, CancellationToken cancellationToken = default)
    {
        Result<Trip> access = await AuthorizeReadAsync(authUserId, tripId, privileged, cancellationToken);
        if (access.IsFailure)
        {
            return access.Error;
        }

        IReadOnlyList<TrackingEvent> points = await OrderedPointsAsync(tripId, cancellationToken);
        return TrackingAnalytics.Summarize(points);
    }

    private async Task<IReadOnlyList<TrackingEvent>> OrderedPointsAsync(Guid tripId, CancellationToken cancellationToken)
    {
        IReadOnlyList<TrackingEvent> events = await _tracking.ListAsync(e => e.TripId == tripId, cancellationToken);
        return events.OrderBy(e => e.CapturedAtUtc).ToList();
    }

    private async Task<Result<Trip>> AuthorizeReadAsync(Guid authUserId, Guid tripId, bool privileged, CancellationToken cancellationToken)
    {
        Trip? trip = await _trips.GetByIdAsync(tripId, cancellationToken);
        if (trip is null)
        {
            return Error.NotFound("Trip not found.");
        }

        if (privileged)
        {
            return trip;
        }

        UserProfile? profile = (await _users.ListAsync(u => u.AuthUserId == authUserId, cancellationToken)).FirstOrDefault();
        if (profile is not null)
        {
            // The assigned driver may read.
            DriverProfile? driver = (await _drivers.ListAsync(d => d.UserProfileId == profile.Id, cancellationToken)).FirstOrDefault();
            if (driver is not null && trip.DriverProfileId == driver.Id)
            {
                return trip;
            }

            // The load owner may read their shipment's trip.
            if (trip.LoadId is Guid loadId)
            {
                Load? load = await _loads.GetByIdAsync(loadId, cancellationToken);
                if (load is not null && load.ShipperId == profile.Id)
                {
                    return trip;
                }
            }
        }

        return Error.Unauthorized("You cannot view this trip's tracking.");
    }

    private static TrackingPointView MapPoint(TrackingEvent e) => new(
        e.Id, e.Point.Coordinate.Latitude, e.Point.Coordinate.Longitude, e.CapturedAtUtc,
        e.Point.SpeedKph, e.Point.HeadingDegrees, e.Point.AccuracyMetres);
}
