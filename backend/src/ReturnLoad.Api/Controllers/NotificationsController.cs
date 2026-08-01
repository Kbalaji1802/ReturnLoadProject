using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReturnLoad.Api.Extensions;
using ReturnLoad.Api.Http;
using ReturnLoad.Application.UseCases.Notifications;

namespace ReturnLoad.Api.Controllers;

/// <summary>
/// In-app notifications (M7), read by client polling. Each user sees only their own inbox.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/notifications")]
[Authorize]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationService _notifications;

    public NotificationsController(INotificationService notifications) => _notifications = notifications;

    /// <summary>The caller's notifications, newest first.</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (!HttpContext.TryGetUserId(out Guid authUserId))
        {
            return Unauthorized();
        }

        var result = await _notifications.ListMineAsync(authUserId, cancellationToken);
        return result.ToApiResult(HttpContext);
    }

    /// <summary>Unread count for the notification badge.</summary>
    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken cancellationToken)
    {
        if (!HttpContext.TryGetUserId(out Guid authUserId))
        {
            return Unauthorized();
        }

        var result = await _notifications.UnreadCountAsync(authUserId, cancellationToken);
        return result.ToApiResult(HttpContext);
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        if (!HttpContext.TryGetUserId(out Guid authUserId))
        {
            return Unauthorized();
        }

        var result = await _notifications.MarkReadAsync(authUserId, id, cancellationToken);
        return result.ToApiResult(HttpContext, "Marked read.");
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        if (!HttpContext.TryGetUserId(out Guid authUserId))
        {
            return Unauthorized();
        }

        var result = await _notifications.MarkAllReadAsync(authUserId, cancellationToken);
        return result.ToApiResult(HttpContext, "All marked read.");
    }
}
