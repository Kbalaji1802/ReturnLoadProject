namespace ReturnLoad.Domain.Identity;

/// <summary>Interface language a user prefers (market scope Tamil &amp; English, ADR-0004).</summary>
public enum Language
{
    English = 0,
    Tamil = 1,
}

/// <summary>Lifecycle of a driver on the platform (distinct from auth account status).</summary>
public enum DriverStatus
{
    /// <summary>Registered but not yet verified — cannot transact.</summary>
    Pending = 0,

    /// <summary>Verified and able to be matched/booked.</summary>
    Active = 1,

    /// <summary>Temporarily halted by Operations (reversible).</summary>
    Suspended = 2,

    /// <summary>Hard-blocked (fraud/blacklist) — terminal.</summary>
    Blocked = 3,
}

/// <summary>
/// A driver's <b>operational availability</b> to receive new loads — orthogonal to
/// <see cref="DriverStatus"/> (verification/moderation). Only an <see cref="Available"/> driver is
/// matched (MATCHING_ENGINE.md §2 filter 6, correction-sprint Part 3). <see cref="Busy"/> is
/// system-managed (set while the driver is on an active trip); the other states are driver-chosen.
/// </summary>
public enum DriverAvailability
{
    /// <summary>Ready to receive loads — the only state the matching engine surfaces loads to.</summary>
    Available = 0,

    /// <summary>On an active trip. System-managed; a busy driver never receives new loads.</summary>
    Busy = 1,

    /// <summary>Clocked out / app-idle. Not matched. Driver-chosen.</summary>
    Offline = 2,

    /// <summary>Away for a period (personal leave). Not matched. Driver-chosen.</summary>
    OnLeave = 3,

    /// <summary>Vehicle undergoing service/maintenance. Not matched. Driver-chosen.</summary>
    VehicleService = 4,
}

/// <summary>Lifecycle of a carrier organisation.</summary>
public enum CarrierStatus
{
    Pending = 0,
    Active = 1,
    Suspended = 2,
    Blocked = 3,
}

/// <summary>The carrier-scoped role a member holds via an <see cref="Association"/> (§13).</summary>
public enum AssociationRole
{
    CarrierOwner = 0,
    Dispatcher = 1,
    Driver = 2,
    AssociationManager = 3,
}

/// <summary>Lifecycle of a member's association with a carrier.</summary>
public enum AssociationStatus
{
    Pending = 0,
    Active = 1,
    Revoked = 2,
}
