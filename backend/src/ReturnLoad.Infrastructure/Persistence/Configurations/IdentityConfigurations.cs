using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReturnLoad.Domain.Identity;
using ReturnLoad.Domain.ValueObjects;

namespace ReturnLoad.Infrastructure.Persistence.Configurations;

public sealed class UserProfileConfiguration : AggregateConfiguration<UserProfile>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("UserProfiles");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.AuthUserId).IsRequired();
        builder.Property(u => u.FullName).HasMaxLength(200).IsRequired();
        builder.Property(u => u.PreferredLanguage).HasConversion<string>().HasMaxLength(16);
        builder.Property(u => u.CreatedAtUtc).IsRequired();

        builder.Property(u => u.Mobile)
            .HasConversion(m => m.Value, s => MobileNumber.Create(s))
            .HasColumnName("Mobile").HasMaxLength(16).IsRequired();

        builder.Property(u => u.Email)
            .HasConversion(e => e!.Value, s => EmailAddress.Create(s))
            .HasColumnName("Email").HasMaxLength(320);

        builder.HasIndex(u => u.AuthUserId).IsUnique();
        builder.HasIndex(u => u.Mobile).IsUnique();
        builder.HasIndex(u => u.Email).IsUnique();
    }
}

public sealed class CarrierConfiguration : AggregateConfiguration<Carrier>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<Carrier> builder)
    {
        builder.ToTable("Carriers");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.LegalName).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(c => c.CreatedAtUtc).IsRequired();

        builder.Property(c => c.Contact)
            .HasConversion(m => m.Value, s => MobileNumber.Create(s))
            .HasColumnName("Contact").HasMaxLength(16).IsRequired();

        builder.Property(c => c.Gst)
            .HasConversion(g => g!.Value, s => GstNumber.Create(s))
            .HasColumnName("Gst").HasMaxLength(15);

        builder.HasIndex(c => c.Status);
    }
}

public sealed class DispatcherConfiguration : AggregateConfiguration<Dispatcher>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<Dispatcher> builder)
    {
        builder.ToTable("Dispatchers");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.UserProfileId).IsRequired();
        builder.Property(d => d.CarrierId).IsRequired();
        builder.Property(d => d.IsActive).IsRequired();
        builder.Property(d => d.CreatedAtUtc).IsRequired();

        builder.HasOne<Carrier>().WithMany().HasForeignKey(d => d.CarrierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserProfile>().WithMany().HasForeignKey(d => d.UserProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(d => d.CarrierId);
    }
}

public sealed class AssociationConfiguration : AggregateConfiguration<Association>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<Association> builder)
    {
        builder.ToTable("Associations");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.CarrierId).IsRequired();
        builder.Property(a => a.MemberUserProfileId).IsRequired();
        builder.Property(a => a.Role).HasConversion<string>().HasMaxLength(24);
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(a => a.CreatedAtUtc).IsRequired();

        builder.HasOne<Carrier>().WithMany().HasForeignKey(a => a.CarrierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserProfile>().WithMany().HasForeignKey(a => a.MemberUserProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => new { a.CarrierId, a.MemberUserProfileId });
    }
}
