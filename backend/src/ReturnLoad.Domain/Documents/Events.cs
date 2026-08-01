using ReturnLoad.Domain.Common;

namespace ReturnLoad.Domain.Documents;

/// <summary>A document was submitted for verification.</summary>
public sealed record DocumentUploaded(
    Guid DocumentId,
    DocumentOwnerType OwnerType,
    Guid OwnerId,
    DocumentType Type,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

/// <summary>A document passed verification.</summary>
public sealed record DocumentVerified(Guid DocumentId, DateTimeOffset OccurredAtUtc) : IDomainEvent;

/// <summary>
/// A document was rejected by Operations with a reason (correction-sprint Part 8). Drives the
/// "driver notified → re-upload" step so the owner learns what to fix.
/// </summary>
public sealed record DocumentRejected(
    Guid DocumentId, DocumentOwnerType OwnerType, Guid OwnerId, string Reason, DateTimeOffset OccurredAtUtc) : IDomainEvent;
