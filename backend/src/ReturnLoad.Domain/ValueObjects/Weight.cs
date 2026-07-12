using ReturnLoad.Domain.Common;

namespace ReturnLoad.Domain.ValueObjects;

/// <summary>
/// A physical weight, stored in kilograms. Must be positive — a zero or negative weight is
/// meaningless for cargo and capacity. Comparison helpers support capacity checks
/// (e.g. a load's weight against a vehicle's available capacity).
/// </summary>
public sealed class Weight : ValueObject
{
    private Weight(decimal kilograms) => Kilograms = kilograms;

    public decimal Kilograms { get; }

    public decimal Tonnes => Kilograms / 1000m;

    public static Weight FromKilograms(decimal kilograms)
    {
        Guard.AgainstNegativeOrZero(kilograms, "Weight", "weight_must_be_positive");
        return new Weight(kilograms);
    }

    public static Weight FromTonnes(decimal tonnes) => FromKilograms(tonnes * 1000m);

    public bool IsGreaterThanOrEqualTo(Weight other) => Kilograms >= other.Kilograms;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Kilograms;
    }

    public override string ToString() => $"{Kilograms:0.##} kg";
}
