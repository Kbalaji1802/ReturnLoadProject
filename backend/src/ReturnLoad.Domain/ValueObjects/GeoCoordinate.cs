using ReturnLoad.Domain.Common;

namespace ReturnLoad.Domain.ValueObjects;

/// <summary>
/// A WGS-84 geographic coordinate. Latitude ∈ [-90, 90], longitude ∈ [-180, 180]. Used for
/// pickup/delivery points, trip endpoints, and GPS breadcrumbs (see
/// <c>MATCHING_ENGINE.md</c>, <c>OFFLINE_STRATEGY.md</c>). Corridor/routing geometry is a
/// PostGIS concern in the infrastructure layer, not modelled here.
/// </summary>
public sealed class GeoCoordinate : ValueObject
{
    private GeoCoordinate(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    public double Latitude { get; }

    public double Longitude { get; }

    public static GeoCoordinate Create(double latitude, double longitude)
    {
        Guard.Against(latitude is < -90 or > 90, "Latitude must be between -90 and 90.", "latitude_out_of_range");
        Guard.Against(longitude is < -180 or > 180, "Longitude must be between -180 and 180.", "longitude_out_of_range");
        return new GeoCoordinate(latitude, longitude);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Latitude;
        yield return Longitude;
    }

    public override string ToString() => $"({Latitude:0.######}, {Longitude:0.######})";
}
