using ReturnLoad.Domain.Common;

namespace ReturnLoad.Domain.Administration;

/// <summary>
/// A message sent to a user across the booking/trip lifecycle (domain map §12). Carries its
/// own delivery lifecycle; the actual channel provider (push/SMS/email) is chosen in a later
/// milestone — this models the record and its state transitions only.
/// <para><b>Invariants:</b> recipient and body are required; a notification starts
/// <see cref="NotificationStatus.Pending"/>; only legal transitions
/// (Pending → Sent → Read, or Pending → Failed) are allowed.</para>
/// </summary>
public sealed class Notification : AggregateRoot<Guid>
{
    private Notification(Guid id, Guid recipientUserProfileId, NotificationChannel channel, string subject, string body)
        : base(id)
    {
        RecipientUserProfileId = recipientUserProfileId;
        Channel = channel;
        Subject = subject;
        Body = body;
        Status = NotificationStatus.Pending;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid RecipientUserProfileId { get; }

    public NotificationChannel Channel { get; }

    public string Subject { get; }

    public string Body { get; }

    public NotificationStatus Status { get; private set; }

    public string? FailureReason { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset? SentAtUtc { get; private set; }

    public static Notification Queue(Guid recipientUserProfileId, NotificationChannel channel, string subject, string body)
    {
        Guard.AgainstDefault(recipientUserProfileId, "Recipient id", "notification_recipient_required");
        string text = Guard.AgainstNullOrWhiteSpace(body, "Body", "notification_body_required");
        return new Notification(Guid.NewGuid(), recipientUserProfileId, channel, subject ?? string.Empty, text);
    }

    public void MarkSent()
    {
        Guard.Against(Status != NotificationStatus.Pending, "Only a pending notification can be sent.", "notification_not_pending");
        Status = NotificationStatus.Sent;
        SentAtUtc = DateTimeOffset.UtcNow;
    }

    public void MarkFailed(string reason)
    {
        Guard.Against(Status != NotificationStatus.Pending, "Only a pending notification can fail.", "notification_not_pending");
        Status = NotificationStatus.Failed;
        FailureReason = Guard.AgainstNullOrWhiteSpace(reason, "Failure reason", "notification_failure_reason_required");
    }

    public void MarkRead()
    {
        Guard.Against(Status != NotificationStatus.Sent, "Only a sent notification can be marked read.", "notification_not_sent");
        Status = NotificationStatus.Read;
    }
}
