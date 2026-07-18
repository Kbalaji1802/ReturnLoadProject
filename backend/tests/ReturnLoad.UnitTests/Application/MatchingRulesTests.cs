using ReturnLoad.Application.UseCases.Matching;
using ReturnLoad.Domain.Fleet;
using ReturnLoad.Domain.Loads;
using ReturnLoad.Domain.ValueObjects;

namespace ReturnLoad.UnitTests.Application;

/// <summary>
/// The MVP matching hard filters (<c>MATCHING_ENGINE.md</c> §2, filters 1 &amp; 2). Highest-
/// coverage module (01_PROJECT_RULES.md §4): every filter has explicit pass and fail cases,
/// plus a capacity boundary.
/// </summary>
public sealed class MatchingRulesTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static Load LoadOf(CargoType cargo, decimal weightKg) => Load.Create(
        Guid.NewGuid(),
        Location.Create(GeoCoordinate.Create(13.0827, 80.2707), "Chennai"),
        Location.Create(GeoCoordinate.Create(11.0168, 76.9558), "Coimbatore"),
        TimeWindow.Create(Now, Now.AddHours(6)),
        LoadRequirement.Create(cargo, Weight.FromKilograms(weightKg)));

    private static Vehicle VehicleOf(VehicleType type, decimal payloadKg) => Vehicle.Register(
        Guid.NewGuid(), VehicleRegistrationNumber.Create("TN01AB1234"), type,
        VehicleCapacity.Create(Weight.FromKilograms(payloadKg)));

    [Theory]
    [InlineData(CargoType.Refrigerated, VehicleType.Reefer, true)]
    [InlineData(CargoType.Refrigerated, VehicleType.OpenBody, false)]
    [InlineData(CargoType.Perishable, VehicleType.Reefer, true)]
    [InlineData(CargoType.Liquid, VehicleType.Tanker, true)]
    [InlineData(CargoType.Liquid, VehicleType.Flatbed, false)]
    [InlineData(CargoType.Construction, VehicleType.Tipper, true)]
    [InlineData(CargoType.General, VehicleType.OpenBody, true)]
    [InlineData(CargoType.General, VehicleType.Tanker, false)]
    [InlineData(CargoType.Other, VehicleType.Tanker, true)] // Other accepts any type
    public void Vehicle_type_compatibility_matches_the_rules(CargoType cargo, VehicleType type, bool expected) =>
        Assert.Equal(expected, MatchingRules.IsVehicleTypeCompatible(cargo, type));

    [Fact]
    public void Eligible_when_type_matches_and_capacity_covers_weight() =>
        Assert.True(MatchingRules.IsEligible(LoadOf(CargoType.General, 12000m), VehicleOf(VehicleType.OpenBody, 12000m)));

    [Fact]
    public void Not_eligible_when_load_is_one_kg_over_capacity() =>
        Assert.False(MatchingRules.IsEligible(LoadOf(CargoType.General, 12001m), VehicleOf(VehicleType.OpenBody, 12000m)));

    [Fact]
    public void Not_eligible_when_type_incompatible_even_if_capacity_fits() =>
        Assert.False(MatchingRules.IsEligible(LoadOf(CargoType.Liquid, 1000m), VehicleOf(VehicleType.OpenBody, 12000m)));
}
