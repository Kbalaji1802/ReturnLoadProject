using ReturnLoad.Domain.Common;

namespace ReturnLoad.Domain.Identity;

/// <summary>
/// A driver — the person operating a truck (glossary §8). References a
/// <see cref="UserProfile"/> and holds driver-specific identity (licence, optional Aadhaar
/// for KYC). Verification is a <b>pre-trip gate</b>: a driver only becomes
/// <see cref="DriverStatus.Active"/> once required documents are verified
/// (<c>08_TRUST_AND_SAFETY.md</c>) — that cross-aggregate check lives in the application
/// layer, which then calls <see cref="MarkVerified"/>.
/// <para><b>Invariants:</b> licence required; starts <see cref="DriverStatus.Pending"/>;
/// a Blocked driver cannot be verified or reinstated; only Pending → Active on verify.</para>
/// </summary>
public sealed class DriverProfile : AggregateRoot<Guid>
{
    private DriverProfile(Guid id, Guid userProfileId, DrivingLicenceNumber licence, AadhaarNumber? aadhaar)
        : base(id)
    {
        UserProfileId = userProfileId;
        Licence = licence;
        Aadhaar = aadhaar;
        Status = DriverStatus.Pending;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    private DriverProfile()
    {
    }

    public Guid UserProfileId { get; }

    public DrivingLicenceNumber Licence { get; private set; } = null!;

    /// <summary>Optional KYC identifier — sensitive PII (see <see cref="AadhaarNumber"/>).</summary>
    public AadhaarNumber? Aadhaar { get; private set; }

    public DriverStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public bool IsTransactable => Status == DriverStatus.Active;

    public static DriverProfile Register(Guid userProfileId, DrivingLicenceNumber licence, AadhaarNumber? aadhaar = null)
    {
        Guard.AgainstDefault(userProfileId, "User profile id", "driver_user_required");
        ArgumentNullException.ThrowIfNull(licence);

        DriverProfile driver = new(Guid.NewGuid(), userProfileId, licence, aadhaar);
        driver.Raise(new DriverRegistered(driver.Id, userProfileId, driver.CreatedAtUtc));
        return driver;
    }

    /// <summary>Promotes a verified driver to Active. Only valid from Pending.</summary>
    public void MarkVerified()
    {
        Guard.Against(Status == DriverStatus.Blocked, "A blocked driver cannot be verified.", "driver_blocked");
        Guard.Against(Status != DriverStatus.Pending, "Only a pending driver can be verified.", "driver_not_pending");
        Status = DriverStatus.Active;
        Raise(new DriverVerified(Id, DateTimeOffset.UtcNow));
    }

    public void Suspend()
    {
        Guard.Against(Status == DriverStatus.Blocked, "A blocked driver cannot be suspended.", "driver_blocked");
        Status = DriverStatus.Suspended;
    }

    public void Reinstate()
    {
        Guard.Against(Status == DriverStatus.Blocked, "A blocked driver cannot be reinstated.", "driver_blocked");
        Guard.Against(Status != DriverStatus.Suspended, "Only a suspended driver can be reinstated.", "driver_not_suspended");
        Status = DriverStatus.Active;
    }

    public void Block() => Status = DriverStatus.Blocked;

    public void UpdateLicence(DrivingLicenceNumber licence)
    {
        ArgumentNullException.ThrowIfNull(licence);
        Licence = licence;
    }

    public void RecordAadhaar(AadhaarNumber aadhaar)
    {
        ArgumentNullException.ThrowIfNull(aadhaar);
        Aadhaar = aadhaar;
    }
}
