using ReturnLoad.Domain.Common;
using ReturnLoad.Domain.ValueObjects;

namespace ReturnLoad.Domain.Tracking;

/// <summary>
/// A single GPS reading: a <see cref="GeoCoordinate"/> with optional speed, heading, and
/// accuracy. The building block of a trip's breadcrumb trail (<c>OFFLINE_STRATEGY.md</c> §4).
/// <para><b>Invariants:</b> speed/accuracy, if present, are non-negative; heading ∈ [0, 360).</para>
/// </summary>
public sealed class LocationPoint : ValueObject
{
    private LocationPoint(GeoCoordinate coordinate, double? speedKph, double? headingDegrees, double? accuracyMetres)
    {
        Coordinate = coordinate;
        SpeedKph = speedKph;
        HeadingDegrees = headingDegrees;
        AccuracyMetres = accuracyMetres;
    }

    public GeoCoordinate Coordinate { get; }

    public double? SpeedKph { get; }

    public double? HeadingDegrees { get; }

    public double? AccuracyMetres { get; }

    public static LocationPoint Create(
        GeoCoordinate coordinate,
        double? speedKph = null,
        double? headingDegrees = null,
        double? accuracyMetres = null)
    {
        ArgumentNullException.ThrowIfNull(coordinate);
        Guard.Against(speedKph is < 0, "Speed cannot be negative.", "speed_negative");
        Guard.Against(accuracyMetres is < 0, "Accuracy cannot be negative.", "accuracy_negative");
        Guard.Against(headingDegrees is < 0 or >= 360, "Heading must be in [0, 360).", "heading_out_of_range");
        return new LocationPoint(coordinate, speedKph, headingDegrees, accuracyMetres);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Coordinate;
        yield return SpeedKph;
        yield return HeadingDegrees;
        yield return AccuracyMetres;
    }
}
