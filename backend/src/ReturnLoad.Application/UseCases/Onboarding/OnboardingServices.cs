using ReturnLoad.Application.Abstractions.Persistence;
using ReturnLoad.Domain.Fleet;
using ReturnLoad.Domain.Identity;
using ReturnLoad.Domain.ValueObjects;
using ReturnLoad.Shared.Results;

namespace ReturnLoad.Application.UseCases.Onboarding;

// ---- Contracts --------------------------------------------------------------

public sealed record RegisterCarrierRequest(string LegalName, string ContactMobile, string? Gst);

public sealed record RegisterDriverRequest(
    string FullName, string Mobile, string? Email, string LicenceNumber, string? Aadhaar, Guid? CarrierId);

public sealed record DriverRegistrationResult(Guid DriverProfileId, Guid UserProfileId);

public sealed record DriverSummary(Guid Id, Guid UserProfileId, string Licence, DriverStatus Status);

public sealed record RegisterVehicleRequest(
    Guid CarrierId, string RegistrationNumber, VehicleType Type, decimal MaxPayloadKg, decimal? VolumeCubicMetres);

/// <summary>A driver registers a vehicle into their own fleet (no carrier id — resolved server-side).</summary>
public sealed record RegisterDriverVehicleRequest(
    string RegistrationNumber, VehicleType Type, decimal MaxPayloadKg, decimal? VolumeCubicMetres);

public sealed record VehicleView(
    Guid Id, Guid CarrierId, string RegistrationNumber, VehicleType Type, decimal MaxPayloadKg, VehicleStatus Status);

// ---- Carrier ----------------------------------------------------------------

public interface ICarrierService
{
    Task<Result<Guid>> RegisterAsync(RegisterCarrierRequest request, CancellationToken cancellationToken = default);
}

internal sealed class CarrierService : ICarrierService
{
    private readonly IRepository<Carrier> _carriers;
    private readonly IUnitOfWork _uow;

    public CarrierService(IRepository<Carrier> carriers, IUnitOfWork uow)
    {
        _carriers = carriers;
        _uow = uow;
    }

    public async Task<Result<Guid>> RegisterAsync(RegisterCarrierRequest request, CancellationToken cancellationToken = default)
    {
        Carrier carrier = Carrier.Register(
            request.LegalName,
            MobileNumber.Create(request.ContactMobile),
            string.IsNullOrWhiteSpace(request.Gst) ? null : GstNumber.Create(request.Gst));

        await _carriers.AddAsync(carrier, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return carrier.Id;
    }
}

// ---- Driver onboarding ------------------------------------------------------

public interface IDriverOnboardingService
{
    Task<Result<DriverRegistrationResult>> RegisterAsync(Guid authUserId, RegisterDriverRequest request, CancellationToken cancellationToken = default);

    /// <summary>Lists drivers (Operations view — e.g. verification queue).</summary>
    Task<Result<IReadOnlyList<DriverSummary>>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The caller's own driver profile, resolved from their authenticated user id. Lets a
    /// driver client obtain <b>its own</b> id (e.g. to scope document uploads) instead of
    /// guessing from the drivers list — closing the cross-user attachment hole (M4.2 §8 S1).
    /// </summary>
    Task<Result<DriverSummary>> GetForUserAsync(Guid authUserId, CancellationToken cancellationToken = default);
}

internal sealed class DriverOnboardingService : IDriverOnboardingService
{
    private readonly IRepository<UserProfile> _users;
    private readonly IRepository<DriverProfile> _drivers;
    private readonly IRepository<Association> _associations;
    private readonly IRepository<Carrier> _carriers;
    private readonly IUnitOfWork _uow;

    public DriverOnboardingService(
        IRepository<UserProfile> users,
        IRepository<DriverProfile> drivers,
        IRepository<Association> associations,
        IRepository<Carrier> carriers,
        IUnitOfWork uow)
    {
        _users = users;
        _drivers = drivers;
        _associations = associations;
        _carriers = carriers;
        _uow = uow;
    }

