using ReturnLoad.Application.Abstractions.Geo;
using ReturnLoad.Application.Abstractions.Persistence;
using ReturnLoad.Application.UseCases.Notifications;
using ReturnLoad.Application.UseCases.Reviews;
using ReturnLoad.Domain.Bookings;
using ReturnLoad.Domain.Fleet;
using ReturnLoad.Domain.Identity;
using ReturnLoad.Domain.Loads;
using ReturnLoad.Domain.Trips;
using ReturnLoad.Shared.Results;

namespace ReturnLoad.Application.UseCases.Bookings;

/// <summary>
/// A booking request enriched for the load owner's choose-a-driver screen (Part 7): the driver's
/// name, vehicle, rating, completed-trip count, verification status, current availability, and
/// distance/ETA from their last-known location to the pickup — everything needed to pick one driver.
/// </summary>
public sealed record BookingRequestView(
    Guid Id, Guid LoadId, Guid DriverProfileId, Guid VehicleId,
    BookingRequestStatus Status, DateTimeOffset CreatedAtUtc, DateTimeOffset? DecidedAtUtc,
    string? DriverName, string? VehicleRegistration, double? DriverRating = null,
    int CompletedTrips = 0, double? DistanceFromPickupKm = null, int? EtaToPickupMinutes = null,
    DriverStatus? VerificationStatus = null, DriverAvailability? Availability = null);

/// <summary>
/// The booking-request workflow (M4.3 Steps 3–4): a verified driver requests a load with one of
/// their verified vehicles; the load owner accepts one request (a Trip is created and the load
/// becomes assigned; other requests are rejected) or rejects it. Enforces the Trust &amp; Safety
/// pre-trip gate — an unverified driver/vehicle can never request.
/// </summary>
public interface IBookingService
{
    /// <summary>Driver requests to carry a load with a vehicle. Returns the request id.</summary>
    Task<Result<Guid>> RequestAsync(Guid authUserId, Guid loadId, Guid vehicleId, CancellationToken cancellationToken = default);

    /// <summary>The driver's own requests.</summary>
    Task<Result<IReadOnlyList<BookingRequestView>>> ListMineAsync(Guid authUserId, CancellationToken cancellationToken = default);

    /// <summary>The requests on a load the caller owns.</summary>
    Task<Result<IReadOnlyList<BookingRequestView>>> ListForLoadAsync(Guid authUserId, Guid loadId, CancellationToken cancellationToken = default);

    /// <summary>All booking requests (ops/admin console).</summary>
    Task<Result<IReadOnlyList<BookingRequestView>>> ListAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Load owner accepts a request → creates the Trip, assigns the load. Returns the trip id.</summary>
    Task<Result<Guid>> AcceptAsync(Guid authUserId, Guid requestId, CancellationToken cancellationToken = default);

    /// <summary>Load owner rejects a request.</summary>
    Task<Result> RejectAsync(Guid authUserId, Guid requestId, CancellationToken cancellationToken = default);
}

internal sealed class BookingService : IBookingService
{
    private readonly IRepository<BookingRequest> _bookings;
    private readonly IRepository<UserProfile> _users;
    private readonly IRepository<DriverProfile> _drivers;
    private readonly IRepository<Association> _associations;
    private readonly IRepository<Vehicle> _vehicles;
    private readonly IRepository<Load> _loads;
    private readonly IRepository<Trip> _trips;
    private readonly INotificationService _notify;
    private readonly IReviewService _reviews;
    private readonly IRouteService _routes;
    private readonly IUnitOfWork _uow;

    public BookingService(
        IRepository<BookingRequest> bookings,
        IRepository<UserProfile> users,
        IRepository<DriverProfile> drivers,
        IRepository<Association> associations,
        IRepository<Vehicle> vehicles,
        IRepository<Load> loads,
        IRepository<Trip> trips,
        INotificationService notify,
        IReviewService reviews,
        IRouteService routes,
        IUnitOfWork uow)
    {
        _bookings = bookings;
        _users = users;
        _drivers = drivers;
        _associations = associations;
        _vehicles = vehicles;
        _loads = loads;
        _trips = trips;
        _notify = notify;
        _reviews = reviews;
        _routes = routes;
        _uow = uow;
    }

