using ReturnLoad.Domain.Common;
using ReturnLoad.Domain.Loads;
using ReturnLoad.Domain.ValueObjects;

namespace ReturnLoad.UnitTests.Domain;

/// <summary>The platform-computed route metrics on a load (M4.3 Step 1).</summary>
public sealed class LoadRouteMetricsTests
{
    private static Load NewLoad() => Load.Create(
        Guid.NewGuid(),
        Location.Create(GeoCoordinate.Create(13.0827, 80.2707), "Chennai"),
        Location.Create(GeoCoordinate.Create(11.0168, 76.9558), "Coimbatore"),
        TimeWindow.Create(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(6)),
        LoadRequirement.Create(CargoType.General, Weight.FromKilograms(5000m)));

    [Fact]
    public void SetRoute_records_distance_and_duration()
    {
        Load load = NewLoad();

        load.SetRoute(497.50m, 540);

        Assert.Equal(497.50m, load.DistanceKm);
        Assert.Equal(540, load.EstimatedDurationMinutes);
    }

    [Fact]
    public void A_new_load_has_no_route_metrics_until_computed()
    {
        Load load = NewLoad();

        Assert.Null(load.DistanceKm);
        Assert.Null(load.EstimatedDurationMinutes);
    }

    [Fact]
    public void SetRoute_rejects_a_negative_distance()
    {
        Load load = NewLoad();

        Assert.Throws<DomainException>(() => load.SetRoute(-1m, 60));
    }
}
