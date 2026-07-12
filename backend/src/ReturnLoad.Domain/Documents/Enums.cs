namespace ReturnLoad.Domain.Documents;

/// <summary>The kind of subject a document belongs to (Trust &amp; Safety §2).</summary>
public enum DocumentOwnerType
{
    Driver = 0,
    Vehicle = 1,
    Carrier = 2,
}

/// <summary>
/// Document kinds — the Indian verification pillars plus proof-of-delivery
/// (<c>08_TRUST_AND_SAFETY.md</c> §2).
/// </summary>
public enum DocumentType
{
    DriverKyc = 0,
    RegistrationCertificate = 1,
    Insurance = 2,
    DrivingLicence = 3,
    Permit = 4,
    FitnessCertificate = 5,
    PollutionCertificate = 6,
    ProofOfDelivery = 7,
    Other = 99,
}

/// <summary>
/// Verification state of a document — a <b>state with an expiry</b>, never a permanent
/// "verified" flag (<c>08_TRUST_AND_SAFETY.md</c> §2, principle 3). Transitions:
/// NotSubmitted → Submitted → UnderReview → Verified, with Rejected and (auto) Expired
/// branches. "Fail closed": ambiguity stays non-Verified.
/// </summary>
public enum VerificationStatus
{
    NotSubmitted = 0,
    Submitted = 1,
    UnderReview = 2,
    Verified = 3,
    Rejected = 4,
    Expired = 5,
}

/// <summary>Record lifecycle of a document (separate from its verification state).</summary>
public enum DocumentStatus
{
    /// <summary>The current document of its type for the owner.</summary>
    Active = 0,

    /// <summary>Replaced by a newer submission — kept for audit, not used for gating.</summary>
    Archived = 1,
}
