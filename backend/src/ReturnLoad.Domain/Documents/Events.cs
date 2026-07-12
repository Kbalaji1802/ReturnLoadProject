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
