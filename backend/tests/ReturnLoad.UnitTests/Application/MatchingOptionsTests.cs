using ReturnLoad.Application.UseCases.Matching;
using ReturnLoad.Domain.Loads;

namespace ReturnLoad.UnitTests.Application;

/// <summary>
/// The Part 2 pickup-radius resolution: each pickup area type maps to its configured km. Values are
/// config-driven (defaults asserted here); the hard filter that uses them is covered end-to-end in
/// the integration suite.
/// </summary>
public sealed class MatchingOptionsTests
{
    [Theory]
    [InlineData(AreaType.Urban, 5)]
    [InlineData(AreaType.Suburban, 10)]
    [InlineData(AreaType.Highway, 25)]
    public void ResolvePickupRadiusKm_maps_each_area_type_to_its_configured_radius(AreaType area, double expectedKm)
    {
        MatchingOptions options = new();
        Assert.Equal(expectedKm, options.ResolvePickupRadiusKm(area));
    }

    [Fact]
    public void ResolvePickupRadiusKm_honours_overridden_config_values()
    {
        MatchingOptions options = new() { UrbanRadiusKm = 3, SuburbanRadiusKm = 12, HighwayRadiusKm = 40 };

        Assert.Equal(3, options.ResolvePickupRadiusKm(AreaType.Urban));
        Assert.Equal(12, options.ResolvePickupRadiusKm(AreaType.Suburban));
        Assert.Equal(40, options.ResolvePickupRadiusKm(AreaType.Highway));
    }
}
