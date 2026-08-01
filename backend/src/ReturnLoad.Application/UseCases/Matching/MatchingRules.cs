using ReturnLoad.Domain.Fleet;
using ReturnLoad.Domain.Loads;

namespace ReturnLoad.Application.UseCases.Matching;

/// <summary>
/// The deterministic MVP matching rules (<c>MATCHING_ENGINE.md</c> §2). Pure and side-effect
/// free so each hard filter is explicitly unit-tested with pass and fail cases (01_PROJECT_RULES.md
/// §4 — the engine carries the highest coverage). Geo/time filters (3,4,5) arrive with PostGIS;
/// this covers filters 1 (vehicle type ↔ cargo) and 2 (payload capacity). Verification filters
/// (6,7,8) are enforced by the service against entity status.
/// </summary>
public static class MatchingRules
{
    /// <summary>
    /// Which vehicle types can carry which cargo (filter 1). A rule, not data on the load, so
    /// Loads and Fleet stay decoupled (see <see cref="LoadRequirement"/>). Tunable — moves to
    /// configuration/per-lane rules later; correctness and explainability first.
    /// </summary>
    private static readonly IReadOnlyDictionary<CargoType, VehicleType[]> CompatibleVehicles =
        new Dictionary<CargoType, VehicleType[]>
        {
            [CargoType.General] = [VehicleType.OpenBody, VehicleType.ClosedContainer, VehicleType.Flatbed, VehicleType.LightCommercial, VehicleType.Trailer],
            [CargoType.Perishable] = [VehicleType.Reefer, VehicleType.ClosedContainer],
            [CargoType.Refrigerated] = [VehicleType.Reefer],
            [CargoType.Fragile] = [VehicleType.ClosedContainer, VehicleType.LightCommercial],
            [CargoType.Hazardous] = [VehicleType.Tanker, VehicleType.ClosedContainer],
            [CargoType.Construction] = [VehicleType.Flatbed, VehicleType.Tipper, VehicleType.OpenBody, VehicleType.Trailer],
            [CargoType.Liquid] = [VehicleType.Tanker],
        };

    /// <summary>Filter 1: does the vehicle type support this cargo? <c>Other</c> cargo accepts any type.</summary>
    public static bool IsVehicleTypeCompatible(CargoType cargoType, VehicleType vehicleType)
    {
        if (cargoType == CargoType.Other)
        {
            return true;
        }

        return CompatibleVehicles.TryGetValue(cargoType, out VehicleType[]? types) && Array.IndexOf(types, vehicleType) >= 0;
    }

    /// <summary>
    /// Filters 1 + 2 combined for a single vehicle: the type supports the cargo and the payload
    /// capacity covers the load weight. The vehicle's <b>verified</b> status (filter 8) and the
    /// driver's (filter 7) are checked by the service before this is called.
    /// </summary>
    public static bool IsEligible(Load load, Vehicle vehicle)
    {
        ArgumentNullException.ThrowIfNull(load);
        ArgumentNullException.ThrowIfNull(vehicle);

        return IsVehicleTypeCompatible(load.Requirement.CargoType, vehicle.Type)
            && vehicle.Capacity.CanCarry(load.Requirement.Weight);
    }
}
