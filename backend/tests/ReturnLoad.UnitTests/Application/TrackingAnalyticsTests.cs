using ReturnLoad.Application.UseCases.Tracking;
using ReturnLoad.Domain.Tracking;
using ReturnLoad.Domain.ValueObjects;

namespace ReturnLoad.UnitTests.Application;

/// <summary>Pure trip analytics over a tracking breadcrumb (M6). Deterministic fixtures.</summary>
public sealed class TrackingAnalyticsTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    private static TrackingEvent Point(double lat, double lng, double? speedKph, DateTimeOffset capturedAt) =>
        TrackingEvent.Capture(
            Guid.NewGuid(), TrackingEventType.LocationPing,
            LocationPoint.Create(GeoCoordinate.Create(lat, lng), speedKph),
            capturedAt, capturedAt);

    [Fact]
    public void Empty_breadcrumb_summarises_to_zero()
    {
        TripTrackingSummary summary = TrackingAnalytics.Summarize([]);

        Assert.Equal(0, summary.PointCount);
        Assert.Equal(0, summary.DistanceKm);
        Assert.Equal(0, summary.DurationMinutes);
    }

    [Fact]
    public void Summarises_distance_duration_and_speeds()
    {
        // ~1 km north over 2 minutes; speeds 30 then 50 kph.
        List<TrackingEvent> points =
        [
            Point(13.0000, 80.0000, 30, Start),
            Point(13.0090, 80.0000, 50, Start.AddMinutes(2)),
        ];

        TripTrackingSummary summary = TrackingAnalytics.Summarize(points);

        Assert.Equal(2, summary.PointCount);
        Assert.Equal(2, summary.DurationMinutes);
        Assert.Equal(50, summary.MaxSpeedKph);
        Assert.InRange(summary.DistanceKm, 0.9, 1.1);
        Assert.InRange(summary.AvgSpeedKph, 25, 35);
        Assert.Equal(0, summary.IdleMinutes); // moving at ~30 kph, not idle
    }

    [Fact]
    public void Counts_idle_time_when_barely_moving()
    {
        // Two points ~11 m apart over 10 minutes => ~0.07 kph => idle.
        List<TrackingEvent> points =
        [
            Point(13.0000, 80.0000, 0, Start),
            Point(13.0001, 80.0000, 0, Start.AddMinutes(10)),
        ];

        TripTrackingSummary summary = TrackingAnalytics.Summarize(points);

        Assert.Equal(10, summary.IdleMinutes);
    }
}
