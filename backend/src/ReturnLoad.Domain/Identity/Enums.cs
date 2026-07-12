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
