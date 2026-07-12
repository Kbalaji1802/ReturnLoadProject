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
