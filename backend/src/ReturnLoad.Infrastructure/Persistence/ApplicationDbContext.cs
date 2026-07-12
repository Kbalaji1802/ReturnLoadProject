using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ReturnLoad.Infrastructure.Identity;

namespace ReturnLoad.Infrastructure.Persistence;

/// <summary>
/// The Entity Framework Core unit of work for ReturnLoad. Extends
/// <see cref="IdentityDbContext{TUser,TRole,TKey}"/> so ASP.NET Core Identity's tables
/// (users, roles, claims, logins, tokens) are part of the model (M2, ADR-0013), plus the
/// platform's own <see cref="RefreshToken"/> store. Business tables arrive with their
/// modules; entity configurations are auto-discovered from this assembly.
/// </summary>
public sealed class ApplicationDbContext
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(token => token.Id);
            entity.Property(token => token.TokenHash).HasMaxLength(128).IsRequired();
            entity.HasIndex(token => token.TokenHash).IsUnique();
            entity.HasIndex(token => new { token.UserId, token.FamilyId });
            entity.Property(token => token.DeviceId).HasMaxLength(200);
            entity.Property(token => token.ReplacedByTokenHash).HasMaxLength(128);
        });

        // Applies every IEntityTypeConfiguration<T> found in this assembly.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