    public async Task<Result<DriverRegistrationResult>> RegisterAsync(Guid authUserId, RegisterDriverRequest request, CancellationToken cancellationToken = default)
    {
        // One driver profile per person.
        UserProfile? profile = (await _users.ListAsync(u => u.AuthUserId == authUserId, cancellationToken)).FirstOrDefault();
        if (profile is not null && await _drivers.ExistsAsync(d => d.UserProfileId == profile.Id, cancellationToken))
        {
            return Error.Conflict("This account already has a driver profile.");
        }

        profile ??= UserProfile.Create(
            authUserId,
            request.FullName,
            MobileNumber.Create(request.Mobile),
            string.IsNullOrWhiteSpace(request.Email) ? null : EmailAddress.Create(request.Email));

        if (profile.Id == default || !await _users.ExistsAsync(u => u.Id == profile.Id, cancellationToken))
        {
            await _users.AddAsync(profile, cancellationToken);
        }

        DriverProfile driver = DriverProfile.Register(
            profile.Id,
            DrivingLicenceNumber.Create(request.LicenceNumber),
            string.IsNullOrWhiteSpace(request.Aadhaar) ? null : AadhaarNumber.Create(request.Aadhaar));
        await _drivers.AddAsync(driver, cancellationToken);

        if (request.CarrierId is Guid carrierId)
        {
            if (!await _carriers.ExistsAsync(c => c.Id == carrierId, cancellationToken))
            {
                return Error.NotFound("Carrier not found.");
            }

            await _associations.AddAsync(Association.Create(carrierId, profile.Id, AssociationRole.Driver), cancellationToken);
        }

        await _uow.SaveChangesAsync(cancellationToken);
        return new DriverRegistrationResult(driver.Id, profile.Id);
    }

    public async Task<Result<IReadOnlyList<DriverSummary>>> ListAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<DriverProfile> drivers = await _drivers.ListAsync(_ => true, cancellationToken);
        IReadOnlyList<DriverSummary> summaries = drivers
            .Select(d => new DriverSummary(d.Id, d.UserProfileId, d.Licence.Value, d.Status))
            .ToList();
        return Result<IReadOnlyList<DriverSummary>>.Success(summaries);
    }

    public async Task<Result<DriverSummary>> GetForUserAsync(Guid authUserId, CancellationToken cancellationToken = default)
    {
        UserProfile? profile = (await _users.ListAsync(u => u.AuthUserId == authUserId, cancellationToken)).FirstOrDefault();
        DriverProfile? driver = profile is null
            ? null
            : (await _drivers.ListAsync(d => d.UserProfileId == profile.Id, cancellationToken)).FirstOrDefault();

        return driver is null
            ? Error.NotFound("This account does not have a driver profile yet.")
            : new DriverSummary(driver.Id, driver.UserProfileId, driver.Licence.Value, driver.Status);
    }
}

// ---- Vehicle ----------------------------------------------------------------

public interface IVehicleService
{
    Task<Result<Guid>> RegisterAsync(RegisterVehicleRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// A driver registers a vehicle into their own fleet. If the driver has no carrier yet, an
    /// owner-operator carrier is created and the driver associated with it (M4.4). Returns the
    /// new vehicle id.
    /// </summary>
    Task<Result<Guid>> RegisterForDriverAsync(Guid authUserId, RegisterDriverVehicleRequest request, CancellationToken cancellationToken = default);

    /// <summary>The authenticated driver's fleet vehicles (with verification status).</summary>
    Task<Result<IReadOnlyList<VehicleView>>> ListForDriverAsync(Guid authUserId, CancellationToken cancellationToken = default);

    /// <summary>Vehicles for the ops/admin console, optionally filtered by status (e.g. pending = Draft).</summary>
    Task<Result<IReadOnlyList<VehicleView>>> ListByStatusAsync(VehicleStatus? status, CancellationToken cancellationToken = default);

    Task<Result> ActivateAsync(Guid vehicleId, bool mandatoryDocumentsValid, CancellationToken cancellationToken = default);
}

internal sealed class VehicleService : IVehicleService
{
    private readonly IRepository<Vehicle> _vehicles;
    private readonly IRepository<Carrier> _carriers;
    private readonly IRepository<UserProfile> _users;
    private readonly IRepository<DriverProfile> _drivers;
    private readonly IRepository<Association> _associations;
    private readonly IUnitOfWork _uow;

    public VehicleService(
        IRepository<Vehicle> vehicles,
        IRepository<Carrier> carriers,
        IRepository<UserProfile> users,
        IRepository<DriverProfile> drivers,
        IRepository<Association> associations,
        IUnitOfWork uow)
    {
        _vehicles = vehicles;
        _carriers = carriers;
        _users = users;
        _drivers = drivers;
        _associations = associations;
        _uow = uow;
    }

