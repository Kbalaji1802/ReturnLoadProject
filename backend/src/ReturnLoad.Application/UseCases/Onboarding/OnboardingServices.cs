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
}

// ---- Vehicle ----------------------------------------------------------------

public interface IVehicleService
{
    Task<Result<Guid>> RegisterAsync(RegisterVehicleRequest request, CancellationToken cancellationToken = default);

    Task<Result> ActivateAsync(Guid vehicleId, bool mandatoryDocumentsValid, CancellationToken cancellationToken = default);
}

internal sealed class VehicleService : IVehicleService
{
    private readonly IRepository<Vehicle> _vehicles;
    private readonly IRepository<Carrier> _carriers;
    private readonly IUnitOfWork _uow;

    public VehicleService(IRepository<Vehicle> vehicles, IRepository<Carrier> carriers, IUnitOfWork uow)
    {
        _vehicles = vehicles;
        _carriers = carriers;
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
