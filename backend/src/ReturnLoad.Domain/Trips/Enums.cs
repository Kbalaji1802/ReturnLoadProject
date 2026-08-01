namespace ReturnLoad.Domain.Trips;

/// <summary>
/// Lifecycle of a trip — the loaded journey a driver runs after a booking is accepted
/// (M4.3 Step 5; owner-confirmation gates added in the correction sprint, Part 5). Ordered:
/// Created → DriverAccepted → DriverEnRoute → ArrivedPickup → <b>PickupConfirmed</b> → Loaded →
/// InTransit → ArrivedDestination → Unloaded → <b>DeliveryConfirmed</b> → Completed, with a
/// Cancelled branch before completion. The two confirmation steps are advanced by the load owner
/// (or by the driver after a configurable wait — see <c>Trip.Advance</c>). New values are appended
/// (10, 11) so existing numeric serialisation stays stable; order is defined by the lifecycle array.
/// </summary>
public enum TripStatus
{
    Created = 0,
    DriverAccepted = 1,
    DriverEnRoute = 2,
    ArrivedPickup = 3,
    Loaded = 4,
    InTransit = 5,
    ArrivedDestination = 6,
    Unloaded = 7,
    Completed = 8,
    Cancelled = 9,

    /// <summary>The load owner confirmed the driver at pickup (gate before Loaded, Part 5).</summary>
    PickupConfirmed = 10,

    /// <summary>The load owner confirmed delivery (gate before Completed, Part 5).</summary>
    DeliveryConfirmed = 11,
}

/// <summary>
/// Who is advancing a trip's status (Part 5 participant authorization). The driver runs the driving
/// steps; the load owner runs the confirmation gates; staff may override.
/// </summary>
public enum TripActor
{
    Driver = 0,
    Owner = 1,
    Staff = 2,
}