    public async Task<Result<Guid>> RegisterAsync(RegisterVehicleRequest request, CancellationToken cancellationToken = default)
    {
        if (!await _carriers.ExistsAsync(c => c.Id == request.CarrierId, cancellationToken))
        {
            return Error.NotFound("Carrier not found.");
        }

        Vehicle vehicle = Vehicle.Register(
            request.CarrierId,
            VehicleRegistrationNumber.Create(request.RegistrationNumber),
            request.Type,
            VehicleCapacity.Create(Weight.FromKilograms(request.MaxPayloadKg), request.VolumeCubicMetres));

        await _vehicles.AddAsync(vehicle, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return vehicle.Id;
    }

    public async Task<Result<Guid>> RegisterForDriverAsync(Guid authUserId, RegisterDriverVehicleRequest request, CancellationToken cancellationToken = default)
    {
        UserProfile? profile = (await _users.ListAsync(u => u.AuthUserId == authUserId, cancellationToken)).FirstOrDefault();
        DriverProfile? driver = profile is null
            ? null
            : (await _drivers.ListAsync(d => d.UserProfileId == profile.Id, cancellationToken)).FirstOrDefault();
        if (profile is null || driver is null)
        {
            return Error.Validation("Register your driver profile before adding a vehicle.");
        }

        // Resolve the driver's carrier, or create an owner-operator carrier + association.
        Association? association = (await _associations.ListAsync(
            a => a.MemberUserProfileId == profile.Id && a.Role == AssociationRole.Driver && a.Status != AssociationStatus.Revoked,
            cancellationToken)).FirstOrDefault();

        Guid carrierId;
        if (association is not null)
        {
            carrierId = association.CarrierId;
        }
        else
        {
            Carrier carrier = Carrier.Register($"{profile.FullName} (Owner-Operator)", profile.Mobile);
            await _carriers.AddAsync(carrier, cancellationToken);
            await _associations.AddAsync(Association.Create(carrier.Id, profile.Id, AssociationRole.Driver), cancellationToken);
            carrierId = carrier.Id;
        }

        Vehicle vehicle = Vehicle.Register(
            carrierId,
            VehicleRegistrationNumber.Create(request.RegistrationNumber),
            request.Type,
            VehicleCapacity.Create(Weight.FromKilograms(request.MaxPayloadKg), request.VolumeCubicMetres));

        await _vehicles.AddAsync(vehicle, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return vehicle.Id;
    }

    public async Task<Result<IReadOnlyList<VehicleView>>> ListForDriverAsync(Guid authUserId, CancellationToken cancellationToken = default)
    {
        UserProfile? profile = (await _users.ListAsync(u => u.AuthUserId == authUserId, cancellationToken)).FirstOrDefault();
        if (profile is null)
        {
            return Error.Validation("Register your driver profile first.");
        }

        HashSet<Guid> carrierIds = [.. (await _associations.ListAsync(
            a => a.MemberUserProfileId == profile.Id && a.Role == AssociationRole.Driver && a.Status != AssociationStatus.Revoked,
            cancellationToken)).Select(a => a.CarrierId)];

        if (carrierIds.Count == 0)
        {
            return Result<IReadOnlyList<VehicleView>>.Success([]);
        }

        IReadOnlyList<Vehicle> vehicles = await _vehicles.ListAsync(v => carrierIds.Contains(v.CarrierId), cancellationToken);
        return Result<IReadOnlyList<VehicleView>>.Success(vehicles.Select(MapVehicle).ToList());
    }

    public async Task<Result<IReadOnlyList<VehicleView>>> ListByStatusAsync(VehicleStatus? status, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Vehicle> vehicles = status is VehicleStatus s
            ? await _vehicles.ListAsync(v => v.Status == s, cancellationToken)
            : await _vehicles.ListAsync(_ => true, cancellationToken);
        return Result<IReadOnlyList<VehicleView>>.Success(vehicles.Select(MapVehicle).ToList());
    }

    private static VehicleView MapVehicle(Vehicle v) =>
        new(v.Id, v.CarrierId, v.Registration.Value, v.Type, v.Capacity.MaxPayload.Kilograms, v.Status);

    public async Task<Result> ActivateAsync(Guid vehicleId, bool mandatoryDocumentsValid, CancellationToken cancellationToken = default)
    {
        Vehicle? vehicle = await _vehicles.GetByIdAsync(vehicleId, cancellationToken);
        if (vehicle is null)
        {
            return Result.Failure(Error.NotFound("Vehicle not found."));
        }

        vehicle.Activate(mandatoryDocumentsValid);
        _vehicles.Update(vehicle);
        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
