using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReturnLoad.Domain.Fleet;
using ReturnLoad.Domain.Identity;
using ReturnLoad.Domain.Trips;

namespace ReturnLoad.Infrastructure.Persistence.Configurations;

public sealed class TripConfiguration : AggregateConfiguration<Trip>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<Trip> builder)
    {
        builder.ToTable("Trips");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.CarrierId).IsRequired();
        builder.Property(t => t.VehicleId).IsRequired();
        builder.Property(t => t.DriverProfileId).IsRequired();
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(t => t.CreatedAtUtc).IsRequired();
        builder.Property(t => t.StartedAtUtc);
        builder.Property(t => t.CompletedAtUtc);

        builder.OwnsOne(t => t.Origin, o => OwnedConfig.Location(o, "Origin"));
        builder.OwnsOne(t => t.Destination, d => OwnedConfig.Location(d, "Destination"));
        builder.OwnsOne(t => t.ReturnLeg, leg =>
        {
            leg.OwnsOne(l => l.Origin, o => OwnedConfig.Location(o, "ReturnOrigin"));
            leg.OwnsOne(l => l.Destination, d => OwnedConfig.Location(d, "ReturnDestination"));
            leg.OwnsOne(l => l.Availability, w => OwnedConfig.TimeWindow(w, "ReturnAvail"));
        });

        builder.HasOne<Carrier>().WithMany().HasForeignKey(t => t.CarrierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Vehicle>().WithMany().HasForeignKey(t => t.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DriverProfile>().WithMany().HasForeignKey(t => t.DriverProfileId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.Status);
        builder.HasIndex(t => new { t.DriverProfileId, t.VehicleId }); // driver + vehicle assignment
    }
}
