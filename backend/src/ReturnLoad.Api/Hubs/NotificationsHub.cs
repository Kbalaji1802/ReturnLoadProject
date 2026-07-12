using Microsoft.AspNetCore.SignalR;

namespace ReturnLoad.Api.Hubs;

/// <summary>
/// SignalR realtime <b>foundation</b> (bootstrap scope only). This hub establishes
/// the realtime endpoint and connection-lifecycle logging so future features —
/// live match, booking, and tracking updates (03_TECHNICAL_BIBLE.md §4) — have a
/// wired entry point. It intentionally exposes no business methods yet.
/// </summary>
public sealed class NotificationsHub : Hub
{
    private readonly ILogger<NotificationsHub> _logger;

    public NotificationsHub(ILogger<NotificationsHub> logger) => _logger = logger;

    public override Task OnConnectedAsync()
    {
        _logger.LogInformation("SignalR client connected: {ConnectionId}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("SignalR client disconnected: {ConnectionId}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
