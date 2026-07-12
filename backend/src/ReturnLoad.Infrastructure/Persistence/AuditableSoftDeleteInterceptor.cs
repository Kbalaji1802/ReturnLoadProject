using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ReturnLoad.Infrastructure.Persistence.Configurations;

namespace ReturnLoad.Infrastructure.Persistence;

/// <summary>
/// Enforces the cross-cutting persistence conventions on every save (ADR-0015):
/// <list type="bullet">
/// <item>bumps the app-managed <c>Version</c> concurrency token (1 on insert, +1 on update);</item>
/// <item>stamps <c>UpdatedAtUtc</c> on modification (created-at is set by the domain);</item>
/// <item>converts hard deletes into <b>soft deletes</b> (<c>IsDeleted = true</c>) so records
/// are retained for audit and hidden by the global query filter.</item>
/// </list>
/// Only entities that carry these shadow properties (the aggregates) are touched.
/// </summary>
public sealed class AuditableSoftDeleteInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void Apply(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;

        foreach (EntityEntry entry in context.ChangeTracker.Entries())
        {
            if (entry.Metadata.FindProperty(PersistenceConventions.Version) is null)
            {
                continue; // not an aggregate with our conventions (e.g. Identity/RefreshToken)
            }

            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Property(PersistenceConventions.Version).CurrentValue = 1L;
                    entry.Property(PersistenceConventions.IsDeleted).CurrentValue = false;
                    break;

                case EntityState.Modified:
                    entry.Property(PersistenceConventions.Version).CurrentValue =
                        (long)(entry.Property(PersistenceConventions.Version).CurrentValue ?? 0L) + 1L;
                    entry.Property(PersistenceConventions.UpdatedAtUtc).CurrentValue = now;
                    break;

                case EntityState.Deleted:
                    // Soft delete: retain the row, hide it via the query filter.
                    entry.State = EntityState.Modified;
                    entry.Property(PersistenceConventions.IsDeleted).CurrentValue = true;
                    entry.Property(PersistenceConventions.UpdatedAtUtc).CurrentValue = now;
                    entry.Property(PersistenceConventions.Version).CurrentValue =
                        (long)(entry.Property(PersistenceConventions.Version).CurrentValue ?? 0L) + 1L;
                    break;

                default:
                    break;
            }
        }
    }
}
