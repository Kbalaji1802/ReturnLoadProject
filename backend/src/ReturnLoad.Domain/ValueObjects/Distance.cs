using ReturnLoad.Domain.Common;

namespace ReturnLoad.Domain.ValueObjects;

/// <summary>
/// A distance in kilometres — the market's distance unit (ADR-0004). Non-negative
/// (a distance of zero is valid; a negative distance is not).
/// </summary>
public sealed class Distance : ValueObject
{
    private Distance(decimal kilometres) => Kilometres = kilometres;

    public decimal Kilometres { get; }

    public static Distance Zero { get; } = new(0m);

    public static Distance FromKilometres(decimal kilometres)
    {
        Guard.AgainstNegative(kilometres, "Distance", "distance_negative");
        return new Distance(kilometres);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Kilometres;
    }

    public override string ToString() => $"{Kilometres:0.##} km";
}
