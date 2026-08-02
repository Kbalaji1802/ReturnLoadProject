using ReturnLoad.Application.Abstractions.Persistence;
using ReturnLoad.Application.UseCases.Notifications;
using ReturnLoad.Domain.Fleet;
using ReturnLoad.Domain.Identity;
using ReturnLoad.Domain.Loads;
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
    DateTimeOffset? StartedAtUtc, DateTimeOffset? CompletedAtUtc,
    double OriginLat, double OriginLng, double DestinationLat, double DestinationLng);

/// <summary>
/// An enriched trip for the admin trip-details page (Part 11): the trip fields plus resolved driver
/// name, vehicle registration, and load addresses, and the confirmation-gate flags. Live location /
/// ETA / tracking history / reviews are fetched via their own endpoints.
/// </summary>
public sealed record TripDetailView(
    Guid Id, TripStatus Status, DateTimeOffset? StartedAtUtc, DateTimeOffset? CompletedAtUtc,
    DateTimeOffset StatusChangedAtUtc, bool PickupAutoConfirmed, bool DeliveryAutoConfirmed,
    Guid DriverProfileId, string? DriverName, Guid VehicleId, string? VehicleRegistration,
    Guid? LoadId, string? OriginAddress, string? DestinationAddress,
    double OriginLat, double OriginLng, double DestinationLat, double DestinationLng);

public interface ITripService
{
    Task<Result<Guid>> CreateAsync(CreateTripRequest request, CancellationToken cancellationToken = default);

    Task<Result<TripView>> GetAsync(Guid tripId, CancellationToken cancellationToken = default);

    /// <summary>The authenticated driver's trips — current + history (My Trips, M4.3 Step 7).</summary>
    Task<Result<IReadOnlyList<TripView>>> ListMineAsync(Guid authUserId, CancellationToken cancellationToken = default);

    /// <summary>All trips (ops/admin console).</summary>
    Task<Result<IReadOnlyList<TripView>>> ListAllAsync(CancellationToken cancellationToken = default);

    /// <summary>The trip fulfilling a load — for the load owner to track it (M6). Staff or owner.</summary>
    Task<Result<TripView>> GetForLoadAsync(Guid authUserId, Guid loadId, bool privileged, CancellationToken cancellationToken = default);

    /// <summary>An enriched trip for the admin trip-details page (Part 11). Staff-only.</summary>
    Task<Result<TripDetailView>> GetDetailAsync(Guid tripId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Advances a trip one legal step (Part 5). The caller is authorised against the trip: only the
    /// assigned driver may advance driving steps and only the load owner may confirm the pickup/
    /// delivery gates (the driver may self-advance a gate after the configured wait); staff may
    /// override. A non-participant is rejected.
    /// </summary>
    Task<Result> AdvanceAsync(Guid authUserId, Guid tripId, TripStatus target, bool privileged, CancellationToken cancellationToken = default);
}

internal sealed class TripService : ITripService
{
    private readonly IRepository<Trip> _trips;
    private readonly IRepository<UserProfile> _users;
    private readonly IRepository<DriverProfile> _drivers;
    private readonly IRepository<Vehicle> _vehicles;
    private readonly IRepository<Load> _loads;
    private readonly INotificationService _notify;
    private readonly TripOptions _options;
    private readonly IUnitOfWork _uow;

