using ReturnLoad.Application.Abstractions.Persistence;
using ReturnLoad.Domain.Identity;
using ReturnLoad.Domain.Tracking;
using ReturnLoad.Domain.Trips;
using ReturnLoad.Domain.ValueObjects;
using ReturnLoad.Shared.Results;

namespace ReturnLoad.Application.UseCases.Trips;

public sealed record CreateTripRequest(
    Guid CarrierId, Guid VehicleId, Guid DriverProfileId,
    double OriginLat, double OriginLng, string? OriginAddress,
    double DestinationLat, double DestinationLng, string? DestinationAddress,
    double ReturnDestinationLat, double ReturnDestinationLng, string? ReturnDestinationAddress,
    DateTimeOffset ReturnAvailableFrom, DateTimeOffset ReturnAvailableTo);

public sealed record TripView(
    Guid Id, Guid CarrierId, Guid VehicleId, Guid DriverProfileId, TripStatus Status,
    DateTimeOffset? StartedAtUtc, DateTimeOffset? CompletedAtUtc);

public sealed record RecordTrackingRequest(
    TrackingEventType Type, double Latitude, double Longitude, double? SpeedKph, DateTimeOffset CapturedAtUtc);

public sealed record TrackingEventView(
    Guid Id, TrackingEventType Type, double Latitude, double Longitude, DateTimeOffset CapturedAtUtc);

public interface ITripService
{
    Task<Result<Guid>> CreateAsync(CreateTripRequest request, CancellationToken cancellationToken = default);

    Task<Result<TripView>> GetAsync(Guid tripId, CancellationToken cancellationToken = default);

    /// <summary>The authenticated driver's trips — current + history (My Trips, M4.3 Step 7).</summary>
    Task<Result<IReadOnlyList<TripView>>> ListMineAsync(Guid authUserId, CancellationToken cancellationToken = default);

    /// <summary>All trips (ops/admin console).</summary>
    Task<Result<IReadOnlyList<TripView>>> ListAllAsync(CancellationToken cancellationToken = default);

    Task<Result> AdvanceAsync(Guid tripId, TripStatus target, CancellationToken cancellationToken = default);

    Task<Result> RecordTrackingAsync(Guid tripId, RecordTrackingRequest request, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<TrackingEventView>>> GetTrackingAsync(Guid tripId, CancellationToken cancellationToken = default);
}

internal sealed class TripService : ITripService
{
    private readonly IRepository<Trip> _trips;
    private readonly IRepository<TrackingEvent> _tracking;
    private readonly IRepository<UserProfile> _users;
    private readonly IRepository<DriverProfile> _drivers;
    private readonly IUnitOfWork _uow;

    public TripService(
        IRepository<Trip> trips,
        IRepository<TrackingEvent> tracking,
        IRepository<UserProfile> users,
        IRepository<DriverProfile> drivers,
        IUnitOfWork uow)
    {
        _trips = trips;
        _tracking = tracking;
        _users = users;
        _drivers = drivers;
        _uow = uow;
    }

    public async Task<Result<Guid>> CreateAsync(CreateTripRequest request, CancellationToken cancellationToken = default)
    {
        Location destination = Location.Create(
            GeoCoordinate.Create(request.DestinationLat, request.DestinationLng), request.DestinationAddress);

        ReturnLeg returnLeg = ReturnLeg.Create(
            destination,
            Location.Create(GeoCoordinate.Create(request.ReturnDestinationLat, request.ReturnDestinationLng), request.ReturnDestinationAddress),
            TimeWindow.Create(request.ReturnAvailableFrom, request.ReturnAvailableTo));

        Trip trip = Trip.Create(
            request.CarrierId, request.VehicleId, request.DriverProfileId,
            Location.Create(GeoCoordinate.Create(request.OriginLat, request.OriginLng), request.OriginAddress),
            destination,
            returnLeg);

        await _trips.AddAsync(trip, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return trip.Id;
    }

    public async Task<Result<TripView>> GetAsync(Guid tripId, CancellationToken cancellationToken = default)
    {
        Trip? trip = await _trips.GetByIdAsync(tripId, cancellationToken);
        return trip is null ? Error.NotFound("Trip not found.") : MapView(trip);
    }

    public async Task<Result<IReadOnlyList<TripView>>> ListMineAsync(Guid authUserId, CancellationToken cancellationToken = default)
    {
        UserProfile? profile = (await _users.ListAsync(u => u.AuthUserId == authUserId, cancellationToken)).FirstOrDefault();
        DriverProfile? driver = profile is null
            ? null
            : (await _drivers.ListAsync(d => d.UserProfileId == profile.Id, cancellationToken)).FirstOrDefault();
        if (driver is null)
        {
            return Error.Validation("Register as a driver first.");
        }

        IReadOnlyList<Trip> trips = await _trips.ListAsync(t => t.DriverProfileId == driver.Id, cancellationToken);
        return Result<IReadOnlyList<TripView>>.Success(trips.Select(MapView).ToList());
    }

    public async Task<Result<IReadOnlyList<TripView>>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Trip> trips = await _trips.ListAsync(_ => true, cancellationToken);
        return Result<IReadOnlyList<TripView>>.Success(trips.Select(MapView).ToList());
    }

    private static TripView MapView(Trip trip) =>
        new(trip.Id, trip.CarrierId, trip.VehicleId, trip.DriverProfileId, trip.Status, trip.StartedAtUtc, trip.CompletedAtUtc);

    /// <summary>
    /// Advances the trip one legal step toward <paramref name="target"/> (or cancels it). An
    /// illegal transition throws <c>DomainException</c>, which the API maps to 400 (ADR-0016).
    /// </summary>
    public async Task<Result> AdvanceAsync(Guid tripId, TripStatus target, CancellationToken cancellationToken = default)
    {
        Trip? trip = await _trips.GetByIdAsync(tripId, cancellationToken);
        if (trip is null)
        {
            return Result.Failure(Error.NotFound("Trip not found."));
        }

        trip.Advance(target);
        _trips.Update(trip);
        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> RecordTrackingAsync(Guid tripId, RecordTrackingRequest request, CancellationToken cancellationToken = default)
    {
        if (!await _trips.ExistsAsync(t => t.Id == tripId, cancellationToken))
        {
            return Result.Failure(Error.NotFound("Trip not found."));
        }

        TrackingEvent evt = TrackingEvent.Capture(
            tripId,
            request.Type,
            LocationPoint.Create(GeoCoordinate.Create(request.Latitude, request.Longitude), request.SpeedKph),
            request.CapturedAtUtc,
            DateTimeOffset.UtcNow);

        await _tracking.AddAsync(evt, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<TrackingEventView>>> GetTrackingAsync(Guid tripId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<TrackingEvent> events = await _tracking.ListAsync(e => e.TripId == tripId, cancellationToken);
        IReadOnlyList<TrackingEventView> views = events
            .OrderBy(e => e.CapturedAtUtc)
            .Select(e => new TrackingEventView(e.Id, e.Type, e.Point.Coordinate.Latitude, e.Point.Coordinate.Longitude, e.CapturedAtUtc))
            .ToList();
        return Result<IReadOnlyList<TrackingEventView>>.Success(views);
    }
}
