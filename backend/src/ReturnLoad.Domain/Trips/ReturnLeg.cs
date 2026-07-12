using ReturnLoad.Domain.Common;
using ReturnLoad.Domain.ValueObjects;

namespace ReturnLoad.Domain.Trips;

/// <summary>
/// The empty leg of a trip we aim to convert into a paid load — the platform's core value
/// (glossary §8, Business Bible §2). Describes where the truck will be available to pick up
/// a return load, where it is heading, and when. The matching engine pairs Loads against
/// these (<c>MATCHING_ENGINE.md</c>).
/// <para><b>Invariant:</b> origin, destination, and availability window are required.</para>
/// </summary>
public sealed class ReturnLeg : ValueObject
{
    private ReturnLeg(Location origin, Location destination, TimeWindow availability)
    {
        Origin = origin;
        Destination = destination;
        Availability = availability;
    }

    /// <summary>Where the truck becomes empty and available (typically the trip's delivery point).</summary>
    public Location Origin { get; }

    /// <summary>Where the truck is heading back to.</summary>
    public Location Destination { get; }

    /// <summary>When the truck is available to carry a return load.</summary>
    public TimeWindow Availability { get; }

    public static ReturnLeg Create(Location origin, Location destination, TimeWindow availability)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(availability);
        Guard.Against(origin == destination, "Return leg origin and destination must differ.", "return_leg_same_endpoints");
        return new ReturnLeg(origin, destination, availability);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Origin;
        yield return Destination;
        yield return Availability;
    }
}
