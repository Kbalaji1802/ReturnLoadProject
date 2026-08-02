using ReturnLoad.Domain.Common;

namespace ReturnLoad.Domain.Documents;

/// <summary>
/// A record that one expiry reminder was sent for a <see cref="Document"/> at one threshold
/// (RC-2 Part 13) — <b>append-only</b>, like <see cref="DocumentReview"/>.
/// <para>
/// This exists to make the reminder sweep idempotent. The sweep runs on a timer and re-evaluates
/// every unexpired document each pass, so without a record of what has already gone out a driver
/// would be notified again on every tick for the whole window. The pair
/// (<see cref="DocumentId"/>, <see cref="ThresholdDays"/>) is unique: each threshold fires once
/// per document, ever.
/// </para>
/// </summary>
public sealed class DocumentExpiryReminder : AggregateRoot<Guid>
{
    /// <summary>The threshold value used for the "already expired" reminder.</summary>
    public const int ExpiredThreshold = 0;

    private DocumentExpiryReminder(Guid id, Guid documentId, int thresholdDays, DateTimeOffset sentAtUtc)
        : base(id)
    {
        DocumentId = documentId;
        ThresholdDays = thresholdDays;
        SentAtUtc = sentAtUtc;
    }

    private DocumentExpiryReminder()
    {
    }

    public Guid DocumentId { get; }

    /// <summary>
    /// Days-before-expiry this reminder covered — 30, 15, 7, 1, or <see cref="ExpiredThreshold"/>
    /// (0) for the document having lapsed.
    /// </summary>
    public int ThresholdDays { get; }

    public DateTimeOffset SentAtUtc { get; }

    public static DocumentExpiryReminder Sent(Guid documentId, int thresholdDays, DateTimeOffset sentAtUtc) =>
        new(Guid.NewGuid(), documentId, thresholdDays, sentAtUtc);
}
