namespace ReturnLoad.Domain.Fleet;

/// <summary>
/// Body/type of a goods vehicle. Drives cargo compatibility in matching filter 1
/// (<c>MATCHING_ENGINE.md</c> §2) — e.g. a reefer for perishables, a flatbed for
/// construction material.
/// </summary>
public enum VehicleType
{
    OpenBody = 0,
    ClosedContainer = 1,
    Flatbed = 2,
    Reefer = 3,
    Tanker = 4,
    Tipper = 5,
    LightCommercial = 6,
    Trailer = 7,
    Other = 99,
}

/// <summary>
/// Lifecycle of a vehicle record. A vehicle starts as <see cref="Draft"/> and may only
/// become <see cref="Active"/> (matchable) when its mandatory documents are verified and
/// unexpired (Trust &amp; Safety / matching filter 8).
/// </summary>
public enum VehicleStatus
{
    Draft = 0,
    Active = 1,
    Maintenance = 2,
    Suspended = 3,
}
