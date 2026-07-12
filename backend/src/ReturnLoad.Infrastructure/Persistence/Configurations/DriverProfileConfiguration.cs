using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReturnLoad.Application.Abstractions.Security;
using ReturnLoad.Domain.Identity;

namespace ReturnLoad.Infrastructure.Persistence.Configurations;

/// <summary>
/// Persists a driver. The <b>Aadhaar</b> value object is encrypted at rest via
/// <see cref="IFieldEncryptor"/> (ADR-0015, Trust &amp; Safety §1) — the plaintext never
/// reaches the database. This config takes a dependency, so it is applied explicitly in
/// <c>OnModelCreating</c> (not by assembly scan).
/// </summary>
public sealed class DriverProfileConfiguration : AggregateConfiguration<DriverProfile>
{
    private readonly IFieldEncryptor _encryptor;

    public DriverProfileConfiguration(IFieldEncryptor encryptor) => _encryptor = encryptor;

    protected override void ConfigureAggregate(EntityTypeBuilder<DriverProfile> builder)
    {
        builder.ToTable("Drivers");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.UserProfileId).IsRequired();
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(d => d.CreatedAtUtc).IsRequired();

        builder.Property(d => d.Licence)
            .HasConversion(l => l.Value, s => DrivingLicenceNumber.Create(s))
            .HasColumnName("LicenceNumber").HasMaxLength(24).IsRequired();

        // Aadhaar: encrypted at rest. Encrypt on the way in, decrypt + revalidate on the way out.
        builder.Property(d => d.Aadhaar)
            .HasConversion(
                a => _encryptor.Encrypt(a!.Value),
                s => AadhaarNumber.Create(_encryptor.Decrypt(s)))
            .HasColumnName("AadhaarEncrypted").HasMaxLength(512);

        builder.HasOne<UserProfile>().WithMany().HasForeignKey(d => d.UserProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(d => d.UserProfileId);
        builder.HasIndex(d => d.Licence).IsUnique();
        builder.HasIndex(d => d.Status);
    }
}
