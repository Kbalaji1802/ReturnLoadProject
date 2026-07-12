using Microsoft.EntityFrameworkCore;

namespace ReturnLoad.Infrastructure.Persistence;

/// <summary>
/// The Entity Framework Core unit of work for ReturnLoad.
/// <para>
/// It intentionally declares NO <see cref="DbSet{TEntity}"/> yet — the foundation
/// has no business tables and no migrations (that is task T-012). Entity
/// configurations are auto-discovered from this assembly, so the context needs no
/// changes when the first entities are added.
/// </para>
/// </summary>
public sealed class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Applies every IEntityTypeConfiguration<T> found in this assembly. Safe
        // and inert while there are none.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
