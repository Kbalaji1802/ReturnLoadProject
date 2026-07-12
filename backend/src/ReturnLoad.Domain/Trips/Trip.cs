using ReturnLoad.Domain.Common;
using ReturnLoad.Domain.ValueObjects;

namespace ReturnLoad.Domain.Trips;

/// <summary>
/// A truck's planned movement, including the <see cref="ReturnLeg"/> we want to fill
/// (glossary §8; domain map §12). Owns the trip lifecycle and timestamps.
/// <para><b>Domain rules / invariants:</b></para>
/// <list type="bullet">
/// <item>Carrier, vehicle, driver, origin, destination, and a return leg are required.</item>
/// <item>Origin and destination must differ.</item>
/// <item>Legal transitions only: Created → Assigned → Started → InTransit → Completed;
/// cancellable before completion. Completion records a timestamp.</item>
/// </list>
/// </summary>
public sealed class Trip : AggregateRoot<Guid>
{
    private Trip(
        Guid id,
        Guid carrierId,
        Guid vehicleId,
        Guid driverProfileId,
        Location origin,
        Location destination,
        ReturnLeg returnLeg)
        : base(id)
    {
        CarrierId = carrierId;
        VehicleId = vehicleId;
        DriverProfileId = driverProfileId;
        Origin = origin;
        Destination = destination;
        ReturnLeg = returnLeg;
        Status = TripStatus.Created;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid CarrierId { get; }

    public Guid VehicleId { get; }

    public Guid DriverProfileId { get; }

    public Location Origin { get; }

    public Location Destination { get; }

    public ReturnLeg ReturnLeg { get; private set; }

    public TripStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset? StartedAtUtc { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public static Trip Create(
        Guid carrierId,
        Guid vehicleId,
        Guid driverProfileId,
        Location origin,
        Location destination,
        ReturnLeg returnLeg)
    {
        Guard.AgainstDefault(carrierId, "Carrier id", "trip_carrier_required");
        Guard.AgainstDefault(vehicleId, "Vehicle id", "trip_vehicle_required");
        Guard.AgainstDefault(driverProfileId, "Driver id", "trip_driver_required");
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(returnLeg);
        Guard.Against(origin == destination, "Trip origin and destination must differ.", "trip_same_origin_destination");

        Trip trip = new(Guid.NewGuid(), carrierId, vehicleId, driverProfileId, origin, destination, returnLeg);
        trip.Raise(new TripCreated(trip.Id, vehicleId, driverProfileId, trip.CreatedAtUtc));
        return trip;
    }

    public void Assign()
    {
        Guard.Against(Status != TripStatus.Created, "Only a created trip can be assigned.", "trip_not_created");
        Status = TripStatus.Assigned;
    }

    public void Start()
    {
        Guard.Against(Status != TripStatus.Assigned, "Only an assigned trip can start.", "trip_not_assigned");
        Status = TripStatus.Started;
        StartedAtUtc = DateTimeOffset.UtcNow;
        Raise(new TripStarted(Id, StartedAtUtc.Value));
    }

    public void MarkInTransit()
    {
        Guard.Against(Status != TripStatus.Started, "Only a started trip can move to in-transit.", "trip_not_started");
        Status = TripStatus.InTransit;
    }

    public void Complete()
    {
        Guard.Against(
            Status is not (TripStatus.Started or TripStatus.InTransit),
            "Only a started or in-transit trip can complete.",
            "trip_not_completable");
        Status = TripStatus.Completed;
        CompletedAtUtc = DateTimeOffset.UtcNow;
        Raise(new TripCompleted(Id, CompletedAtUtc.Value));
    }

    public void Cancel()
    {
        Guard.Against(
            Status is TripStatus.Completed or TripStatus.Cancelled,
            "A completed or cancelled trip cannot be cancelled.",
            "trip_not_cancellable");
        Status = TripStatus.Cancelled;
    }
}
