using Microsoft.AspNetCore.SignalR;
using ReturnLoad.Application.Abstractions.Realtime;

namespace ReturnLoad.Api.Hubs;

/// <summary>
/// SignalR implementation of <see cref="ILiveTrackingNotifier"/> (Part 6): broadcasts each new
/// position to the trip's watcher group. Registered in the API layer so the Application layer stays
/// transport-agnostic (ADR-0006). Never throws to the caller — a realtime hiccup must not fail
/// location ingestion (the position is already persisted and available via the REST live view).
/// </summary>
public sealed class SignalRLiveTrackingNotifier : ILiveTrackingNotifier
{
    private readonly IHubContext<TrackingHub> _hub;
    private readonly ILogger<SignalRLiveTrackingNotifier> _logger;

    public SignalRLiveTrackingNotifier(IHubContext<TrackingHub> hub, ILogger<SignalRLiveTrackingNotifier> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task PositionRecordedAsync(TripLivePush push, CancellationToken cancellationToken = default)
    {
        try
        {
            await _hub.Clients.Group(TrackingHub.GroupFor(push.TripId)).SendAsync("position", push, cancellationToken);
        }
        catch (Exception ex)
        {
            // Fail-soft: the position is persisted; the live REST endpoint still serves it.
            _logger.LogWarning(ex, "Failed to broadcast live position for trip {TripId}", push.TripId);
        }
    }
}
