using ReturnLoad.Application.Abstractions.Persistence;
using ReturnLoad.Application.UseCases.Loads;
using ReturnLoad.Domain.Fleet;
using ReturnLoad.Domain.Identity;
using ReturnLoad.Domain.Loads;
using ReturnLoad.Shared.Results;

namespace ReturnLoad.Application.UseCases.Matching;

/// <summary>
/// The MVP matching engine (<c>MATCHING_ENGINE.md</c>): given an authenticated driver, returns
/// the posted loads their fleet can actually carry — never the whole board. Enforces the hard
/// filters our data supports today: driver verified (7), vehicle verified (8), vehicle type ↔
/// cargo (1) and payload capacity (2). Geo-corridor/time filters (3,4,5) arrive with PostGIS.
/// <b>Zero matches is a valid result</b>, not an error.
/// </summary>
public interface IMatchingService
{
    Task<Result<IReadOnlyList<LoadView>>> FindCompatibleLoadsAsync(Guid authUserId, CancellationToken cancellationToken = default);
}

internal sealed class MatchingService : IMatchingService
{
    private readonly IRepository<UserProfile> _users;
    private readonly IRepository<DriverProfile> _drivers;
    private readonly IRepository<Association> _associations;
    private readonly IRepository<Vehicle> _vehicles;
    private readonly IRepository<Load> _loads;

    public MatchingService(
        IRepository<UserProfile> users,
        IRepository<DriverProfile> drivers,
        IRepository<Association> associations,
        IRepository<Vehicle> vehicles,
        IRepository<Load> loads)
    {
        _users = users;
        _drivers = drivers;
        _associations = associations;
        _vehicles = vehicles;
        _loads = loads;
    }

    public async Task<Result<IReadOnlyList<LoadView>>> FindCompatibleLoadsAsync(Guid authUserId, CancellationToken cancellationToken = default)
    {
        UserProfile? profile = (await _users.ListAsync(u => u.AuthUserId == authUserId, cancellationToken)).FirstOrDefault();
        DriverProfile? driver = profile is null
            ? null
            : (await _drivers.ListAsync(d => d.UserProfileId == profile.Id, cancellationToken)).FirstOrDefault();

        if (profile is null || driver is null)
        {
            return Error.Validation("Register as a driver to see matching loads.");
        }

        // Filter 7 — an unverified driver is eligible for nothing (Trust & Safety §1). Not an
        // error: "no matches yet" until verification completes.
        if (driver.Status != DriverStatus.Active)
        {
            return Empty();
        }

        // The driver's carrier(s). Associations are created Pending and (today) not explicitly
        // activated, so we accept any non-revoked link; tightening to Active follows when
        // association activation is tied to verification.
        IReadOnlyList<Association> associations = await _associations.ListAsync(
            a => a.MemberUserProfileId == profile.Id
                && a.Role == AssociationRole.Driver
                && a.Status != AssociationStatus.Revoked,
            cancellationToken);

        HashSet<Guid> carrierIds = [.. associations.Select(a => a.CarrierId)];
        if (carrierIds.Count == 0)
        {
            return Empty();
        }

        // Filter 8 — only verified (Active) vehicles are matchable.
        IReadOnlyList<Vehicle> vehicles = await _vehicles.ListAsync(
            v => carrierIds.Contains(v.CarrierId) && v.Status == VehicleStatus.Active, cancellationToken);
        if (vehicles.Count == 0)
        {
            return Empty();
        }

        IReadOnlyList<Load> posted = await _loads.ListAsync(l => l.Status == LoadStatus.Posted, cancellationToken);

        // Filters 1 + 2 — a load matches if ANY of the driver's verified vehicles can carry it.
        List<LoadView> matches = posted
            .Where(load => vehicles.Any(vehicle => MatchingRules.IsEligible(load, vehicle)))
            .Select(Map)
            .ToList();

        return Result<IReadOnlyList<LoadView>>.Success(matches);
    }

    private static Result<IReadOnlyList<LoadView>> Empty() =>
        Result<IReadOnlyList<LoadView>>.Success([]);

    private static LoadView Map(Load l) => new(
        l.Id, l.ShipperId, l.Origin.Address, l.Destination.Address,
        l.PickupWindow.Start, l.PickupWindow.End, l.Requirement.CargoType,
        l.Requirement.Weight.Kilograms, l.OfferedPrice?.Amount, l.Status,
        l.DistanceKm, l.EstimatedDurationMinutes);
}