    public async Task<Result<Guid>> RequestAsync(Guid authUserId, Guid loadId, Guid vehicleId, CancellationToken cancellationToken = default)
    {
        UserProfile? profile = (await _users.ListAsync(u => u.AuthUserId == authUserId, cancellationToken)).FirstOrDefault();
        DriverProfile? driver = profile is null
            ? null
            : (await _drivers.ListAsync(d => d.UserProfileId == profile.Id, cancellationToken)).FirstOrDefault();
        if (profile is null || driver is null)
        {
            return Error.Validation("Register as a driver before requesting loads.");
        }

        // Trust & Safety pre-trip gate: driver must be verified.
        if (driver.Status != DriverStatus.Active)
        {
            return Error.Validation("Your account must be verified before you can request loads.");
        }

        Load? load = await _loads.GetByIdAsync(loadId, cancellationToken);
        if (load is null)
        {
            return Error.NotFound("Load not found.");
        }

        if (load.Status != LoadStatus.Posted)
        {
            return Error.Conflict("This load is no longer open for requests.");
        }

        Vehicle? vehicle = await _vehicles.GetByIdAsync(vehicleId, cancellationToken);
        if (vehicle is null)
        {
            return Error.NotFound("Vehicle not found.");
        }

        // Vehicle must be verified (filter 8) and in the driver's fleet.
        if (vehicle.Status != VehicleStatus.Active)
        {
            return Error.Validation("Your vehicle must be verified before you can request loads.");
        }

        HashSet<Guid> carrierIds = [.. (await _associations.ListAsync(
            a => a.MemberUserProfileId == profile.Id && a.Role == AssociationRole.Driver && a.Status != AssociationStatus.Revoked,
            cancellationToken)).Select(a => a.CarrierId)];
        if (!carrierIds.Contains(vehicle.CarrierId))
        {
            return Error.Validation("That vehicle is not in your fleet.");
        }

        // One active trip at a time (Step 3 rule).
        bool hasActiveTrip = await _trips.ExistsAsync(
            t => t.DriverProfileId == driver.Id && t.Status != TripStatus.Completed && t.Status != TripStatus.Cancelled,
            cancellationToken);
        if (hasActiveTrip)
        {
            return Error.Conflict("You already have an active trip. Finish it before requesting another load.");
        }

        bool alreadyRequested = await _bookings.ExistsAsync(
            b => b.LoadId == loadId && b.DriverProfileId == driver.Id && b.Status == BookingRequestStatus.Pending,
            cancellationToken);
        if (alreadyRequested)
        {
            return Error.Conflict("You have already requested this load.");
        }

        BookingRequest request = BookingRequest.Create(loadId, driver.Id, vehicle.CarrierId, vehicleId);
        await _bookings.AddAsync(request, cancellationToken);
        await _notify.NotifyUserAsync(load.ShipperId, "New load request", "A verified driver requested one of your loads.", cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return request.Id;
    }

    public async Task<Result<Guid>> AcceptAsync(Guid authUserId, Guid requestId, CancellationToken cancellationToken = default)
    {
        UserProfile? owner = (await _users.ListAsync(u => u.AuthUserId == authUserId, cancellationToken)).FirstOrDefault();
        if (owner is null)
        {
            return Error.Validation("Complete your profile first.");
        }

        BookingRequest? request = await _bookings.GetByIdAsync(requestId, cancellationToken);
        if (request is null)
        {
            return Error.NotFound("Booking request not found.");
        }

        Load? load = await _loads.GetByIdAsync(request.LoadId, cancellationToken);
        if (load is null)
        {
            return Error.NotFound("Load not found.");
        }

        if (load.ShipperId != owner.Id)
        {
            return Error.Unauthorized("You can only decide requests on your own loads.");
        }

        if (request.Status != BookingRequestStatus.Pending)
        {
            return Error.Conflict("This request has already been decided.");
        }

        if (load.Status != LoadStatus.Posted)
        {
            return Error.Conflict("This load has already been assigned.");
        }

        request.Accept();
        _bookings.Update(request);

        // The loaded journey pickup → drop; a placeholder return leg (drop → pickup) satisfies the
        // Trip's return-leg invariant until real return-leg posting lands.
        ReturnLeg returnLeg = ReturnLeg.Create(load.Destination, load.Origin, load.PickupWindow);
        Trip trip = Trip.Create(request.CarrierId, request.VehicleId, request.DriverProfileId, load.Origin, load.Destination, returnLeg, load.Id);
        await _trips.AddAsync(trip, cancellationToken);

        // The assigned driver is now busy — they must not receive new loads while on this trip
        // (Part 3). Released back to Available when the trip completes/cancels (TripService).
        DriverProfile? assignedDriver = await _drivers.GetByIdAsync(request.DriverProfileId, cancellationToken);
        if (assignedDriver is not null)
        {
            assignedDriver.MarkBusy();
            _drivers.Update(assignedDriver);
        }

        // The load is now assigned to this driver.
        load.MarkMatched();
        load.Book();
        _loads.Update(load);

        // Every other pending request for this load is superseded.
        IReadOnlyList<BookingRequest> others = await _bookings.ListAsync(
            b => b.LoadId == load.Id && b.Status == BookingRequestStatus.Pending && b.Id != request.Id, cancellationToken);
        foreach (BookingRequest other in others)
        {
            other.Reject();
            _bookings.Update(other);
            await _notify.NotifyDriverAsync(other.DriverProfileId, "Request not selected", "The load owner chose another driver for this load.", cancellationToken);
        }

        await _notify.NotifyDriverAsync(request.DriverProfileId, "Request approved", "Your load request was approved — a trip has been created.", cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return trip.Id;
    }

    public async Task<Result> RejectAsync(Guid authUserId, Guid requestId, CancellationToken cancellationToken = default)
    {
        UserProfile? owner = (await _users.ListAsync(u => u.AuthUserId == authUserId, cancellationToken)).FirstOrDefault();
        if (owner is null)
        {
            return Result.Failure(Error.Validation("Complete your profile first."));
        }

        BookingRequest? request = await _bookings.GetByIdAsync(requestId, cancellationToken);
        if (request is null)
        {
            return Result.Failure(Error.NotFound("Booking request not found."));
        }

        Load? load = await _loads.GetByIdAsync(request.LoadId, cancellationToken);
        if (load is null || load.ShipperId != owner.Id)
        {
            return Result.Failure(Error.Unauthorized("You can only decide requests on your own loads."));
        }

        if (request.Status != BookingRequestStatus.Pending)
        {
            return Result.Failure(Error.Conflict("This request has already been decided."));
        }

        request.Reject();
        _bookings.Update(request);
        await _notify.NotifyDriverAsync(request.DriverProfileId, "Request rejected", "Your load request was not accepted.", cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<BookingRequestView>>> ListMineAsync(Guid authUserId, CancellationToken cancellationToken = default)
    {
        UserProfile? profile = (await _users.ListAsync(u => u.AuthUserId == authUserId, cancellationToken)).FirstOrDefault();
        DriverProfile? driver = profile is null
            ? null
            : (await _drivers.ListAsync(d => d.UserProfileId == profile.Id, cancellationToken)).FirstOrDefault();
        if (driver is null)
        {
            return Error.Validation("Register as a driver first.");
        }

        IReadOnlyList<BookingRequest> list = await _bookings.ListAsync(b => b.DriverProfileId == driver.Id, cancellationToken);
        return Result<IReadOnlyList<BookingRequestView>>.Success(list.Select(Map).ToList());
    }

    public async Task<Result<IReadOnlyList<BookingRequestView>>> ListForLoadAsync(Guid authUserId, Guid loadId, CancellationToken cancellationToken = default)
    {
        UserProfile? owner = (await _users.ListAsync(u => u.AuthUserId == authUserId, cancellationToken)).FirstOrDefault();
        if (owner is null)
        {
            return Error.Validation("Complete your profile first.");
        }

        Load? load = await _loads.GetByIdAsync(loadId, cancellationToken);
        if (load is null)
        {
            return Error.NotFound("Load not found.");
        }

        if (load.ShipperId != owner.Id)
        {
            return Error.Unauthorized("You can only view requests on your own loads.");
        }

        IReadOnlyList<BookingRequest> list = await _bookings.ListAsync(b => b.LoadId == loadId, cancellationToken);

        // Enrich each candidate with everything the owner needs to choose one driver (Part 7):
        // name, vehicle, rating, completed trips, verification, availability, and distance/ETA from
        // the driver's last-known location to this load's pickup. N+1 is fine at the handful-of-
        // requests-per-load scale.
        List<BookingRequestView> views = [];
        foreach (BookingRequest b in list)
        {
            DriverProfile? driver = await _drivers.GetByIdAsync(b.DriverProfileId, cancellationToken);
            string? driverName = null;
            double? driverRating = null;
            int completedTrips = 0;
            double? distanceKm = null;
            int? etaMinutes = null;
            DriverStatus? verification = null;
            DriverAvailability? availability = null;

            if (driver is not null)
            {
                driverName = (await _users.GetByIdAsync(driver.UserProfileId, cancellationToken))?.FullName;
                RatingSummary summary = await _reviews.GetSummaryAsync(driver.UserProfileId, cancellationToken);
                driverRating = summary.Count > 0 ? summary.Average : null;
                verification = driver.Status;
                availability = driver.Availability;

                completedTrips = (await _trips.ListAsync(
                    t => t.DriverProfileId == driver.Id && t.Status == TripStatus.Completed, cancellationToken)).Count;

                if (driver.LastKnownLatitude is double dLat && driver.LastKnownLongitude is double dLng)
                {
                    RouteResult? route = await _routes.GetRouteAsync(
                        dLat, dLng, load.Origin.Coordinate.Latitude, load.Origin.Coordinate.Longitude, cancellationToken);
                    if (route is not null)
                    {
                        distanceKm = (double)route.DistanceKm;
                        etaMinutes = (int)Math.Round(route.EstimatedDuration.TotalMinutes);
                    }
                }
            }

            Vehicle? vehicle = await _vehicles.GetByIdAsync(b.VehicleId, cancellationToken);
            views.Add(Map(b) with
            {
                DriverName = driverName,
                VehicleRegistration = vehicle?.Registration.Value,
                DriverRating = driverRating,
                CompletedTrips = completedTrips,
                DistanceFromPickupKm = distanceKm,
                EtaToPickupMinutes = etaMinutes,
                VerificationStatus = verification,
                Availability = availability,
            });
        }

        // Show pending requests first, best-rated drivers on top (a preferred-partner signal),
        // then the nearest to the pickup — the owner's most likely pick.
        List<BookingRequestView> ordered = views
            .OrderBy(v => v.Status == BookingRequestStatus.Pending ? 0 : 1)
            .ThenByDescending(v => v.DriverRating ?? 0)
            .ThenBy(v => v.DistanceFromPickupKm ?? double.MaxValue)
            .ThenBy(v => v.CreatedAtUtc)
            .ToList();

        return Result<IReadOnlyList<BookingRequestView>>.Success(ordered);
    }

    public async Task<Result<IReadOnlyList<BookingRequestView>>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<BookingRequest> list = await _bookings.ListAsync(_ => true, cancellationToken);
        return Result<IReadOnlyList<BookingRequestView>>.Success(list.Select(Map).ToList());
    }

    private static BookingRequestView Map(BookingRequest b) =>
        new(b.Id, b.LoadId, b.DriverProfileId, b.VehicleId, b.Status, b.CreatedAtUtc, b.DecidedAtUtc, null, null);
}
