namespace ReturnLoad.Domain.Trips;

/// <summary>
/// Lifecycle of a trip — a truck's planned movement including the return leg (glossary §8).
/// Created → Assigned → Started → InTransit → Completed, with a Cancelled branch before
/// completion.
/// </summary>
public enum TripStatus
{
    Created = 0,
    Assigned = 1,
    Started = 2,
    InTransit = 3,
    Completed = 4,
    Cancelled = 5,
}
