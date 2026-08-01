using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReturnLoad.Domain.Bookings;
using ReturnLoad.Domain.Fleet;
using ReturnLoad.Domain.Identity;
using ReturnLoad.Domain.Loads;

namespace ReturnLoad.Infrastructure.Persistence.Configurations;

public sealed class BookingRequestConfiguration : AggregateConfiguration<BookingRequest>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<BookingRequest> builder)
    {
        builder.ToTable("BookingRequests");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.LoadId).IsRequired();
        builder.Property(b => b.DriverProfileId).IsRequired();
        builder.Property(b => b.CarrierId).IsRequired();
        builder.Property(b => b.VehicleId).IsRequired();
        builder.Property(b => b.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(b => b.CreatedAtUtc).IsRequired();
        builder.Property(b => b.DecidedAtUtc);

        builder.HasOne<Load>().WithMany().HasForeignKey(b => b.LoadId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DriverProfile>().WithMany().HasForeignKey(b => b.DriverProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Vehicle>().WithMany().HasForeignKey(b => b.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Carrier>().WithMany().HasForeignKey(b => b.CarrierId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => new { b.LoadId, b.Status });
        builder.HasIndex(b => new { b.DriverProfileId, b.Status });
    }
}
