using ReturnLoad.Application.Abstractions.Persistence;
using ReturnLoad.Domain.Administration;
using ReturnLoad.Domain.Identity;
using ReturnLoad.Shared.Results;

namespace ReturnLoad.Application.UseCases.Notifications;

public sealed record NotificationView(Guid Id, string Subject, string Body, bool Read, DateTimeOffset CreatedAtUtc);

/// <summary>
/// In-app notifications (M7), delivered by <b>polling</b> — no Firebase/push provider (that plugs
/// in behind the <see cref="NotificationChannel"/> seam later). Business services call the
/// <c>Notify*</c> methods at lifecycle events; the notification is enlisted in the caller's unit of
/// work, so it commits atomically with the change that triggered it. Clients read their own inbox.
/// </summary>
public interface INotificationService
{
    /// <summary>Queues an in-app notification for a user profile. Enlists in the current unit of
    /// work — the caller's SaveChanges persists it (do not call SaveChanges here).</summary>
    Task NotifyUserAsync(Guid recipientUserProfileId, string subject, string body, CancellationToken cancellationToken = default);

    /// <summary>Queues an in-app notification for a driver (resolved to their user profile).</summary>
    Task NotifyDriverAsync(Guid driverProfileId, string subject, string body, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<NotificationView>>> ListMineAsync(Guid authUserId, CancellationToken cancellationToken = default);

    Task<Result<int>> UnreadCountAsync(Guid authUserId, CancellationToken cancellationToken = default);

    Task<Result> MarkReadAsync(Guid authUserId, Guid notificationId, CancellationToken cancellationToken = default);

    Task<Result> MarkAllReadAsync(Guid authUserId, CancellationToken cancellationToken = default);
}

internal sealed class NotificationService : INotificationService
{
    private readonly IRepository<Notification> _notifications;
    private readonly IRepository<UserProfile> _users;
    private readonly IRepository<DriverProfile> _drivers;
    private readonly IUnitOfWork _uow;

    public NotificationService(
        IRepository<Notification> notifications,
        IRepository<UserProfile> users,
        IRepository<DriverProfile> drivers,
        IUnitOfWork uow)
    {
        _notifications = notifications;
        _users = users;
        _drivers = drivers;
        _uow = uow;
    }

    public async Task NotifyUserAsync(Guid recipientUserProfileId, string subject, string body, CancellationToken cancellationToken = default)
    {
        if (recipientUserProfileId == Guid.Empty)
        {
            return;
        }

        Notification notification = Notification.Queue(recipientUserProfileId, NotificationChannel.InApp, subject, body);
        notification.MarkSent(); // in-app: immediately available in the recipient's inbox
        await _notifications.AddAsync(notification, cancellationToken);
        // No SaveChanges: enlisted in the caller's unit of work so it commits with the trigger.
    }

    public async Task NotifyDriverAsync(Guid driverProfileId, string subject, string body, CancellationToken cancellationToken = default)
    {
        DriverProfile? driver = await _drivers.GetByIdAsync(driverProfileId, cancellationToken);
        if (driver is not null)
        {
            await NotifyUserAsync(driver.UserProfileId, subject, body, cancellationToken);
        }
    }

    public async Task<Result<IReadOnlyList<NotificationView>>> ListMineAsync(Guid authUserId, CancellationToken cancellationToken = default)
    {
        Guid? profileId = await ResolveProfileIdAsync(authUserId, cancellationToken);
        if (profileId is null)
        {
            return Result<IReadOnlyList<NotificationView>>.Success([]);
        }

        IReadOnlyList<Notification> items = await _notifications.ListAsync(n => n.RecipientUserProfileId == profileId, cancellationToken);
        IReadOnlyList<NotificationView> views = items
            .OrderByDescending(n => n.CreatedAtUtc)
            .Select(n => new NotificationView(n.Id, n.Subject, n.Body, n.Status == NotificationStatus.Read, n.CreatedAtUtc))
            .ToList();
        return Result<IReadOnlyList<NotificationView>>.Success(views);
    }

    public async Task<Result<int>> UnreadCountAsync(Guid authUserId, CancellationToken cancellationToken = default)
    {
        Guid? profileId = await ResolveProfileIdAsync(authUserId, cancellationToken);
        if (profileId is null)
        {
            return Result<int>.Success(0);
        }

        IReadOnlyList<Notification> items = await _notifications.ListAsync(
            n => n.RecipientUserProfileId == profileId && n.Status != NotificationStatus.Read, cancellationToken);
        return Result<int>.Success(items.Count);
    }

    public async Task<Result> MarkReadAsync(Guid authUserId, Guid notificationId, CancellationToken cancellationToken = default)
    {
        Guid? profileId = await ResolveProfileIdAsync(authUserId, cancellationToken);
        Notification? notification = await _notifications.GetByIdAsync(notificationId, cancellationToken);
        if (notification is null || profileId is null || notification.RecipientUserProfileId != profileId)
        {
            return Result.Failure(Error.NotFound("Notification not found."));
        }

        if (notification.Status == NotificationStatus.Sent)
        {
            notification.MarkRead();
            _notifications.Update(notification);
            await _uow.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }

    public async Task<Result> MarkAllReadAsync(Guid authUserId, CancellationToken cancellationToken = default)
    {
        Guid? profileId = await ResolveProfileIdAsync(authUserId, cancellationToken);
        if (profileId is null)
        {
            return Result.Success();
        }

        IReadOnlyList<Notification> unread = await _notifications.ListAsync(
            n => n.RecipientUserProfileId == profileId && n.Status == NotificationStatus.Sent, cancellationToken);
        foreach (Notification notification in unread)
        {
            notification.MarkRead();
            _notifications.Update(notification);
        }

        if (unread.Count > 0)
        {
            await _uow.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }

    private async Task<Guid?> ResolveProfileIdAsync(Guid authUserId, CancellationToken cancellationToken)
    {
        UserProfile? profile = (await _users.ListAsync(u => u.AuthUserId == authUserId, cancellationToken)).FirstOrDefault();
        return profile?.Id;
    }
}
