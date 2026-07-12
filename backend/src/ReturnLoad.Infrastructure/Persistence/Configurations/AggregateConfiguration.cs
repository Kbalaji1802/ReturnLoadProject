using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ReturnLoad.Infrastructure.Persistence.Configurations;

/// <summary>Shadow-property names shared by the persistence layer.</summary>
internal static class PersistenceConventions
{
    public const string IsDeleted = "IsDeleted";
    public const string CreatedBy = "CreatedBy";
    public const string UpdatedBy = "UpdatedBy";
    public const string UpdatedAtUtc = "UpdatedAtUtc";
    public const string Version = "Version";
}

/// <summary>
/// Base configuration applied to every aggregate root. Adds — as <b>shadow properties</b>
/// so the domain stays persistence-ignorant — soft delete (<c>IsDeleted</c> + a global
/// query filter), audit fields (<c>CreatedBy</c>/<c>UpdatedBy</c>/<c>UpdatedAtUtc</c>;
/// created-at is already a domain property), and an application-managed
/// <c>Version</c> concurrency token (portable across providers — see ADR-0015). Domain
/// events are ignored. Concrete configs implement <see cref="ConfigureAggregate"/>.
/// </summary>
public abstract class AggregateConfiguration<T> : IEntityTypeConfiguration<T>
    where T : class
{
    public void Configure(EntityTypeBuilder<T> builder)
    {
        // Domain events are behaviour, not state — never persisted.
        builder.Ignore("DomainEvents");

        builder.Property<bool>(PersistenceConventions.IsDeleted).HasDefaultValue(false);
        builder.Property<string>(PersistenceConventions.CreatedBy).HasMaxLength(256);
        builder.Property<string>(PersistenceConventions.UpdatedBy).HasMaxLength(256);
        builder.Property<DateTimeOffset?>(PersistenceConventions.UpdatedAtUtc);

        // Optimistic concurrency: EF sends the original Version in the UPDATE's WHERE and
        // the interceptor increments it; a lost update => DbUpdateConcurrencyException.
        builder.Property<long>(PersistenceConventions.Version).IsConcurrencyToken();

        // Soft-deleted rows are invisible to normal queries.
        builder.HasQueryFilter(e => !EF.Property<bool>(e, PersistenceConventions.IsDeleted));

        ConfigureAggregate(builder);
    }

    protected abstract void ConfigureAggregate(EntityTypeBuilder<T> builder);
}
