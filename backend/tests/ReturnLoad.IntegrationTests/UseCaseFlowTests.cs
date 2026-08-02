using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReturnLoad.Application;
using ReturnLoad.Application.Abstractions.Geo;
using ReturnLoad.Application.UseCases.Bookings;
using ReturnLoad.Application.UseCases.Documents;
using ReturnLoad.Application.UseCases.Notifications;
using ReturnLoad.Application.UseCases.Reviews;
using ReturnLoad.Application.UseCases.Tracking;
using ReturnLoad.Application.UseCases.Loads;
using ReturnLoad.Application.UseCases.Matching;
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

        // This trip was created directly (no load owner); staff advances every step including the
        // two owner-confirmation gates.
        foreach (TripStatus step in new[]
        {
            TripStatus.DriverAccepted, TripStatus.DriverEnRoute, TripStatus.ArrivedPickup, TripStatus.PickupConfirmed,
            TripStatus.Loaded, TripStatus.InTransit, TripStatus.ArrivedDestination, TripStatus.Unloaded,
            TripStatus.DeliveryConfirmed, TripStatus.Completed,
        })
        {
            Assert.True((await trips.AdvanceAsync(Guid.NewGuid(), tripId, step, privileged: true)).IsSuccess);
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
        await trips.AdvanceAsync(driverAuthId, accepted.Value, TripStatus.DriverAccepted, privileged: false);
        await trips.AdvanceAsync(driverAuthId, accepted.Value, TripStatus.DriverEnRoute, privileged: false);

        ITrackingService tracking = _provider.GetRequiredService<ITrackingService>();
        Result recorded = await tracking.RecordLocationAsync(
            driverAuthId, accepted.Value, new RecordLocationRequest(9.95, 78.10, DateTimeOffset.UtcNow, SpeedKph: 40));
        Assert.True(recorded.IsSuccess);

        Result<TripLiveView> live = await tracking.GetLiveAsync(shipperAuthId, accepted.Value, privileged: false);
        Assert.True(live.IsSuccess);
        Assert.True(live.Value.HasLocation);

        Result<TripLiveView> stranger = await tracking.GetLiveAsync(Guid.NewGuid(), accepted.Value, privileged: false);
        Assert.True(stranger.IsFailure);

        // M7: the driver was notified of the approval; the owner of the request; both can read.
        INotificationService notifications = _provider.GetRequiredService<INotificationService>();
        IReadOnlyList<NotificationView> driverInbox = (await notifications.ListMineAsync(driverAuthId)).Value;
        Assert.Contains(driverInbox, n => n.Subject == "Request approved");
        Assert.True((await notifications.UnreadCountAsync(driverAuthId)).Value > 0);

        IReadOnlyList<NotificationView> ownerInbox = (await notifications.ListMineAsync(shipperAuthId)).Value;
        Assert.Contains(ownerInbox, n => n.Subject == "New load request");

        // M8 + Part 5: drive the trip to completion — the driver runs the driving steps and the load
        // owner confirms the two gates (pickup + delivery), then both parties review each other.
        await trips.AdvanceAsync(driverAuthId, accepted.Value, TripStatus.ArrivedPickup, privileged: false);
        await trips.AdvanceAsync(shipperAuthId, accepted.Value, TripStatus.PickupConfirmed, privileged: false); // owner gate
        await trips.AdvanceAsync(driverAuthId, accepted.Value, TripStatus.Loaded, privileged: false);
        await trips.AdvanceAsync(driverAuthId, accepted.Value, TripStatus.InTransit, privileged: false);
        await trips.AdvanceAsync(driverAuthId, accepted.Value, TripStatus.ArrivedDestination, privileged: false);
        await trips.AdvanceAsync(driverAuthId, accepted.Value, TripStatus.Unloaded, privileged: false);
        await trips.AdvanceAsync(shipperAuthId, accepted.Value, TripStatus.DeliveryConfirmed, privileged: false); // owner gate
        await trips.AdvanceAsync(driverAuthId, accepted.Value, TripStatus.Completed, privileged: false);

        // The load must follow the trip. It previously stopped at Booked on acceptance and never
        // moved again, so a finished delivery still counted as "running" on the owner's dashboard
        // and "Delivered" was permanently zero.
        Domain.Loads.Load deliveredLoad = await db.Loads.AsNoTracking().FirstAsync(l => l.Id == loadId);
        Assert.Equal(Domain.Loads.LoadStatus.Delivered, deliveredLoad.Status);

        IReviewService reviews = _provider.GetRequiredService<IReviewService>();
        Assert.True((await reviews.SubmitAsync(driverAuthId, accepted.Value, new SubmitReviewRequest(5, "Great load owner"))).IsSuccess);
        Assert.True((await reviews.SubmitAsync(shipperAuthId, accepted.Value, new SubmitReviewRequest(4, "Reliable driver"))).IsSuccess);

        Assert.Equal(4, (await reviews.GetSummaryAsync(driver.UserProfileId)).Average);   // owner rated the driver 4
        Assert.Equal(5, (await reviews.GetSummaryAsync(shipperProfile.Id)).Average);       // driver rated the owner 5

        // A driver can't review the same trip twice.
        Assert.True((await reviews.SubmitAsync(driverAuthId, accepted.Value, new SubmitReviewRequest(3, "dup"))).IsFailure);
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

    [Fact]
    public async Task Matching_excludes_loads_outside_the_pickup_radius_and_requires_a_location()
    {
        // A verified, available driver operating a verified vehicle, currently in Tambaram (Chennai).
        var setup = await SetupVerifiedAvailableDriverAsync("9800000041", "9800000042", "9800000043", "TN0120200004001");
        const double tambaramLat = 12.9249, tambaramLng = 80.1000;

        ILoadService loads = _provider.GetRequiredService<ILoadService>();

        // A nearby Suburban pickup (~4 km, Chromepet) — inside the 10 km radius.
        Guid nearLoadId = (await loads.PostAsync(setup.ShipperAuthId, new PostLoadRequest(
            12.9516, 80.1462, "Chromepet", 11.01, 76.95, "Coimbatore",
            DateTimeOffset.UtcNow.AddHours(2), DateTimeOffset.UtcNow.AddHours(8),
            CargoType.General, 5000m, 15000m, AreaType.Suburban))).Value;

        // A far pickup (Madurai, ~350 km) — well outside any tier's radius.
        Guid farLoadId = (await loads.PostAsync(setup.ShipperAuthId, new PostLoadRequest(
            9.9252, 78.1198, "Madurai", 11.01, 76.95, "Coimbatore",
            DateTimeOffset.UtcNow.AddHours(2), DateTimeOffset.UtcNow.AddHours(8),
            CargoType.General, 5000m, 15000m, AreaType.Highway))).Value;

        IMatchingService matching = _provider.GetRequiredService<IMatchingService>();
        IReadOnlyList<ScoredLoadView> matched = (await matching.FindCompatibleLoadsAsync(
            setup.DriverAuthId, tambaramLat, tambaramLng)).Value;

        Assert.Contains(matched, m => m.Id == nearLoadId);   // inside radius → visible
        Assert.DoesNotContain(matched, m => m.Id == farLoadId); // outside radius → excluded (not just down-ranked)

        // Without a location we cannot honour the radius — the feed is empty, not "everything".
        IReadOnlyList<ScoredLoadView> noLocation = (await matching.FindCompatibleLoadsAsync(
            setup.DriverAuthId, null, null)).Value;
        Assert.Empty(noLocation);
    }

    [Fact]
    public async Task Matching_only_surfaces_loads_to_available_drivers()
    {
        var setup = await SetupVerifiedAvailableDriverAsync("9800000051", "9800000052", "9800000053", "TN0120200005001");
        const double tambaramLat = 12.9249, tambaramLng = 80.1000;

        ILoadService loads = _provider.GetRequiredService<ILoadService>();
        Guid nearLoadId = (await loads.PostAsync(setup.ShipperAuthId, new PostLoadRequest(
            12.9516, 80.1462, "Chromepet", 11.01, 76.95, "Coimbatore",
            DateTimeOffset.UtcNow.AddHours(2), DateTimeOffset.UtcNow.AddHours(8),
            CargoType.General, 5000m, 15000m, AreaType.Suburban))).Value;

        IMatchingService matching = _provider.GetRequiredService<IMatchingService>();
        IDriverOnboardingService driversSvc = _provider.GetRequiredService<IDriverOnboardingService>();

        // Available → sees the load.
        Assert.Contains(
            (await matching.FindCompatibleLoadsAsync(setup.DriverAuthId, tambaramLat, tambaramLng)).Value,
            m => m.Id == nearLoadId);

        // Driver goes Offline → the same query now returns nothing (status change effective immediately).
        Assert.True((await driversSvc.SetAvailabilityAsync(setup.DriverAuthId, DriverAvailability.Offline)).IsSuccess);
        Assert.Empty((await matching.FindCompatibleLoadsAsync(setup.DriverAuthId, tambaramLat, tambaramLng)).Value);
    }

    [Fact]
    public async Task Document_queue_is_enriched_and_rejection_notifies_keeps_history_and_reupload_archives()
    {
        Guid driverAuthId = Guid.NewGuid();

        // Carrier + a driver under it + a vehicle in the fleet.
        Guid carrierId = (await _provider.GetRequiredService<ICarrierService>()
            .RegisterAsync(new RegisterCarrierRequest("Chennai Carriers", "9800000061", null))).Value;
        DriverRegistrationResult driver = (await _provider.GetRequiredService<IDriverOnboardingService>()
            .RegisterAsync(driverAuthId, new RegisterDriverRequest("Kumar", "9800000062", null, "TN0120200006001", null, carrierId))).Value;
        Guid vehicleId = (await _provider.GetRequiredService<IVehicleService>()
            .RegisterAsync(new RegisterVehicleRequest(carrierId, "TN01AB6001", VehicleType.OpenBody, 12000m, null))).Value;

        IDocumentService documents = _provider.GetRequiredService<IDocumentService>();
        using MemoryStream file = new(Encoding.UTF8.GetBytes("dummy-pdf"));
        Guid docId = (await documents.SubmitAsync(
            new SubmitDocumentRequest(DocumentOwnerType.Driver, driver.DriverProfileId, DocumentType.DrivingLicence, "DL-9", null, new DateOnly(2030, 1, 1)),
            file, "licence.pdf", "application/pdf", file.Length)).Value;

        // Part 1: the review queue carries who + company + vehicle context, not a bare GUID.
        PendingDocumentView row = (await documents.ListPendingAsync()).Value.Single(p => p.Id == docId);
        Assert.Equal("Kumar", row.DriverName);
        Assert.Equal("Chennai Carriers", row.Company);
        Assert.Equal("TN01AB6001", row.VehicleRegistration);
        Assert.Equal(DocumentType.DrivingLicence, row.Type);
        Assert.Equal("DL-9", row.DocumentNumber);

        // Part 8: reject with a reason → driver is notified and can read the reason.
        Guid adminId = Guid.NewGuid();
        Assert.True((await documents.RejectAsync(docId, "Blurry scan", adminId)).IsSuccess);

        INotificationService notifications = _provider.GetRequiredService<INotificationService>();
        IReadOnlyList<NotificationView> inbox = (await notifications.ListMineAsync(driverAuthId)).Value;
        Assert.Contains(inbox, n => n.Subject == "Document rejected");

        DocumentView rejected = (await documents.ListForOwnerAsync(DocumentOwnerType.Driver, driver.DriverProfileId)).Value.Single(d => d.Id == docId);
        Assert.Equal("Blurry scan", rejected.RejectionReason);
        Assert.Equal(Domain.Documents.VerificationStatus.Rejected, rejected.VerificationStatus);

        // The decision is kept in the append-only history with its reason and reviewer.
        IReadOnlyList<DocumentReviewView> history = (await documents.ListReviewHistoryAsync(docId)).Value;
        DocumentReviewView entry = Assert.Single(history);
        Assert.Equal(Domain.Documents.DocumentReviewDecision.Rejected, entry.Decision);
        Assert.Equal("Blurry scan", entry.Reason);
        Assert.Equal(adminId, entry.DecidedByUserId);

        // Part 8: the driver re-uploads a replacement → the prior document is archived (history kept).
        using MemoryStream replacement = new(Encoding.UTF8.GetBytes("corrected-pdf"));
        Guid newDocId = (await documents.SubmitAsync(
            new SubmitDocumentRequest(DocumentOwnerType.Driver, driver.DriverProfileId, DocumentType.DrivingLicence, "DL-9", null, new DateOnly(2030, 1, 1)),
            replacement, "licence-v2.pdf", "application/pdf", replacement.Length)).Value;

        IReadOnlyList<DocumentView> owned = (await documents.ListForOwnerAsync(DocumentOwnerType.Driver, driver.DriverProfileId)).Value;
        Assert.Equal(Domain.Documents.DocumentStatus.Archived, owned.Single(d => d.Id == docId).Status);   // superseded, kept for audit
        Assert.Contains(owned, d => d.Id == newDocId && d.Status == Domain.Documents.DocumentStatus.Active); // the live one
    }

    [Fact]
    public async Task Owner_choose_driver_view_is_enriched_for_the_decision()
    {
        var setup = await SetupVerifiedAvailableDriverAsync("9800000071", "9800000072", "9800000073", "TN0120200007001");

        // The driver shares their location while available (near the pickup).
        IDriverOnboardingService driversSvc = _provider.GetRequiredService<IDriverOnboardingService>();
        Assert.True((await driversSvc.SetAvailabilityAsync(setup.DriverAuthId, DriverAvailability.Available, 12.95, 80.15)).IsSuccess);

        // Shipper posts a load; the driver requests it with their verified vehicle.
        ILoadService loads = _provider.GetRequiredService<ILoadService>();
        Guid loadId = (await loads.PostAsync(setup.ShipperAuthId, new PostLoadRequest(
            12.9249, 80.1000, "Tambaram", 11.01, 76.95, "Coimbatore",
            DateTimeOffset.UtcNow.AddHours(2), DateTimeOffset.UtcNow.AddHours(8),
            CargoType.General, 5000m, 15000m, AreaType.Suburban))).Value;

        IBookingService bookings = _provider.GetRequiredService<IBookingService>();
        Assert.True((await bookings.RequestAsync(setup.DriverAuthId, loadId, setup.VehicleId)).IsSuccess);

        // The owner's candidate list carries the full decision context (Part 7).
        BookingRequestView candidate = (await bookings.ListForLoadAsync(setup.ShipperAuthId, loadId)).Value.Single();
        Assert.Equal(Domain.Identity.DriverStatus.Active, candidate.VerificationStatus);        // verified
        Assert.Equal(DriverAvailability.Available, candidate.Availability);                     // available now
        Assert.Equal(0, candidate.CompletedTrips);                                              // no history yet
        Assert.Equal(497.50, candidate.DistanceFromPickupKm);                                   // road distance (stub)
        Assert.Equal(540, candidate.EtaToPickupMinutes);                                        // ETA to pickup (stub)
        Assert.NotNull(candidate.DriverName);
        Assert.NotNull(candidate.VehicleRegistration);
    }

    [Fact]
    public async Task Admin_driver_detail_is_fully_enriched()
    {
        var setup = await SetupVerifiedAvailableDriverAsync("9800000081", "9800000082", "9800000083", "TN0120200008001");

        IDriverOnboardingService drivers = _provider.GetRequiredService<IDriverOnboardingService>();
        Guid driverProfileId = (await drivers.GetForUserAsync(setup.DriverAuthId)).Value.Id;

        DriverDetailView detail = (await drivers.GetDetailAsync(driverProfileId)).Value;

        Assert.Equal("Driver", detail.FullName);                       // resolved from the user profile
        Assert.Equal(Domain.Identity.DriverStatus.Active, detail.Status);
        Assert.Equal(DriverAvailability.Available, detail.Availability);
        Assert.Equal("Fleet Co", detail.Company);                      // carrier via association
        Assert.Single(detail.Vehicles);                                // the fleet vehicle
        Assert.Equal(0, detail.CompletedTrips);
        Assert.Null(detail.CurrentTripId);                             // no active trip yet
    }

    /// <summary>
    /// Onboards a verified (licence approved), Available driver with a verified (Active) vehicle in
    /// their carrier's fleet, plus a shipper profile — the precondition for matching. Returns the ids.
    /// </summary>
    private async Task<(Guid DriverAuthId, Guid ShipperAuthId, Guid VehicleId)> SetupVerifiedAvailableDriverAsync(
        string driverMobile, string shipperMobile, string carrierMobile, string licence)
    {
        Guid driverAuthId = Guid.NewGuid();
        Guid shipperAuthId = Guid.NewGuid();

        ApplicationDbContext db = _provider.GetRequiredService<ApplicationDbContext>();
        db.UserProfiles.Add(UserProfile.Create(shipperAuthId, "Shipper", MobileNumber.Create(shipperMobile)));
        await db.SaveChangesAsync();

        Guid carrierId = (await _provider.GetRequiredService<ICarrierService>()
            .RegisterAsync(new RegisterCarrierRequest("Fleet Co", carrierMobile, null))).Value;
        DriverRegistrationResult driver = (await _provider.GetRequiredService<IDriverOnboardingService>()
            .RegisterAsync(driverAuthId, new RegisterDriverRequest("Driver", driverMobile, null, licence, null, carrierId))).Value;

        // Verify the driver via an approved licence document.
        IDocumentService documents = _provider.GetRequiredService<IDocumentService>();
        using MemoryStream file = new(Encoding.UTF8.GetBytes("dummy-pdf"));
        Guid docId = (await documents.SubmitAsync(
            new SubmitDocumentRequest(DocumentOwnerType.Driver, driver.DriverProfileId, DocumentType.DrivingLicence, "DL", null, new DateOnly(2030, 1, 1)),
            file, "licence.pdf", "application/pdf", file.Length)).Value;
        Assert.True((await documents.ApproveAsync(docId)).IsSuccess);

        // A verified (Active) vehicle in the fleet.
        IVehicleService vehicles = _provider.GetRequiredService<IVehicleService>();
        Guid vehicleId = (await vehicles.RegisterAsync(new RegisterVehicleRequest(carrierId, "TN01AB" + driverMobile[^4..], VehicleType.OpenBody, 12000m, null))).Value;
        Assert.True((await vehicles.ActivateAsync(vehicleId, mandatoryDocumentsValid: true)).IsSuccess);

        // The driver clocks in — Available (default is Offline).
        Assert.True((await _provider.GetRequiredService<IDriverOnboardingService>()
            .SetAvailabilityAsync(driverAuthId, DriverAvailability.Available)).IsSuccess);

        return (driverAuthId, shipperAuthId, vehicleId);
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
