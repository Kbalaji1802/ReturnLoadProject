using ReturnLoad.Domain.Common;
using ReturnLoad.Domain.ValueObjects;

namespace ReturnLoad.Domain.Fleet;

/// <summary>
/// A vehicle's carrying capacity: maximum payload (weight) and optional load volume. Used
/// by matching filter 2 — a load is only eligible if its weight ≤ the vehicle's available
/// capacity and its dimensions fit (<c>MATCHING_ENGINE.md</c> §2).
/// <para><b>Invariant:</b> max payload must be greater than zero (enforced via
/// <see cref="Weight"/>); volume, if given, must be positive.</para>
/// </summary>
public sealed class VehicleCapacity : ValueObject
{
    private VehicleCapacity(Weight maxPayload, decimal? volumeCubicMetres)
    {
        MaxPayload = maxPayload;
        VolumeCubicMetres = volumeCubicMetres;
    }

    public Weight MaxPayload { get; }

    public decimal? VolumeCubicMetres { get; }

    public static VehicleCapacity Create(Weight maxPayload, decimal? volumeCubicMetres = null)
    {
        ArgumentNullException.ThrowIfNull(maxPayload); // Weight already guarantees > 0
        if (volumeCubicMetres.HasValue)
        {
            Guard.AgainstNegativeOrZero(volumeCubicMetres.Value, "Volume", "capacity_volume_must_be_positive");
        }

        return new VehicleCapacity(maxPayload, volumeCubicMetres);
    }

    /// <summary>Whether this vehicle can carry the given load weight.</summary>
    public bool CanCarry(Weight loadWeight)
    {
        ArgumentNullException.ThrowIfNull(loadWeight);
        return MaxPayload.IsGreaterThanOrEqualTo(loadWeight);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return MaxPayload;
        yield return VolumeCubicMetres;
    }
}
