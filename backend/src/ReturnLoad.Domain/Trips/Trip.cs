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
        StatusChangedAtUtc = CreatedAtUtc;
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

    /// <summary>When the trip last changed status — the clock the owner-confirmation timeout runs against (Part 5).</summary>
    public DateTimeOffset StatusChangedAtUtc { get; private set; }

    /// <summary>True if pickup was advanced by the driver after the owner-confirm window elapsed, not by the owner.</summary>
    public bool PickupAutoConfirmed { get; private set; }

    /// <summary>True if delivery was advanced by the driver after the owner-confirm window elapsed, not by the owner.</summary>
    public bool DeliveryAutoConfirmed { get; private set; }

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

    /// <summary>The ordered forward lifecycle (Part 5). Cancelled is a branch, not in it.</summary>
    private static readonly TripStatus[] Lifecycle =
    [
        TripStatus.Created, TripStatus.DriverAccepted, TripStatus.DriverEnRoute, TripStatus.ArrivedPickup,
        TripStatus.PickupConfirmed, TripStatus.Loaded, TripStatus.InTransit, TripStatus.ArrivedDestination,
        TripStatus.Unloaded, TripStatus.DeliveryConfirmed, TripStatus.Completed,
    ];

    /// <summary>The two gates the load owner confirms (Part 5).</summary>
    private static bool IsOwnerConfirmationStep(TripStatus status) =>
        status is TripStatus.PickupConfirmed or TripStatus.DeliveryConfirmed;

    /// <summary>
    /// Advances the trip exactly one legal step toward <paramref name="target"/>, or cancels it, on
    /// behalf of <paramref name="actor"/> (Part 5 participant authorization). Only the immediate next
    /// state (or Cancelled, before completion) is permitted — no skipping.
    /// <list type="bullet">
    /// <item>Driving/physical steps may be advanced only by the <see cref="TripActor.Driver"/> (or Staff).</item>
    /// <item>Owner-confirmation gates are advanced by the <see cref="TripActor.Owner"/> (or Staff). The
    /// driver may self-advance a gate only once <paramref name="ownerConfirmWindow"/> has elapsed since
    /// the trip entered the preceding state — recorded as <b>auto-confirmed</b> so a truck is never
    /// stranded waiting on an absent owner.</item>
    /// </list>
    /// Entering <see cref="TripStatus.DriverEnRoute"/> stamps the start; <see cref="TripStatus.Completed"/> stamps completion.
    /// </summary>
    public void Advance(TripStatus target, TripActor actor, DateTimeOffset nowUtc, TimeSpan ownerConfirmWindow)
    {
        if (target == TripStatus.Cancelled)
        {
            Cancel();
            StatusChangedAtUtc = nowUtc;
            return;
        }

        int currentIndex = Array.IndexOf(Lifecycle, Status);
        int targetIndex = Array.IndexOf(Lifecycle, target);
        Guard.Against(currentIndex < 0, "A finished trip cannot advance.", "trip_finished");
        Guard.Against(
            targetIndex != currentIndex + 1,
            $"Illegal trip transition from {Status} to {target}.",
            "trip_illegal_transition");

        if (IsOwnerConfirmationStep(target))
        {
            AuthorizeOwnerConfirmation(target, actor, nowUtc, ownerConfirmWindow);
        }
        else
        {
            // Driving/physical steps are the driver's to advance (staff may override for support).
            Guard.Against(
                actor == TripActor.Owner,
                "The load owner cannot advance the driver's steps.",
                "trip_driver_step");
        }

        Status = target;
        StatusChangedAtUtc = nowUtc;

        if (target == TripStatus.DriverEnRoute)
        {
            StartedAtUtc = nowUtc;
            Raise(new TripStarted(Id, StartedAtUtc.Value));
        }
        else if (target == TripStatus.Completed)
        {
            CompletedAtUtc = nowUtc;
            Raise(new TripCompleted(Id, CompletedAtUtc.Value));
        }
    }

    private void AuthorizeOwnerConfirmation(TripStatus target, TripActor actor, DateTimeOffset nowUtc, TimeSpan ownerConfirmWindow)
    {
        if (actor is TripActor.Owner or TripActor.Staff)
        {
            return; // the owner confirmed (or staff overrode) — the normal path
        }

        // Driver self-advance: allowed only after the owner has had the configured window to confirm.
        bool windowElapsed = nowUtc - StatusChangedAtUtc >= ownerConfirmWindow;
        Guard.Against(
            !windowElapsed,
            "Waiting for the load owner to confirm. You can proceed once the confirmation window elapses.",
            "trip_awaiting_owner_confirmation");

        if (target == TripStatus.PickupConfirmed)
        {
            PickupAutoConfirmed = true;
        }
        else
        {
            DeliveryAutoConfirmed = true;
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
