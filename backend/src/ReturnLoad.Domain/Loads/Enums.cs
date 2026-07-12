namespace ReturnLoad.Domain.Loads;

/// <summary>
/// The nature of the cargo. Drives vehicle-type compatibility in matching filter 1
/// (<c>MATCHING_ENGINE.md</c> §2) — e.g. perishables need a reefer.
/// </summary>
public enum CargoType
{
    General = 0,
    Perishable = 1,
    Fragile = 2,
    Hazardous = 3,
    Construction = 4,
    Liquid = 5,
    Refrigerated = 6,
    Other = 99,
}

/// <summary>
/// Lifecycle of a load posted by a shipper. Matching only <b>proposes</b>; a load advances
/// through these states as matches are accepted and the trip runs (glossary §8;
/// <c>MATCHING_ENGINE.md</c> §5). Match/Booking are their own future aggregates.
/// </summary>
public enum LoadStatus
{
    /// <summary>Being prepared by the shipper; not yet visible to matching.</summary>
    Draft = 0,

    /// <summary>Open and available for matching.</summary>
    Posted = 1,

    /// <summary>A match has been proposed/accepted.</summary>
    Matched = 2,

    /// <summary>Committed by both sides.</summary>
    Booked = 3,

    /// <summary>Picked up and on the way.</summary>
    InTransit = 4,

    /// <summary>Delivered to destination.</summary>
    Delivered = 5,

    /// <summary>Withdrawn before completion.</summary>
    Cancelled = 6,
}
