using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReturnLoad.Application;
using ReturnLoad.Application.Abstractions.Geo;
using ReturnLoad.Application.UseCases.Bookings;
using ReturnLoad.Application.UseCases.Documents;
using ReturnLoad.Application.UseCases.Tracking;
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

        // Replace the real OSRM route provider so posting a load computes distance/ETA from a
        // deterministic stub instead of hitting the network (last registration wins).
        services.AddSingleton<IRouteService>(new FakeRouteService());

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
            file, "licence.pdf", "application/pdf", file.Length)).Value;

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

        LoadView posted = (await loads.BrowseAvailableAsync()).Value.Single(l => l.Id == loadId);
        // The platform computed and stored the route metrics (M4.3 Step 1) — not the shipper.
        Assert.Equal(497.50m, posted.DistanceKm);
        Assert.Equal(540, posted.EstimatedDurationMinutes);

        // 7) Create a trip and drive it to completion.
        ITripService trips = _provider.GetRequiredService<ITripService>();
        Guid tripId = (await trips.CreateAsync(new CreateTripRequest(
            carrierId, vehicleId, driver.DriverProfileId,
            11.01, 76.95, "Coimbatore", 13.08, 80.27, "Chennai", 12.9, 77.5, "Bengaluru",
            DateTimeOffset.UtcNow.AddHours(10), DateTimeOffset.UtcNow.AddHours(20)))).Value;

        foreach (TripStatus step in new[]
        {
            TripStatus.DriverAccepted, TripStatus.DriverEnRoute, TripStatus.ArrivedPickup, TripStatus.Loaded,
            TripStatus.InTransit, TripStatus.ArrivedDestination, TripStatus.Unloaded, TripStatus.Completed,
        })
        {
            Assert.True((await trips.AdvanceAsync(tripId, step)).IsSuccess);
        }

        TripView trip = (await trips.GetAsync(tripId)).Value;
        Assert.Equal(TripStatus.Completed, trip.Status);
        Assert.NotNull(trip.CompletedAtUtc);
    }

    [Fact]
    public async Task Booking_request_then_owner_accept_creates_a_trip_and_assigns_the_load()
    {
        Guid driverAuthId = Guid.NewGuid();
        Guid shipperAuthId = Guid.NewGuid();

        ApplicationDbContext db = _provider.GetRequiredService<ApplicationDbContext>();
        UserProfile shipperProfile = UserProfile.Create(shipperAuthId, "Shipper", MobileNumber.Create("9800000019"));
        db.UserProfiles.Add(shipperProfile);
        await db.SaveChangesAsync();

        // Carrier + a driver under it, verified via an approved licence.
        Guid carrierId = (await _provider.GetRequiredService<ICarrierService>()
            .RegisterAsync(new RegisterCarrierRequest("Madurai Movers", "9800000020", null))).Value;
        DriverRegistrationResult driver = (await _provider.GetRequiredService<IDriverOnboardingService>()
            .RegisterAsync(driverAuthId, new RegisterDriverRequest("Vel", "9800000021", null, "TN0120200005678", null, carrierId))).Value;

        IDocumentService documents = _provider.GetRequiredService<IDocumentService>();
        using MemoryStream licence = new(Encoding.UTF8.GetBytes("dummy-pdf"));
        Guid docId = (await documents.SubmitAsync(
            new SubmitDocumentRequest(DocumentOwnerType.Driver, driver.DriverProfileId, DocumentType.DrivingLicence, "DL-2", null, new DateOnly(2030, 1, 1)),
            licence, "licence.pdf", "application/pdf", licence.Length)).Value;
        Assert.True((await documents.ApproveAsync(docId)).IsSuccess);

        // A verified (Active) vehicle in the carrier's fleet.
        IVehicleService vehicles = _provider.GetRequiredService<IVehicleService>();
        Guid vehicleId = (await vehicles.RegisterAsync(new RegisterVehicleRequest(carrierId, "TN58AB9999", VehicleType.OpenBody, 12000m, null))).Value;
        Assert.True((await vehicles.ActivateAsync(vehicleId, mandatoryDocumentsValid: true)).IsSuccess);

        // Shipper posts a load.
        ILoadService loads = _provider.GetRequiredService<ILoadService>();
        Guid loadId = (await loads.PostAsync(shipperAuthId, new PostLoadRequest(
            13.08, 80.27, "Chennai", 9.92, 78.11, "Madurai",
            DateTimeOffset.UtcNow.AddHours(2), DateTimeOffset.UtcNow.AddHours(8),
            CargoType.General, 5000m, 15000m))).Value;

        // Step 3: the driver requests the load. Step 4: the owner accepts → a trip is created.
        IBookingService bookings = _provider.GetRequiredService<IBookingService>();
        Guid requestId = (await bookings.RequestAsync(driverAuthId, loadId, vehicleId)).Value;
        Result<Guid> accepted = await bookings.AcceptAsync(shipperAuthId, requestId);
        Assert.True(accepted.IsSuccess);

        // The load is assigned (Booked) and a trip now exists for this driver + vehicle.
        Load assignedLoad = await db.Loads.AsNoTracking().FirstAsync(l => l.Id == loadId);
        Assert.Equal(LoadStatus.Booked, assignedLoad.Status);
        Domain.Trips.Trip trip = await db.Trips.AsNoTracking().FirstAsync(t => t.Id == accepted.Value);
        Assert.Equal(driver.DriverProfileId, trip.DriverProfileId);
        Assert.Equal(vehicleId, trip.VehicleId);

        // The request is now Accepted.
        Domain.Bookings.BookingRequest request = await db.BookingRequests.AsNoTracking().FirstAsync(b => b.Id == requestId);
        Assert.Equal(Domain.Bookings.BookingRequestStatus.Accepted, request.Status);

        // M6: drive the trip to an active state, the driver records a location, and the load
        // owner can see it live — but an unrelated user cannot.
        ITripService trips = _provider.GetRequiredService<ITripService>();
        await trips.AdvanceAsync(accepted.Value, TripStatus.DriverAccepted);
        await trips.AdvanceAsync(accepted.Value, TripStatus.DriverEnRoute);

        ITrackingService tracking = _provider.GetRequiredService<ITrackingService>();
        Result recorded = await tracking.RecordLocationAsync(
            driverAuthId, accepted.Value, new RecordLocationRequest(9.95, 78.10, DateTimeOffset.UtcNow, SpeedKph: 40));
        Assert.True(recorded.IsSuccess);

        Result<TripLiveView> live = await tracking.GetLiveAsync(shipperAuthId, accepted.Value, privileged: false);
        Assert.True(live.IsSuccess);
        Assert.True(live.Value.HasLocation);

        Result<TripLiveView> stranger = await tracking.GetLiveAsync(Guid.NewGuid(), accepted.Value, privileged: false);
        Assert.True(stranger.IsFailure);
    }

    [Fact]
    public async Task Driver_adding_a_vehicle_creates_an_owner_operator_carrier()
    {
        Guid driverAuthId = Guid.NewGuid();

        // A driver with no carrier (self-registered, carrierId null).
        await _provider.GetRequiredService<IDriverOnboardingService>()
            .RegisterAsync(driverAuthId, new RegisterDriverRequest("Solo", "9800000031", null, "TN0120200009999", null, null));

        IVehicleService vehicles = _provider.GetRequiredService<IVehicleService>();
        Guid vehicleId = (await vehicles.RegisterForDriverAsync(
            driverAuthId, new RegisterDriverVehicleRequest("TN59CD1234", VehicleType.OpenBody, 9000m, null))).Value;

        // The vehicle is listed for the driver and a carrier was created to own it.
        IReadOnlyList<VehicleView> mine = (await vehicles.ListForDriverAsync(driverAuthId)).Value;
        VehicleView view = Assert.Single(mine);
        Assert.Equal(vehicleId, view.Id);
        Assert.Equal(VehicleStatus.Draft, view.Status);

        ApplicationDbContext db = _provider.GetRequiredService<ApplicationDbContext>();
        Assert.True(await db.Carriers.AsNoTracking().AnyAsync(c => c.Id == view.CarrierId));
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }

    /// <summary>Deterministic route provider for tests — 497.50 km / 9 hours, no network.</summary>
    private sealed class FakeRouteService : IRouteService
    {
        public Task<RouteResult?> GetRouteAsync(
            double originLatitude, double originLongitude, double destinationLatitude, double destinationLongitude,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<RouteResult?>(new RouteResult(497.50m, TimeSpan.FromHours(9)));
    }
}
