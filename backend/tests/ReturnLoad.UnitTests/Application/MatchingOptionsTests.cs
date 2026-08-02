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

    // ---- RC-2 Part 3: the unaccepted-load radius ladder ------------------------------------

    private static MatchingOptions WithLadder() => new()
    {
        UrbanRadiusKm = 5,
        RadiusEscalation =
        [
            new RadiusEscalationStep { AfterMinutes = 15, RadiusKm = 10 },
            new RadiusEscalationStep { AfterMinutes = 30, RadiusKm = 20 },
            new RadiusEscalationStep { AfterMinutes = 60, RadiusKm = 50 },
            new RadiusEscalationStep { AfterMinutes = 120, RadiusKm = 100 },
        ],
    };

    [Theory]
    [InlineData(0, 5)]      // fresh: the area-type radius
    [InlineData(14.9, 5)]   // just before the first rung
    [InlineData(15, 10)]    // boundary: the rung applies at exactly AfterMinutes
    [InlineData(29, 10)]
    [InlineData(30, 20)]
    [InlineData(60, 50)]
    [InlineData(119, 50)]
    [InlineData(120, 100)]
    [InlineData(600, 100)]  // past the last rung: stays at the widest
    public void An_unaccepted_load_widens_its_radius_as_it_ages(double ageMinutes, double expectedKm)
    {
        Assert.Equal(
            expectedKm,
            WithLadder().ResolvePickupRadiusKm(AreaType.Urban, TimeSpan.FromMinutes(ageMinutes)));
    }

    [Fact]
    public void Escalation_never_narrows_the_area_type_radius()
    {
        // A Highway load starts at 25km — wider than the ladder's early rungs. Applying a rung
        // literally would shrink its reach and hide it from drivers who could already see it.
        MatchingOptions options = WithLadder();
        options.HighwayRadiusKm = 25;

        Assert.Equal(25, options.ResolvePickupRadiusKm(AreaType.Highway, TimeSpan.FromMinutes(15)));
        Assert.Equal(25, options.ResolvePickupRadiusKm(AreaType.Highway, TimeSpan.FromMinutes(30)));
        Assert.Equal(50, options.ResolvePickupRadiusKm(AreaType.Highway, TimeSpan.FromMinutes(60)));
    }

    [Fact]
    public void An_empty_ladder_leaves_the_flat_area_type_radius()
    {
        MatchingOptions options = new() { RadiusEscalation = [] };

        Assert.Equal(5, options.ResolvePickupRadiusKm(AreaType.Urban, TimeSpan.FromDays(1)));
    }

    [Fact]
    public void Ladder_rungs_are_applied_in_time_order_regardless_of_config_order()
    {
        // Config is hand-edited JSON; a mis-ordered array must not change the outcome.
        MatchingOptions options = new()
        {
            UrbanRadiusKm = 5,
            RadiusEscalation =
            [
                new RadiusEscalationStep { AfterMinutes = 120, RadiusKm = 100 },
                new RadiusEscalationStep { AfterMinutes = 15, RadiusKm = 10 },
                new RadiusEscalationStep { AfterMinutes = 60, RadiusKm = 50 },
            ],
        };

        Assert.Equal(5, options.ResolvePickupRadiusKm(AreaType.Urban, TimeSpan.FromMinutes(1)));
        Assert.Equal(10, options.ResolvePickupRadiusKm(AreaType.Urban, TimeSpan.FromMinutes(20)));
        Assert.Equal(50, options.ResolvePickupRadiusKm(AreaType.Urban, TimeSpan.FromMinutes(90)));
        Assert.Equal(100, options.ResolvePickupRadiusKm(AreaType.Urban, TimeSpan.FromMinutes(200)));
    }
}
