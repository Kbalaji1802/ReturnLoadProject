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
