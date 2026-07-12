using ReturnLoad.Domain.Common;
using ReturnLoad.Domain.ValueObjects;

namespace ReturnLoad.Domain.Loads;

/// <summary>
/// What a load needs from a vehicle: the cargo type, its weight, and optional volume. The
/// matching engine uses these against a vehicle's type and capacity (filters 1–2). Kept as
/// cargo characteristics only — which vehicle types satisfy which cargo is a matching rule,
/// not data stored on the load (keeps Loads decoupled from Fleet).
/// <para><b>Invariant:</b> weight must be positive (via <see cref="Weight"/>); volume, if
/// present, must be positive.</para>
/// </summary>
public sealed class LoadRequirement : ValueObject
{
    private LoadRequirement(CargoType cargoType, Weight weight, decimal? volumeCubicMetres)
    {
        CargoType = cargoType;
        Weight = weight;
        VolumeCubicMetres = volumeCubicMetres;
    }

    public CargoType CargoType { get; }

    public Weight Weight { get; }

    public decimal? VolumeCubicMetres { get; }

    public static LoadRequirement Create(CargoType cargoType, Weight weight, decimal? volumeCubicMetres = null)
    {
        ArgumentNullException.ThrowIfNull(weight); // Weight guarantees > 0
        if (volumeCubicMetres.HasValue)
        {
            Guard.AgainstNegativeOrZero(volumeCubicMetres.Value, "Volume", "load_volume_must_be_positive");
        }

        return new LoadRequirement(cargoType, weight, volumeCubicMetres);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return CargoType;
        yield return Weight;
        yield return VolumeCubicMetres;
    }
}
