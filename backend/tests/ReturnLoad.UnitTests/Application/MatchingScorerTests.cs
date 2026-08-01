using ReturnLoad.Application.UseCases.Matching;
using ReturnLoad.Domain.Loads;
using ReturnLoad.Domain.ValueObjects;

namespace ReturnLoad.UnitTests.Application;

/// <summary>
/// The M5 ranking stage (MATCHING_ENGINE.md §3, §7). Deterministic fixtures lock the order:
/// pickup proximity dominates, fuller trucks rank higher, and scoring degrades gracefully with
/// no driver location. Score is always 0–100 and stars 1–5.
/// </summary>
public sealed class MatchingScorerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static readonly MatchingOptions Options = new();

    // Madurai — the driver's current location in the worked example.
    private const double MaduraiLat = 9.9252;
    private const double MaduraiLng = 78.1198;

    private static Load LoadFrom(double lat, double lng, decimal weightKg, decimal? distanceKm) =>
        BuildLoad(lat, lng, weightKg, distanceKm);

    private static Load BuildLoad(double lat, double lng, decimal weightKg, decimal? distanceKm)
    {
        Load load = Load.Create(
            Guid.NewGuid(),
            Location.Create(GeoCoordinate.Create(lat, lng), "Pickup"),
            Location.Create(GeoCoordinate.Create(13.0827, 80.2707), "Chennai"),
            TimeWindow.Create(Now, Now.AddHours(6)),
            LoadRequirement.Create(CargoType.General, Weight.FromKilograms(weightKg)));
        if (distanceKm is decimal d)
        {
            load.SetRoute(d, 300);
        }

        return load;
    }

    [Fact]
    public void A_nearby_pickup_ranks_above_a_distant_one()
    {
        // Near: pickup in Madurai. Far: pickup in Coimbatore (~215 km away).
        MatchScore near = MatchingScorer.Score(LoadFrom(9.93, 78.12, 15000m, 460m), 18000m, MaduraiLat, MaduraiLng, Options);
        MatchScore far = MatchingScorer.Score(LoadFrom(11.0168, 76.9558, 15000m, 210m), 18000m, MaduraiLat, MaduraiLng, Options);

        Assert.True(near.Score > far.Score);
        Assert.True(near.Stars >= far.Stars);
        Assert.Contains("pickup", near.Reason);
    }

    [Fact]
    public void Score_is_bounded_and_stars_are_one_to_five()
    {
        MatchScore s = MatchingScorer.Score(LoadFrom(9.93, 78.12, 18000m, 600m), 18000m, MaduraiLat, MaduraiLng, Options);

        Assert.InRange(s.Score, 0, 100);
        Assert.InRange(s.Stars, 1, 5);
    }

    [Fact]
    public void Without_a_driver_location_it_still_ranks_by_other_signals()
    {
        MatchScore s = MatchingScorer.Score(LoadFrom(9.93, 78.12, 18000m, 600m), 18000m, null, null, Options);

        Assert.True(s.Score > 0);
        Assert.Contains("set your location", s.Reason);
    }

    [Fact]
    public void A_fuller_truck_ranks_above_an_underused_one()
    {
        Load load = LoadFrom(9.93, 78.12, 9000m, 300m);
        MatchScore full = MatchingScorer.Score(load, 10000m, MaduraiLat, MaduraiLng, Options);   // 90% utilised
        MatchScore underused = MatchingScorer.Score(load, 30000m, MaduraiLat, MaduraiLng, Options); // 30% utilised

        Assert.True(full.Score > underused.Score);
    }
}
