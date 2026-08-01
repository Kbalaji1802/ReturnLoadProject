namespace ReturnLoad.Domain.Tracking;

/// <summary>
/// The kind of tracking event captured on a trip. Location pings feed the trip path; the
/// status points mirror the driver's offline-capable status updates
/// (<c>OFFLINE_STRATEGY.md</c> §2).
/// </summary>
public enum TrackingEventType
{
    LocationPing = 0,
    PickedUp = 1,
    InTransit = 2,
    Delivered = 3,
    Exception = 4,
}

/// <summary>Where a tracking point came from (M6). Lets analytics/fraud tools distinguish real
/// device GPS from manual or system-generated points.</summary>
public enum TrackingSource
{
    Device = 0,
    Manual = 1,
    System = 2,
}
