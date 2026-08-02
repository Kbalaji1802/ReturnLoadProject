using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReturnLoad.Domain.Documents;

namespace ReturnLoad.Infrastructure.Persistence.Configurations;

/// <summary>
/// Persists the single <c>Documents</c> table that serves every subject via
/// <see cref="Document.OwnerType"/> — covering "VehicleDocument" and "DriverDocument"
/// (and carrier documents) without splitting the one Document aggregate (M3 / ADR-0014).
/// Owner is polymorphic, so it is indexed (OwnerType, OwnerId) rather than FK-constrained.
/// </summary>
public sealed class DocumentConfiguration : AggregateConfiguration<Document>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("Documents");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.OwnerType).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(d => d.OwnerId).IsRequired();
        builder.Property(d => d.Type).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(d => d.StorageKey).HasMaxLength(512).IsRequired();
        builder.Property(d => d.DocumentNumber).HasMaxLength(64);
        builder.Property(d => d.IssuedOn);
        builder.Property(d => d.ExpiresOn);
        builder.Property(d => d.VerificationStatus).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(d => d.RejectionReason).HasMaxLength(500);
        builder.Property(d => d.VerifiedAtUtc);
        builder.Property(d => d.CreatedAtUtc).IsRequired();

        builder.HasIndex(d => new { d.OwnerType, d.OwnerId });
        builder.HasIndex(d => d.VerificationStatus);
        builder.HasIndex(d => d.ExpiresOn);
    }
}

/// <summary>
/// Persists the append-only Operations decision history (Part 8). A separate aggregate — like
/// <c>TrackingEvent</c>/<c>AuditLog</c> (ADR-0014) — keyed by <see cref="DocumentReview.DocumentId"/>,
/// so a rejection reason is never overwritten and every re-upload keeps its trail.
/// </summary>
public sealed class DocumentReviewConfiguration : AggregateConfiguration<DocumentReview>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<DocumentReview> builder)
    {
        builder.ToTable("DocumentReviews");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.DocumentId).IsRequired();
        builder.Property(r => r.Decision).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(r => r.Reason).HasMaxLength(500);
        builder.Property(r => r.DecidedByUserId);
        builder.Property(r => r.DecidedAtUtc).IsRequired();

        builder.HasOne<Document>().WithMany().HasForeignKey(r => r.DocumentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(r => r.DocumentId);
    }
}

/// <summary>
/// Persists which expiry reminders have already gone out (RC-2 Part 13). The unique index on
/// (DocumentId, ThresholdDays) is the idempotency guarantee: the sweep re-evaluates every document
/// on every tick, and this is what stops a driver being notified again each pass for the whole
/// window. It also makes a duplicate a database error rather than a silent double-send.
/// </summary>
public sealed class DocumentExpiryReminderConfiguration : AggregateConfiguration<DocumentExpiryReminder>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<DocumentExpiryReminder> builder)
    {
        builder.ToTable("DocumentExpiryReminders");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.DocumentId).IsRequired();
        builder.Property(r => r.ThresholdDays).IsRequired();
        builder.Property(r => r.SentAtUtc).IsRequired();

        builder.HasOne<Document>().WithMany().HasForeignKey(r => r.DocumentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(r => new { r.DocumentId, r.ThresholdDays }).IsUnique();
    }
}
