using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReturnLoad.Application;
using ReturnLoad.Application.UseCases.Documents;
using ReturnLoad.Application.UseCases.Notifications;
using ReturnLoad.Application.UseCases.Onboarding;
using ReturnLoad.Domain.Documents;
using ReturnLoad.Infrastructure;
using ReturnLoad.Infrastructure.Persistence;
using ReturnLoad.Shared.Results;

namespace ReturnLoad.IntegrationTests;

/// <summary>
/// The RC-2 Part 13 expiry sweep against real persistence (SQLite). An expired compliance document
/// silently makes a driver unmatchable (MATCHING_ENGINE.md §2 filters 7–8), so these cover both
/// that the warning fires at the right moment and — critically for a job on a timer — that it
/// fires only once.
/// </summary>
public sealed class DocumentExpiryTests : IDisposable
{
    private const string TestKey = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=";
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    public DocumentExpiryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ReturnLoadDatabase"] = "Host=localhost;Database=x;Username=x;Password=x",
            ["Encryption:Key"] = TestKey,
        }).Build();

        ServiceCollection services = new();
        services.AddSingleton(config);
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(config);

        foreach (ServiceDescriptor d in services.Where(s =>
            s.ServiceType == typeof(DbContextOptions<ApplicationDbContext>)
            || s.ServiceType == typeof(ApplicationDbContext)
            || (s.ServiceType.FullName?.Contains("IDbContextOptionsConfiguration", StringComparison.Ordinal) ?? false)).ToList())
        {
            services.Remove(d);
        }

        services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite(_connection));
        services.Configure<Shared.Configuration.FileUploadOptions>(_ => { });

        _provider = services.BuildServiceProvider();
        _provider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated();
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }

    private static readonly DateOnly Today = new(2026, 6, 1);

    /// <summary>A verified driver plus a verified licence expiring in <paramref name="inDays"/>.</summary>
    private async Task<(Guid DriverProfileId, Guid DocumentId)> SeedVerifiedLicenceAsync(int inDays)
    {
        Guid authUserId = Guid.NewGuid();
        Result<DriverRegistrationResult> driver = await _provider.GetRequiredService<IDriverOnboardingService>()
            .RegisterAsync(authUserId, new RegisterDriverRequest(
                "Expiry Driver", "9800000041", null, "TN0120200004141", null, null));
        Assert.True(driver.IsSuccess);

        ApplicationDbContext db = _provider.GetRequiredService<ApplicationDbContext>();
        Document document = Document.Submit(
            DocumentOwnerType.Driver,
            driver.Value.DriverProfileId,
            DocumentType.DrivingLicence,
            "licence.pdf",
            expiresOn: Today.AddDays(inDays));

        // Verified the day before it expires. A document is always approved while still valid —
        // the domain refuses to verify an expired one — so an already-lapsed case must be seeded
        // relative to its own expiry, not to today.
        document.Verify(Today.AddDays(inDays - 1), DateTimeOffset.UtcNow);

        db.Documents.Add(document);
        await db.SaveChangesAsync();

        return (driver.Value.DriverProfileId, document.Id);
    }

    private async Task<int> UnreadCountAsync(Guid driverProfileId)
    {
        ApplicationDbContext db = _provider.GetRequiredService<ApplicationDbContext>();
        Domain.Identity.DriverProfile driver = await db.Drivers
            .AsNoTracking().FirstAsync(d => d.Id == driverProfileId);
        return await db.Notifications.CountAsync(n => n.RecipientUserProfileId == driver.UserProfileId);
    }

    [Theory]
    [InlineData(45, 0)]   // outside the widest threshold — nothing yet
    [InlineData(30, 1)]   // exactly on the 30-day rung
    [InlineData(20, 1)]   // between rungs still warns, at the nearest accurate one
    [InlineData(7, 1)]
    [InlineData(1, 1)]
    [InlineData(0, 1)]    // expires today — counts as lapsed
    [InlineData(-5, 1)]   // already expired
    public async Task Warns_only_once_the_document_is_inside_a_threshold(int expiresInDays, int expectedNotifications)
    {
        (Guid driverProfileId, _) = await SeedVerifiedLicenceAsync(expiresInDays);

        int sent = await _provider.GetRequiredService<IDocumentExpiryService>().SweepAsync(Today);

        Assert.Equal(expectedNotifications, sent);
        Assert.Equal(expectedNotifications, await UnreadCountAsync(driverProfileId));
    }

    [Fact]
    public async Task Repeated_sweeps_do_not_re_notify_the_same_threshold()
    {
        // The worker runs on a timer. Without the reminder record every tick would re-send, so a
        // driver 30 days out would be notified four times a day for a month.
        (Guid driverProfileId, _) = await SeedVerifiedLicenceAsync(7);
        IDocumentExpiryService expiry = _provider.GetRequiredService<IDocumentExpiryService>();

        Assert.Equal(1, await expiry.SweepAsync(Today));
        Assert.Equal(0, await expiry.SweepAsync(Today));
        Assert.Equal(0, await expiry.SweepAsync(Today));

        Assert.Equal(1, await UnreadCountAsync(driverProfileId));
    }

    [Fact]
    public async Task Each_threshold_fires_once_as_the_expiry_approaches()
    {
        // Walking the clock toward expiry must produce one warning per rung, then the lapsed one.
        (Guid driverProfileId, Guid documentId) = await SeedVerifiedLicenceAsync(40);
        IDocumentExpiryService expiry = _provider.GetRequiredService<IDocumentExpiryService>();
        DateOnly expiresOn = Today.AddDays(40);

        foreach (int daysOut in new[] { 45, 30, 15, 7, 1, -1 })
        {
            await expiry.SweepAsync(expiresOn.AddDays(-daysOut));
        }

        ApplicationDbContext db = _provider.GetRequiredService<ApplicationDbContext>();
        List<int> thresholds = await db.DocumentExpiryReminders.AsNoTracking()
            .Where(r => r.DocumentId == documentId)
            .Select(r => r.ThresholdDays)
            .OrderByDescending(t => t)
            .ToListAsync();

        // 30/15/7/1 plus 0 for expired. The 45-day sweep is outside every rung.
        Assert.Equal([30, 15, 7, 1, DocumentExpiryReminder.ExpiredThreshold], thresholds);
        Assert.Equal(5, await UnreadCountAsync(driverProfileId));
    }

    [Fact]
    public async Task A_document_with_no_expiry_date_is_never_chased()
    {
        Guid authUserId = Guid.NewGuid();
        Result<DriverRegistrationResult> driver = await _provider.GetRequiredService<IDriverOnboardingService>()
            .RegisterAsync(authUserId, new RegisterDriverRequest(
                "No Expiry", "9800000042", null, "TN0120200004242", null, null));

        ApplicationDbContext db = _provider.GetRequiredService<ApplicationDbContext>();
        Document document = Document.Submit(
            DocumentOwnerType.Driver, driver.Value.DriverProfileId, DocumentType.DriverKyc, "kyc.pdf");
        document.Verify(Today.AddDays(-1), DateTimeOffset.UtcNow);
        db.Documents.Add(document);
        await db.SaveChangesAsync();

        Assert.Equal(0, await _provider.GetRequiredService<IDocumentExpiryService>().SweepAsync(Today));
    }

    [Fact]
    public async Task An_archived_document_is_not_chased()
    {
        // Superseded by a re-upload: chasing it would tell the driver to renew something they
        // already replaced.
        (_, Guid documentId) = await SeedVerifiedLicenceAsync(7);
        ApplicationDbContext db = _provider.GetRequiredService<ApplicationDbContext>();
        Document document = await db.Documents.FirstAsync(d => d.Id == documentId);
        document.Archive();
        await db.SaveChangesAsync();

        Assert.Equal(0, await _provider.GetRequiredService<IDocumentExpiryService>().SweepAsync(Today));
    }
}
