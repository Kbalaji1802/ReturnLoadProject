using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReturnLoad.Domain.Identity;
using ReturnLoad.Domain.Loads;
using ReturnLoad.Domain.ValueObjects;

namespace ReturnLoad.Infrastructure.Persistence.Configurations;

public sealed class LoadConfiguration : AggregateConfiguration<Load>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<Load> builder)
    {
        builder.ToTable("Loads");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.ShipperId).IsRequired();
        builder.Property(l => l.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(l => l.CreatedAtUtc).IsRequired();

        builder.OwnsOne(l => l.Origin, o =>
        {
            OwnedConfig.Location(o, "Origin");
            o.HasIndex(x => x.Coordinate); // pickup index
        });
        builder.OwnsOne(l => l.Destination, d =>
        {
            OwnedConfig.Location(d, "Destination");
            d.HasIndex(x => x.Coordinate); // drop index
        });
        builder.OwnsOne(l => l.PickupWindow, w => OwnedConfig.TimeWindow(w, "Pickup"));

        builder.OwnsOne(l => l.Requirement, req =>
        {
            req.Property(r => r.CargoType).HasConversion<string>().HasMaxLength(24).IsRequired();
            req.Property(r => r.Weight)
                .HasConversion(x => x.Kilograms, kg => Weight.FromKilograms(kg))
                .HasColumnName("WeightKg").IsRequired();
            req.Property(r => r.VolumeCubicMetres).HasColumnName("RequiredVolumeCubicMetres");
        });

        builder.OwnsOne(l => l.OfferedPrice, price =>
        {
            price.Property(m => m.Amount).HasColumnName("OfferedPriceAmount").HasPrecision(18, 2);
            price.Property(m => m.Currency).HasColumnName("OfferedPriceCurrency").HasMaxLength(3);
        });

        builder.HasOne<UserProfile>().WithMany().HasForeignKey(l => l.ShipperId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(l => l.Status);
        builder.HasIndex(l => new { l.ShipperId, l.Status });
    }
}
