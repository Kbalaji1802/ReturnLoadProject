using ReturnLoad.Domain.Common;

namespace ReturnLoad.Domain.Documents;

/// <summary>
/// A verifiable document belonging to a Driver, Vehicle, or Carrier (licence, RC, insurance,
/// permit, KYC, PoD). Encapsulates the verification state machine and expiry rule from
/// <c>08_TRUST_AND_SAFETY.md</c>: verification is a state that can expire, and an
/// expired/unverified document must never be treated as valid ("fail closed"). The stored
/// file lives behind <c>IFileStorageService</c> (ADR-0012); only its key is referenced here.
/// <para><b>Invariants:</b> owner + storage key required; cannot verify an expired document;
/// legal transitions only; rejection requires a reason.</para>
/// </summary>
public sealed class Document : AggregateRoot<Guid>
{
    private Document(
        Guid id,
        DocumentOwnerType ownerType,
        Guid ownerId,
        DocumentType type,
        string storageKey,
        string? documentNumber,
        DateOnly? issuedOn,
        DateOnly? expiresOn)
        : base(id)
    {
        OwnerType = ownerType;
        OwnerId = ownerId;
        Type = type;
        StorageKey = storageKey;
        DocumentNumber = documentNumber;
        IssuedOn = issuedOn;
        ExpiresOn = expiresOn;
        VerificationStatus = VerificationStatus.Submitted;
        Status = DocumentStatus.Active;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    private Document()
    {
    }

    public DocumentOwnerType OwnerType { get; }

    public Guid OwnerId { get; }

    public DocumentType Type { get; }

    /// <summary>Key into file storage (ADR-0012); the raw file is never held in the domain.</summary>
    public string StorageKey { get; private set; } = null!;

    /// <summary>The document's own number (RC number, policy number, DL number, …), if any.</summary>
    public string? DocumentNumber { get; private set; }

    public DateOnly? IssuedOn { get; private set; }

    public DateOnly? ExpiresOn { get; private set; }

    public VerificationStatus VerificationStatus { get; private set; }

    public DocumentStatus Status { get; private set; }

    public string? RejectionReason { get; private set; }

    public DateTimeOffset? VerifiedAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    /// <summary>Submits a new document for verification and raises <see cref="DocumentUploaded"/>.</summary>
    public static Document Submit(
        DocumentOwnerType ownerType,
        Guid ownerId,
        DocumentType type,
        string storageKey,
        string? documentNumber = null,
        DateOnly? issuedOn = null,
        DateOnly? expiresOn = null)
    {
        Guard.AgainstDefault(ownerId, "Owner id", "document_owner_required");
        string key = Guard.AgainstNullOrWhiteSpace(storageKey, "Storage key", "document_storage_required");
        Guard.Against(
            issuedOn.HasValue && expiresOn.HasValue && expiresOn.Value < issuedOn.Value,
            "Expiry date cannot precede the issue date.",
            "document_expiry_before_issue");

        Document document = new(Guid.NewGuid(), ownerType, ownerId, type, key, documentNumber, issuedOn, expiresOn);
        document.Raise(new DocumentUploaded(document.Id, ownerType, ownerId, type, document.CreatedAtUtc));
        return document;
    }

    public void StartReview()
    {
        Guard.Against(VerificationStatus != VerificationStatus.Submitted, "Only a submitted document can enter review.", "document_not_submitted");
        VerificationStatus = VerificationStatus.UnderReview;
    }

    /// <summary>Marks the document verified. Rejects if it is already expired ("fail closed").</summary>
    public void Verify(DateOnly asOf, DateTimeOffset verifiedAtUtc)
    {
        Guard.Against(
            VerificationStatus is not (VerificationStatus.UnderReview or VerificationStatus.Submitted),
            "Only a submitted or under-review document can be verified.",
            "document_not_verifiable");
        Guard.Against(IsExpiredAsOf(asOf), "An expired document cannot be verified.", "document_expired");

        VerificationStatus = VerificationStatus.Verified;
        VerifiedAtUtc = verifiedAtUtc;
        RejectionReason = null;
        Raise(new DocumentVerified(Id, verifiedAtUtc));
    }

    public void Reject(string reason)
    {
        RejectionReason = Guard.AgainstNullOrWhiteSpace(reason, "Rejection reason", "document_reject_reason_required");
        VerificationStatus = VerificationStatus.Rejected;
        VerifiedAtUtc = null;
    }

    /// <summary>Auto-transition on/after the expiry date; flips a Verified doc back to non-valid.</summary>
    public void Expire() => VerificationStatus = VerificationStatus.Expired;

    public void Archive() => Status = DocumentStatus.Archived;

    /// <summary>True when the document has an expiry date that is strictly before <paramref name="asOf"/>.</summary>
    public bool IsExpiredAsOf(DateOnly asOf) => ExpiresOn.HasValue && ExpiresOn.Value < asOf;

    /// <summary>
    /// A document is "valid" for gating only when it is Active, Verified, and not expired
    /// (drives driver/vehicle transactability — Trust &amp; Safety, matching filters 7–8).
    /// </summary>
    public bool IsValidAsOf(DateOnly asOf) =>
        Status == DocumentStatus.Active
        && VerificationStatus == VerificationStatus.Verified
        && !IsExpiredAsOf(asOf);
}
