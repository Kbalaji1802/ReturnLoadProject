using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReturnLoad.Application;
using ReturnLoad.Application.UseCases.Documents;
using ReturnLoad.Application.UseCases.Loads;
using ReturnLoad.Application.UseCases.Onboarding;
using ReturnLoad.Application.UseCases.Trips;
using ReturnLoad.Domain.Documents;
using ReturnLoad.Domain.Fleet;
using ReturnLoad.Domain.Identity;
using ReturnLoad.Domain.Loads;
using ReturnLoad.Domain.Trips;
using ReturnLoad.Domain.ValueObjects;
using ReturnLoad.Infrastructure;
using ReturnLoad.Infrastructure.Persistence;
using ReturnLoad.Shared.Results;

namespace ReturnLoad.IntegrationTests;

/// <summary>
/// End-to-end use-case flow across the application services + real relational persistence
/// (SQLite): register carrier → register driver → upload &amp; approve licence → driver
/// becomes Verified → post load → browse → accept → create trip → complete. Uses the
/// production DI wiring with the database provider swapped to SQLite.
/// </summary>
public sealed class UseCaseFlowTests : IDisposable
{
    private const string TestKey = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=";
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    public UseCaseFlowTests()
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

        // Swap Npgsql for the SQLite test connection.
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

    [Fact]
    public async Task Full_onboarding_to_trip_completion_flow_works()
    {
        Guid driverAuthId = Guid.NewGuid();
        Guid shipperAuthId = Guid.NewGuid();

        // A shipper profile so loads can be posted.
        ApplicationDbContext db = _provider.GetRequiredService<ApplicationDbContext>();
        db.UserProfiles.Add(UserProfile.Create(shipperAuthId, "Shipper", MobileNumber.Create("9800000009")));
        await db.SaveChangesAsync();

        // 1) Register a carrier.
        Guid carrierId = (await _provider.GetRequiredService<ICarrierService>()
            .RegisterAsync(new RegisterCarrierRequest("Kovai Logistics", "9800000010", null))).Value;

        // 2) Register a driver under the carrier.
        DriverRegistrationResult driver = (await _provider.GetRequiredService<IDriverOnboardingService>()
            .RegisterAsync(driverAuthId, new RegisterDriverRequest("Raja", "9800000011", null, "TN0120200001234", null, carrierId))).Value;

        // 3) Register a vehicle.
        Guid vehicleId = (await _provider.GetRequiredService<IVehicleService>()
            .RegisterAsync(new RegisterVehicleRequest(carrierId, "TN01AB1234", VehicleType.OpenBody, 12000m, null))).Value;

        // 4) Upload the driver's licence document, then Operations approves it.
        IDocumentService documents = _provider.GetRequiredService<IDocumentService>();
        using MemoryStream file = new(Encoding.UTF8.GetBytes("dummy-pdf"));
        Guid documentId = (await documents.SubmitAsync(
            new SubmitDocumentRequest(DocumentOwnerType.Driver, driver.DriverProfileId, DocumentType.DrivingLicence, "DL-1", null, new DateOnly(2030, 1, 1)),
            file, "licence.pdf", "application/pdf")).Value;

        Result approve = await documents.ApproveAsync(documentId);
        Assert.True(approve.IsSuccess);

        // 5) The driver is now Verified (pre-trip gate satisfied).
        DriverProfile verifiedDriver = await db.Drivers.AsNoTracking().FirstAsync(d => d.Id == driver.DriverProfileId);
        Assert.Equal(DriverStatus.Active, verifiedDriver.Status);

        // 6) Shipper posts a load; it is browsable and then accepted.
        ILoadService loads = _provider.GetRequiredService<ILoadService>();
        Guid loadId = (await loads.PostAsync(shipperAuthId, new PostLoadRequest(
            13.08, 80.27, "Chennai", 11.01, 76.95, "Coimbatore",
            DateTimeOffset.UtcNow.AddHours(2), DateTimeOffset.UtcNow.AddHours(8),
            CargoType.General, 5000m, 15000m))).Value;

        Assert.Contains((await loads.BrowseAvailableAsync()).Value, l => l.Id == loadId);
        Assert.True((await loads.AcceptAsync(loadId)).IsSuccess);

        // 7) Create a trip and drive it to completion.
        ITripService trips = _provider.GetRequiredService<ITripService>();
        Guid tripId = (await trips.CreateAsync(new CreateTripRequest(
            carrierId, vehicleId, driver.DriverProfileId,
            11.01, 76.95, "Coimbatore", 13.08, 80.27, "Chennai", 12.9, 77.5, "Bengaluru",
            DateTimeOffset.UtcNow.AddHours(10), DateTimeOffset.UtcNow.AddHours(20)))).Value;

        Assert.True((await trips.AdvanceAsync(tripId, TripStatus.Assigned)).IsSuccess);
        Assert.True((await trips.AdvanceAsync(tripId, TripStatus.Started)).IsSuccess);
        Assert.True((await trips.AdvanceAsync(tripId, TripStatus.InTransit)).IsSuccess);
        Assert.True((await trips.AdvanceAsync(tripId, TripStatus.Completed)).IsSuccess);

        TripView trip = (await trips.GetAsync(tripId)).Value;
        Assert.Equal(TripStatus.Completed, trip.Status);
        Assert.NotNull(trip.CompletedAtUtc);
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }
}
