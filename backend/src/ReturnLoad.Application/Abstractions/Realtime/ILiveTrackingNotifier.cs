namespace ReturnLoad.Application.Abstractions.Realtime;

/// <summary>
/// A live position broadcast to the load owner / admins watching a trip (Part 6). Provider-agnostic
/// so the Application layer never depends on SignalR (the transport lives in the API layer, ADR-0006).
/// </summary>
public sealed record TripLivePush(
    Guid TripId,
    double Latitude,
    double Longitude,
    DateTimeOffset CapturedAtUtc,
    double? SpeedKph,
    double? HeadingDegrees,
    double? DistanceRemainingKm,
    int? EtaMinutes);

/// <summary>
/// Pushes live tracking updates to subscribed watchers (owner/admin) so the map's truck marker moves
/// in real time instead of by polling (Part 6). Implemented over SignalR in the API layer; a no-op
/// default keeps the Application layer runnable without a realtime transport (e.g. in tests).
/// </summary>
public interface ILiveTrackingNotifier
{
    /// <summary>Broadcasts a new position for a trip to everyone watching it. Never throws to the caller.</summary>
    Task PositionRecordedAsync(TripLivePush push, CancellationToken cancellationToken = default);
}

/// <summary>Default notifier that does nothing — used when no realtime transport is wired.</summary>
public sealed class NoOpLiveTrackingNotifier : ILiveTrackingNotifier
{
    public Task PositionRecordedAsync(TripLivePush push, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
