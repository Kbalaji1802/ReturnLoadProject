using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReturnLoad.Domain.Administration;
using ReturnLoad.Domain.Identity;
using ReturnLoad.Domain.Reviews;
using ReturnLoad.Domain.Tracking;
using ReturnLoad.Domain.Trips;
using ReturnLoad.Domain.ValueObjects;

namespace ReturnLoad.Infrastructure.Persistence.Configurations;

public sealed class TrackingEventConfiguration : AggregateConfiguration<TrackingEvent>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<TrackingEvent> builder)
    {
        builder.ToTable("TrackingEvents");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.TripId).IsRequired();
        builder.Property(e => e.Type).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(e => e.CapturedAtUtc).IsRequired();
        builder.Property(e => e.RecordedAtUtc).IsRequired();

        builder.OwnsOne(e => e.Point, point =>
        {
            point.Property(p => p.Coordinate)
                .HasConversion(DomainConverters.GeoCoordinate)
                .HasColumnName("Coordinate").HasMaxLength(64).IsRequired();
            point.Property(p => p.SpeedKph).HasColumnName("SpeedKph");
            point.Property(p => p.HeadingDegrees).HasColumnName("HeadingDegrees");
            point.Property(p => p.AccuracyMetres).HasColumnName("AccuracyMetres");
        });

        builder.HasOne<Trip>().WithMany().HasForeignKey(e => e.TripId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.TripId, e.CapturedAtUtc }); // trip path, chronological
    }
}

public sealed class ReviewConfiguration : AggregateConfiguration<Review>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("Reviews");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.TripId).IsRequired();
        builder.Property(r => r.AuthorUserProfileId).IsRequired();
        builder.Property(r => r.SubjectUserProfileId).IsRequired();
        builder.Property(r => r.Comment).HasMaxLength(Review.MaxCommentLength);
        builder.Property(r => r.CreatedAtUtc).IsRequired();

        builder.Property(r => r.Rating)
            .HasConversion(rating => rating.Stars, stars => Rating.Of(stars))
            .HasColumnName("RatingStars").IsRequired();

        builder.HasOne<Trip>().WithMany().HasForeignKey(r => r.TripId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserProfile>().WithMany().HasForeignKey(r => r.AuthorUserProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserProfile>().WithMany().HasForeignKey(r => r.SubjectUserProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => r.TripId);
        builder.HasIndex(r => r.SubjectUserProfileId);
    }
}

public sealed class NotificationConfiguration : AggregateConfiguration<Notification>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.RecipientUserProfileId).IsRequired();
        builder.Property(n => n.Channel).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(n => n.Subject).HasMaxLength(200);
        builder.Property(n => n.Body).HasMaxLength(2000).IsRequired();
        builder.Property(n => n.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(n => n.FailureReason).HasMaxLength(500);
        builder.Property(n => n.CreatedAtUtc).IsRequired();
        builder.Property(n => n.SentAtUtc);

        builder.HasOne<UserProfile>().WithMany().HasForeignKey(n => n.RecipientUserProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(n => new { n.RecipientUserProfileId, n.Status });
    }
}

/// <summary>
/// Append-only audit trail (01_PROJECT_RULES.md §6). No soft delete/mutation is expected;
/// the base shadow columns are harmless. Immutable by design at the domain level.
/// </summary>
public sealed class AuditLogConfiguration : AggregateConfiguration<AuditLog>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Actor).HasMaxLength(256).IsRequired();
        builder.Property(a => a.Action).HasMaxLength(128).IsRequired();
        builder.Property(a => a.SubjectType).HasMaxLength(128);
        builder.Property(a => a.SubjectId);
        builder.Property(a => a.Reason).HasMaxLength(1000);
        builder.Property(a => a.BeforeState).HasMaxLength(4000);
        builder.Property(a => a.AfterState).HasMaxLength(4000);
        builder.Property(a => a.CorrelationId).HasMaxLength(128);
        builder.Property(a => a.OccurredAtUtc).IsRequired();

        builder.HasIndex(a => new { a.SubjectType, a.SubjectId });
        builder.HasIndex(a => a.OccurredAtUtc);
    }
}
