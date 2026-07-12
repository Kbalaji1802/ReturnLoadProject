using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReturnLoad.Domain.Fleet;
using ReturnLoad.Domain.Identity;
using ReturnLoad.Domain.ValueObjects;

namespace ReturnLoad.Infrastructure.Persistence.Configurations;

public sealed class VehicleConfiguration : AggregateConfiguration<Vehicle>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("Vehicles");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.CarrierId).IsRequired();
        builder.Property(v => v.Type).HasConversion<string>().HasMaxLength(24);
        builder.Property(v => v.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(v => v.CreatedAtUtc).IsRequired();

        builder.Property(v => v.Registration)
            .HasConversion(r => r.Value, s => VehicleRegistrationNumber.Create(s))
            .HasColumnName("RegistrationNumber").HasMaxLength(16).IsRequired();

        builder.OwnsOne(v => v.Capacity, capacity =>
        {
            capacity.Property(c => c.MaxPayload)
                .HasConversion(w => w.Kilograms, kg => Weight.FromKilograms(kg))
                .HasColumnName("MaxPayloadKg").IsRequired();
            capacity.Property(c => c.VolumeCubicMetres).HasColumnName("VolumeCubicMetres");
        });

        builder.HasOne<Carrier>().WithMany().HasForeignKey(v => v.CarrierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(v => v.Registration).IsUnique();
        builder.HasIndex(v => v.Status);
    }
}
