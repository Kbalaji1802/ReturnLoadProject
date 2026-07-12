using ReturnLoad.Domain.Administration;
using ReturnLoad.Domain.Common;
using ReturnLoad.Domain.Reviews;
using ReturnLoad.Domain.Tracking;
using ReturnLoad.Domain.ValueObjects;

namespace ReturnLoad.UnitTests.Domain;

public sealed class TrackingReviewsAdminTests
{
    private static readonly DateTimeOffset Captured = new(2026, 7, 12, 8, 0, 0, TimeSpan.Zero);

    private static LocationPoint Point() => LocationPoint.Create(GeoCoordinate.Create(11.5, 78.1), speedKph: 60);

    [Fact]
    public void TrackingEvent_preserves_captured_time_and_rejects_impossible_recording()
    {
        TrackingEvent evt = TrackingEvent.Capture(Guid.NewGuid(), TrackingEventType.LocationPing, Point(), Captured, Captured.AddMinutes(30));
        Assert.Equal(Captured, evt.CapturedAtUtc);
        Assert.True(evt.RecordedAtUtc > evt.CapturedAtUtc);

        // Recorded-at cannot precede captured-at.
        Assert.Throws<DomainException>(() =>
            TrackingEvent.Capture(Guid.NewGuid(), TrackingEventType.LocationPing, Point(), Captured, Captured.AddMinutes(-1)));
    }

    [Fact]
    public void LocationPoint_rejects_bad_readings()
    {
        Assert.Throws<DomainException>(() => LocationPoint.Create(GeoCoordinate.Create(0, 0), speedKph: -5));
        Assert.Throws<DomainException>(() => LocationPoint.Create(GeoCoordinate.Create(0, 0), headingDegrees: 360));
    }

    [Fact]
    public void Rating_is_bounded_1_to_5()
    {
        Assert.Equal(5, Rating.Of(5).Stars);
        Assert.Throws<DomainException>(() => Rating.Of(0));
        Assert.Throws<DomainException>(() => Rating.Of(6));
    }

    [Fact]
    public void Review_cannot_be_left_about_yourself()
    {
        Guid self = Guid.NewGuid();
        Assert.Throws<DomainException>(() => Review.Leave(Guid.NewGuid(), self, self, Rating.Of(4)));
    }

    [Fact]
    public void Review_captures_rating_and_trimmed_comment()
    {
        Review review = Review.Leave(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Rating.Of(4), "  Great driver  ");
        Assert.Equal(4, review.Rating.Stars);
        Assert.Equal("Great driver", review.Comment);
    }

    [Fact]
    public void Notification_follows_its_lifecycle()
    {
        Notification notification = Notification.Queue(Guid.NewGuid(), NotificationChannel.Sms, "Trip", "Your trip has started.");
        Assert.Equal(NotificationStatus.Pending, notification.Status);

        notification.MarkSent();
        Assert.Equal(NotificationStatus.Sent, notification.Status);
        Assert.NotNull(notification.SentAtUtc);

        notification.MarkRead();
        Assert.Equal(NotificationStatus.Read, notification.Status);
        Assert.Throws<DomainException>(notification.MarkSent); // cannot re-send
    }

    [Fact]
    public void AuditLog_requires_actor_and_action_and_is_immutable_record()
    {
        AuditLog entry = AuditLog.Record("ops@returnload", "DriverBlocked", "DriverProfile", Guid.NewGuid(), reason: "Fraud");
        Assert.Equal("DriverBlocked", entry.Action);
        Assert.Throws<DomainException>(() => AuditLog.Record("  ", "X"));
        Assert.Throws<DomainException>(() => AuditLog.Record("ops", "  "));

        // AuditLog exposes no mutators — append-only by design (compile-time guarantee).
        Assert.DoesNotContain(
            typeof(AuditLog).GetMethods(),
            m => m.Name.StartsWith("set_", StringComparison.Ordinal) && m.IsPublic);
    }
}
