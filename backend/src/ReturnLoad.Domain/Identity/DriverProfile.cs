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
        Availability = DriverAvailability.Offline;
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

    /// <summary>
    /// Operational availability to receive new loads (Part 3). Independent of <see cref="Status"/>:
    /// a driver is matchable only when verified (<see cref="DriverStatus.Active"/>) <b>and</b>
    /// <see cref="DriverAvailability.Available"/>.
    /// </summary>
    public DriverAvailability Availability { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public bool IsTransactable => Status == DriverStatus.Active;

    /// <summary>Matchable = verified and available (both gates, correction-sprint Parts 2–3).</summary>
    public bool IsAvailableForLoads => Status == DriverStatus.Active && Availability == DriverAvailability.Available;

    /// <summary>Driver's last-known latitude, if they have shared a location (Part 7 owner view / matching).</summary>
    public double? LastKnownLatitude { get; private set; }

    /// <summary>Driver's last-known longitude, if they have shared a location.</summary>
    public double? LastKnownLongitude { get; private set; }

    /// <summary>When the last-known location was captured (staleness signal for the owner).</summary>
    public DateTimeOffset? LastLocationAtUtc { get; private set; }

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

    /// <summary>
    /// The driver sets their own availability (Part 3). <see cref="DriverAvailability.Busy"/> is
    /// system-managed (see <see cref="MarkBusy"/>) and cannot be chosen manually — a driver becomes
    /// busy by being assigned a trip, not by tapping a button.
    /// </summary>
    public void SetAvailability(DriverAvailability target)
    {
        Guard.Against(
            target == DriverAvailability.Busy,
            "Busy is set automatically when you are on a trip.",
            "driver_busy_not_manual");
        ChangeAvailability(target);
    }

    /// <summary>
    /// Marks the driver busy because a trip was assigned to them (called from booking acceptance).
    /// Idempotent — safe to call when already busy.
    /// </summary>
    public void MarkBusy() => ChangeAvailability(DriverAvailability.Busy);

    /// <summary>
    /// Releases the driver from <see cref="DriverAvailability.Busy"/> when their trip ends,
    /// returning them to <see cref="DriverAvailability.Available"/>. Only transitions <b>out of
    /// Busy</b> — a driver who set themselves Offline/OnLeave/VehicleService stays there.
    /// </summary>
    public void ReleaseFromTrip()
    {
        if (Availability == DriverAvailability.Busy)
        {
            ChangeAvailability(DriverAvailability.Available);
        }
    }

    /// <summary>
    /// Records the driver's current location (Part 7). Fed by live tracking pings and by a driver
    /// sharing GPS when clocking in — so the load owner can see each candidate's distance/ETA to the
    /// pickup and matching can honour the pickup radius (ADR-0019).
    /// </summary>
    public void RecordLocation(double latitude, double longitude, DateTimeOffset atUtc)
    {
        LastKnownLatitude = latitude;
        LastKnownLongitude = longitude;
        LastLocationAtUtc = atUtc;
    }

    private void ChangeAvailability(DriverAvailability target)
    {
        if (Availability == target)
        {
            return;
        }

        Availability = target;
        Raise(new DriverAvailabilityChanged(Id, target, DateTimeOffset.UtcNow));
    }

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
