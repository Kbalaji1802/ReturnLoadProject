using ReturnLoad.Domain.Common;

namespace ReturnLoad.Domain.Documents;

/// <summary>The outcome an Operations reviewer recorded on a document.</summary>
public enum DocumentReviewDecision
{
    Approved = 0,
    Rejected = 1,
}

/// <summary>
/// One immutable Operations decision on a <see cref="Document"/> (correction-sprint Part 8) — an
/// <b>append-only</b> record (like <c>TrackingEvent</c>/<c>AuditLog</c>, ADR-0014). The stream of
/// these for a document is its full review history: every approve/reject with its reason, who
/// decided, and when — so a rejection reason is never overwritten and re-uploads keep the trail.
/// </summary>
public sealed class DocumentReview : AggregateRoot<Guid>
{
    private DocumentReview(Guid id, Guid documentId, DocumentReviewDecision decision, string? reason, Guid decidedByUserId, DateTimeOffset decidedAtUtc)
        : base(id)
    {
        DocumentId = documentId;
        Decision = decision;
        Reason = reason;
        DecidedByUserId = decidedByUserId;
        DecidedAtUtc = decidedAtUtc;
    }

    private DocumentReview()
    {
    }

    public Guid DocumentId { get; }

    public DocumentReviewDecision Decision { get; }

    /// <summary>The reviewer's reason — required for a rejection, null for an approval.</summary>
    public string? Reason { get; }

    /// <summary>The reviewing staff member's user id (from their token). <see cref="Guid.Empty"/> if unattributed.</summary>
    public Guid DecidedByUserId { get; }

    public DateTimeOffset DecidedAtUtc { get; }

    /// <summary>Records an approval decision.</summary>
    public static DocumentReview Approved(Guid documentId, Guid decidedByUserId, DateTimeOffset decidedAtUtc) =>
        new(Guid.NewGuid(), documentId, DocumentReviewDecision.Approved, null, decidedByUserId, decidedAtUtc);

    /// <summary>Records a rejection decision with its reason.</summary>
    public static DocumentReview Rejected(Guid documentId, string reason, Guid decidedByUserId, DateTimeOffset decidedAtUtc)
    {
        string trimmed = Guard.AgainstNullOrWhiteSpace(reason, "Rejection reason", "document_review_reason_required");
        return new DocumentReview(Guid.NewGuid(), documentId, DocumentReviewDecision.Rejected, trimmed, decidedByUserId, decidedAtUtc);
    }
}