    public TripService(
        IRepository<Trip> trips,
        IRepository<UserProfile> users,
        IRepository<DriverProfile> drivers,
        IRepository<Vehicle> vehicles,
        IRepository<Load> loads,
        INotificationService notify,
        Microsoft.Extensions.Options.IOptions<TripOptions> options,
        IUnitOfWork uow)
    {
        _trips = trips;
        _users = users;
        _drivers = drivers;
        _vehicles = vehicles;
        _loads = loads;
        _notify = notify;
        _options = options.Value;
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

    public async Task<Result<TripView>> GetForLoadAsync(Guid authUserId, Guid loadId, bool privileged, CancellationToken cancellationToken = default)
    {
        Trip? trip = (await _trips.ListAsync(t => t.LoadId == loadId, cancellationToken)).FirstOrDefault();
        if (trip is null)
        {
            return Error.NotFound("No trip for this load yet.");
        }

        if (!privileged)
        {
            UserProfile? profile = (await _users.ListAsync(u => u.AuthUserId == authUserId, cancellationToken)).FirstOrDefault();
            Load? load = await _loads.GetByIdAsync(loadId, cancellationToken);
            if (profile is null || load is null || load.ShipperId != profile.Id)
            {
                return Error.Unauthorized("You can only track your own loads.");
            }
        }

        return MapView(trip);
    }

    public async Task<Result<TripDetailView>> GetDetailAsync(Guid tripId, CancellationToken cancellationToken = default)
    {
        Trip? trip = await _trips.GetByIdAsync(tripId, cancellationToken);
        if (trip is null)
        {
            return Error.NotFound("Trip not found.");
        }

        DriverProfile? driver = await _drivers.GetByIdAsync(trip.DriverProfileId, cancellationToken);
        string? driverName = driver is null ? null : (await _users.GetByIdAsync(driver.UserProfileId, cancellationToken))?.FullName;
        string? vehicleReg = (await _vehicles.GetByIdAsync(trip.VehicleId, cancellationToken))?.Registration.Value;
        Load? load = trip.LoadId is Guid lid ? await _loads.GetByIdAsync(lid, cancellationToken) : null;

        return new TripDetailView(
            trip.Id, trip.Status, trip.StartedAtUtc, trip.CompletedAtUtc,
            trip.StatusChangedAtUtc, trip.PickupAutoConfirmed, trip.DeliveryAutoConfirmed,
            trip.DriverProfileId, driverName, trip.VehicleId, vehicleReg,
            trip.LoadId, load?.Origin.Address, load?.Destination.Address,
            trip.Origin.Coordinate.Latitude, trip.Origin.Coordinate.Longitude,
            trip.Destination.Coordinate.Latitude, trip.Destination.Coordinate.Longitude);
    }

    private static TripView MapView(Trip trip) =>
        new(trip.Id, trip.CarrierId, trip.VehicleId, trip.DriverProfileId, trip.Status, trip.StartedAtUtc, trip.CompletedAtUtc,
            trip.Origin.Coordinate.Latitude, trip.Origin.Coordinate.Longitude,
            trip.Destination.Coordinate.Latitude, trip.Destination.Coordinate.Longitude);

    /// <summary>
    /// Advances the trip one legal step toward <paramref name="target"/> (or cancels it), authorising
    /// the caller against the trip (Part 5). An illegal transition or a participant violation throws
    /// <c>DomainException</c> / returns a failure, which the API maps to 400/403 (ADR-0016).
    /// </summary>
    public async Task<Result> AdvanceAsync(Guid authUserId, Guid tripId, TripStatus target, bool privileged, CancellationToken cancellationToken = default)
    {
        Trip? trip = await _trips.GetByIdAsync(tripId, cancellationToken);
        if (trip is null)
        {
            return Result.Failure(Error.NotFound("Trip not found."));
        }

        Load? load = trip.LoadId is Guid lid ? await _loads.GetByIdAsync(lid, cancellationToken) : null;

        Result<TripActor> actorResult = await ResolveActorAsync(authUserId, trip, load, privileged, cancellationToken);
        if (actorResult.IsFailure)
        {
            return Result.Failure(actorResult.Error);
        }

        trip.Advance(target, actorResult.Value, DateTimeOffset.UtcNow, _options.OwnerConfirmWindow);
        _trips.Update(trip);

        // Release the driver back to Available once the trip ends, so they can be matched again
        // (Part 3 — the counterpart to MarkBusy on booking acceptance).
        if (target is TripStatus.Completed or TripStatus.Cancelled)
        {
            DriverProfile? driver = await _drivers.GetByIdAsync(trip.DriverProfileId, cancellationToken);
            if (driver is not null)
            {
                driver.ReleaseFromTrip();
                _drivers.Update(driver);
            }
        }

        // Keep the load's lifecycle in step with the trip's. Without this a load stops at Booked
        // the moment it is accepted and never advances, so the owner's dashboard reports a
        // finished delivery as still running and "Delivered" is permanently zero.
        if (load is not null)
        {
            AdvanceLoad(load, target);
        }

        // Notify the load owner at the milestones they care about — including the two confirmation
        // gates they must action (Part 5), so the truck is not left waiting silently.
        if (load is not null)
        {
            (string? subject, string? message) = target switch
            {
                TripStatus.DriverEnRoute => ("Trip started", "Your driver has started the trip."),
                TripStatus.ArrivedPickup => ("Confirm pickup", "Your driver has arrived at pickup. Please confirm to authorise loading."),
                TripStatus.Unloaded => ("Confirm delivery", "Your load has been unloaded at the destination. Please confirm delivery."),
                TripStatus.Completed => ("Trip completed", "Your load has been delivered — the trip is complete."),
                _ => (null, null),
            };
            if (subject is not null)
            {
                await _notify.NotifyUserAsync(load.ShipperId, subject, message!, cancellationToken);
            }
        }

        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>
    /// Mirrors a trip milestone onto the load it fulfils: goods on board means the load is in
    /// transit, a completed trip means it is delivered.
    /// <para>
    /// Each step is guarded by the load's current status rather than assumed from the trip's,
    /// because the two can legitimately diverge — staff can advance a trip past a gate, and
    /// <c>Deliver()</c> requires InTransit, so a trip jumping straight to Completed would
    /// otherwise throw out of a transition that is only a projection. A load already Delivered
    /// or Cancelled is left alone.
    /// </para>
    /// </summary>
    private void AdvanceLoad(Load load, TripStatus target)
    {
        if (load.Status is LoadStatus.Delivered or LoadStatus.Cancelled)
        {
            return;
        }

        // The goods are aboard from Loaded onwards; Completed also implies transit happened.
        bool inTransit = target is TripStatus.Loaded or TripStatus.InTransit
            or TripStatus.ArrivedDestination or TripStatus.Unloaded
            or TripStatus.DeliveryConfirmed or TripStatus.Completed;

        if (inTransit && load.Status is LoadStatus.Booked)
        {
            load.StartTransit();
        }

        if (target is TripStatus.Completed && load.Status is LoadStatus.InTransit)
        {
            load.Deliver();
        }

        _loads.Update(load);
    }

    /// <summary>
    /// Resolves how the caller relates to the trip (Part 5): the assigned driver, the load owner,
    /// privileged staff, or — if none — a non-participant who may not touch the trip.
    /// </summary>
    private async Task<Result<TripActor>> ResolveActorAsync(
        Guid authUserId, Trip trip, Load? load, bool privileged, CancellationToken cancellationToken)
    {
        if (privileged)
        {
            return TripActor.Staff;
        }

        UserProfile? profile = (await _users.ListAsync(u => u.AuthUserId == authUserId, cancellationToken)).FirstOrDefault();
        if (profile is null)
        {
            return Error.Unauthorized("You are not a participant on this trip.");
        }

        DriverProfile? driver = await _drivers.GetByIdAsync(trip.DriverProfileId, cancellationToken);
        if (driver is not null && driver.UserProfileId == profile.Id)
        {
            return TripActor.Driver;
        }

        if (load is not null && load.ShipperId == profile.Id)
        {
            return TripActor.Owner;
        }

        return Error.Unauthorized("You are not a participant on this trip.");
    }
}
