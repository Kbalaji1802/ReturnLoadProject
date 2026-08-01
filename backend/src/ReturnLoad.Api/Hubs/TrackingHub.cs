using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ReturnLoad.Api.Http;
using ReturnLoad.Application.Identity;
using ReturnLoad.Application.UseCases.Tracking;

namespace ReturnLoad.Api.Hubs;

/// <summary>
/// Realtime live-tracking hub (Part 6). A watcher (load owner or admin) subscribes to a trip and
/// receives a <c>position</c> event each time the driver's device reports a new location, so the map
/// marker moves without polling. Subscription is <b>authorised</b> the same way the REST live view is:
/// only the trip's driver, the load owner, or privileged staff may watch a given trip.
/// </summary>
[Authorize]
public sealed class TrackingHub : Hub
{
    private readonly ITrackingService _tracking;

    public TrackingHub(ITrackingService tracking) => _tracking = tracking;

    internal static string GroupFor(Guid tripId) => $"trip-{tripId}";

    /// <summary>Subscribe to a trip's live positions after an authorization check; throws if not permitted.</summary>
    public async Task Subscribe(Guid tripId)
    {
        if (!Context.GetHttpContext()!.TryGetUserId(out Guid authUserId))
        {
            throw new HubException("Not authenticated.");
        }

        bool privileged = Roles.Internal.Any(Context.User!.IsInRole);

        // Reuse the REST read-authorization: a successful live read means this caller may watch.
        var access = await _tracking.GetLiveAsync(authUserId, tripId, privileged, Context.ConnectionAborted);
        if (access.IsFailure)
        {
            throw new HubException("You are not allowed to watch this trip.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(tripId));
    }

    /// <summary>Stop receiving a trip's live positions.</summary>
    public Task Unsubscribe(Guid tripId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupFor(tripId));
}
