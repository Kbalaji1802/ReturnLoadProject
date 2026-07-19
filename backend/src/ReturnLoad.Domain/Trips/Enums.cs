namespace ReturnLoad.Domain.Trips;

/// <summary>
/// Lifecycle of a trip — the loaded journey a driver runs after a booking is accepted
/// (M4.3 Step 5). A single linear progression with a Cancelled branch before completion:
/// Created → DriverAccepted → DriverEnRoute → ArrivedPickup → Loaded → InTransit →
/// ArrivedDestination → Unloaded → Completed.
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
}
