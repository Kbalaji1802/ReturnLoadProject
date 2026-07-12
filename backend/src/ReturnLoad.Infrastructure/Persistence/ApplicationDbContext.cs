using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ReturnLoad.Application.Abstractions.Persistence;
using ReturnLoad.Application.Abstractions.Security;
using ReturnLoad.Domain.Administration;
using ReturnLoad.Domain.Documents;
using ReturnLoad.Domain.Fleet;
using ReturnLoad.Domain.Identity;
using ReturnLoad.Domain.Loads;
using ReturnLoad.Domain.Reviews;
using ReturnLoad.Domain.Tracking;
using ReturnLoad.Domain.Trips;
using ReturnLoad.Infrastructure.Identity;
using ReturnLoad.Infrastructure.Persistence.Configurations;

namespace ReturnLoad.Infrastructure.Persistence;

/// <summary>
/// The EF Core unit of work. Extends <see cref="IdentityDbContext{TUser,TRole,TKey}"/> for
/// authentication (M2) and maps the M3 domain aggregates (M3.5). Entity configurations are
/// auto-discovered; the encryptor-dependent driver config is applied explicitly.
/// Cross-cutting conventions (soft delete, audit, concurrency) are applied by
/// <see cref="AuditableSoftDeleteInterceptor"/> + <see cref="AggregateConfiguration{T}"/>.
/// </summary>
public sealed class ApplicationDbContext
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>, IUnitOfWork
{
    private readonly IFieldEncryptor _encryptor;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IFieldEncryptor encryptor)
        : base(options) => _encryptor = encryptor;

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // ---- Domain aggregates (M3.5) ---------------------------------------------
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<Carrier> Carriers => Set<Carrier>();
    public DbSet<DriverProfile> Drivers => Set<DriverProfile>();
    public DbSet<Dispatcher> Dispatchers => Set<Dispatcher>();
    public DbSet<Association> Associations => Set<Association>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Load> Loads => Set<Load>();
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<TrackingEvent> TrackingEvents => Set<TrackingEvent>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.AddInterceptors(new AuditableSoftDeleteInterceptor());
    }

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

        // Parameterless-ctor configurations (all aggregates except the encryptor-dependent one).
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Driver config needs the field encryptor (Aadhaar at rest) — applied explicitly.
        modelBuilder.ApplyConfiguration(new DriverProfileConfiguration(_encryptor));
    }
}
