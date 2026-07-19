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
/// <item>Legal transitions only — one step at a time along the M4.3 lifecycle (Created →
/// DriverAccepted → … → Completed); cancellable before completion. Completion records a timestamp.</item>
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
        ReturnLeg returnLeg,
        Guid? loadId)
        : base(id)
    {
        CarrierId = carrierId;
        VehicleId = vehicleId;
        DriverProfileId = driverProfileId;
        Origin = origin;
        Destination = destination;
        ReturnLeg = returnLeg;
        LoadId = loadId;
        Status = TripStatus.Created;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    private Trip()
    {
    }

    public Guid CarrierId { get; }

    public Guid VehicleId { get; }

    public Guid DriverProfileId { get; }

    /// <summary>The load this trip fulfils, when created from an accepted booking (M6). Null for
    /// manually-created trips. Lets the load owner track their shipment's trip.</summary>
    public Guid? LoadId { get; }

    public Location Origin { get; } = null!;

    public Location Destination { get; } = null!;

    public ReturnLeg ReturnLeg { get; private set; } = null!;

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
        ReturnLeg returnLeg,
        Guid? loadId = null)
    {
        Guard.AgainstDefault(carrierId, "Carrier id", "trip_carrier_required");
        Guard.AgainstDefault(vehicleId, "Vehicle id", "trip_vehicle_required");
        Guard.AgainstDefault(driverProfileId, "Driver id", "trip_driver_required");
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(returnLeg);
        Guard.Against(origin == destination, "Trip origin and destination must differ.", "trip_same_origin_destination");

        Trip trip = new(Guid.NewGuid(), carrierId, vehicleId, driverProfileId, origin, destination, returnLeg, loadId);
        trip.Raise(new TripCreated(trip.Id, vehicleId, driverProfileId, trip.CreatedAtUtc));
        return trip;
    }

    /// <summary>Whether this trip is currently on the road (past acceptance, before completion).</summary>
    public bool IsActive => Status is not (TripStatus.Created or TripStatus.Completed or TripStatus.Cancelled);

    /// <summary>The ordered forward lifecycle (M4.3 Step 5). Cancelled is a branch, not in it.</summary>
    private static readonly TripStatus[] Lifecycle =
    [
        TripStatus.Created, TripStatus.DriverAccepted, TripStatus.DriverEnRoute, TripStatus.ArrivedPickup,
        TripStatus.Loaded, TripStatus.InTransit, TripStatus.ArrivedDestination, TripStatus.Unloaded,
        TripStatus.Completed,
    ];

    /// <summary>
    /// Advances the trip exactly one legal step toward <paramref name="target"/>, or cancels it.
    /// Only the immediate next state (or Cancelled, before completion) is permitted — no skipping.
    /// Entering <see cref="TripStatus.DriverEnRoute"/> stamps the start; reaching
    /// <see cref="TripStatus.Completed"/> stamps completion.
    /// </summary>
    public void Advance(TripStatus target)
    {
        if (target == TripStatus.Cancelled)
        {
            Cancel();
            return;
        }

        int currentIndex = Array.IndexOf(Lifecycle, Status);
        int targetIndex = Array.IndexOf(Lifecycle, target);
        Guard.Against(currentIndex < 0, "A finished trip cannot advance.", "trip_finished");
        Guard.Against(
            targetIndex != currentIndex + 1,
            $"Illegal trip transition from {Status} to {target}.",
            "trip_illegal_transition");

        Status = target;

        if (target == TripStatus.DriverEnRoute)
        {
            StartedAtUtc = DateTimeOffset.UtcNow;
            Raise(new TripStarted(Id, StartedAtUtc.Value));
        }
        else if (target == TripStatus.Completed)
        {
            CompletedAtUtc = DateTimeOffset.UtcNow;
            Raise(new TripCompleted(Id, CompletedAtUtc.Value));
        }
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
