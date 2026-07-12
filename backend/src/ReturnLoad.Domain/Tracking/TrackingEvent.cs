using ReturnLoad.Domain.Common;

namespace ReturnLoad.Domain.Tracking;

/// <summary>
/// A timestamped location/status point on a trip (domain map §12). Append-only: once
/// captured it is never mutated. Honours the offline contract
/// (<c>OFFLINE_STRATEGY.md</c> §7): it stores the driver's real <see cref="CapturedAtUtc"/>
/// (device time) separately from the server <see cref="RecordedAtUtc"/>, so a batch flushed
/// after reconnect preserves the trip's true path rather than back-filling upload times.
/// <para><b>Invariants:</b> trip and a location point are required; captured-at is required
/// and cannot be in the future relative to when it is recorded.</para>
/// </summary>
public sealed class TrackingEvent : AggregateRoot<Guid>
{
    private TrackingEvent(
        Guid id,
        Guid tripId,
        TrackingEventType type,
        LocationPoint point,
        DateTimeOffset capturedAtUtc,
        DateTimeOffset recordedAtUtc)
        : base(id)
    {
        TripId = tripId;
        Type = type;
        Point = point;
        CapturedAtUtc = capturedAtUtc;
        RecordedAtUtc = recordedAtUtc;
    }

    public Guid TripId { get; }

    public TrackingEventType Type { get; }

    public LocationPoint Point { get; }

    /// <summary>The device's real capture time — never the upload time (offline truthfulness).</summary>
    public DateTimeOffset CapturedAtUtc { get; }

    /// <summary>When the server ingested the event (may be much later than capture).</summary>
    public DateTimeOffset RecordedAtUtc { get; }

    /// <summary>
    /// Records a captured tracking event. <paramref name="recordedAtUtc"/> is the ingestion
    /// time (typically now); it must not precede the capture time.
    /// </summary>
    public static TrackingEvent Capture(
        Guid tripId,
        TrackingEventType type,
        LocationPoint point,
        DateTimeOffset capturedAtUtc,
        DateTimeOffset recordedAtUtc)
    {
        Guard.AgainstDefault(tripId, "Trip id", "tracking_trip_required");
        ArgumentNullException.ThrowIfNull(point);
        Guard.AgainstDefault(capturedAtUtc, "Captured-at", "tracking_captured_required");
        Guard.Against(recordedAtUtc < capturedAtUtc, "Recorded-at cannot precede captured-at.", "tracking_recorded_before_captured");
        return new TrackingEvent(Guid.NewGuid(), tripId, type, point, capturedAtUtc, recordedAtUtc);
    }
}
