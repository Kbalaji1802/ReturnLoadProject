using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReturnLoad.Domain.ValueObjects;

namespace ReturnLoad.Infrastructure.Persistence.Configurations;

/// <summary>Reusable column mappings for owned value objects shared by several aggregates.</summary>
internal static class OwnedConfig
{
    public static void Location<TOwner>(OwnedNavigationBuilder<TOwner, Location> builder, string prefix)
        where TOwner : class
    {
        builder.Property(l => l.Coordinate)
            .HasConversion(DomainConverters.GeoCoordinate)
            .HasColumnName(prefix + "Coordinate").HasMaxLength(64).IsRequired();
        builder.Property(l => l.Address).HasColumnName(prefix + "Address").HasMaxLength(300);
    }

    public static void TimeWindow<TOwner>(OwnedNavigationBuilder<TOwner, TimeWindow> builder, string prefix)
        where TOwner : class
    {
        builder.Property(w => w.Start).HasColumnName(prefix + "Start").IsRequired();
        builder.Property(w => w.End).HasColumnName(prefix + "End").IsRequired();
    }
}
