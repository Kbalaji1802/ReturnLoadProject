using ReturnLoad.Application.Abstractions.Persistence;
using ReturnLoad.Domain.Identity;
using ReturnLoad.Domain.Loads;
using ReturnLoad.Domain.ValueObjects;
using ReturnLoad.Shared.Results;

namespace ReturnLoad.Application.UseCases.Loads;

public sealed record PostLoadRequest(
    double OriginLat, double OriginLng, string? OriginAddress,
    double DestinationLat, double DestinationLng, string? DestinationAddress,
    DateTimeOffset PickupStart, DateTimeOffset PickupEnd,
    CargoType CargoType, decimal WeightKg, decimal? OfferedPriceInr);

public sealed record LoadView(
    Guid Id, Guid ShipperId, string? OriginAddress, string? DestinationAddress,
    DateTimeOffset PickupStart, DateTimeOffset PickupEnd, CargoType CargoType,
    decimal WeightKg, decimal? OfferedPriceInr, LoadStatus Status);

public interface ILoadService
{
    Task<Result<Guid>> PostAsync(Guid authUserId, PostLoadRequest request, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<LoadView>>> BrowseAvailableAsync(CancellationToken cancellationToken = default);

    Task<Result<LoadView>> GetAsync(Guid loadId, CancellationToken cancellationToken = default);

    /// <summary>Accept an available load (proposes + commits it for MVP).</summary>
    Task<Result> AcceptAsync(Guid loadId, CancellationToken cancellationToken = default);
}

internal sealed class LoadService : ILoadService
{
    private readonly IRepository<Load> _loads;
    private readonly IRepository<UserProfile> _users;
    private readonly IUnitOfWork _uow;

    public LoadService(IRepository<Load> loads, IRepository<UserProfile> users, IUnitOfWork uow)
    {
        _loads = loads;
        _users = users;
        _uow = uow;
    }

    public async Task<Result<Guid>> PostAsync(Guid authUserId, PostLoadRequest request, CancellationToken cancellationToken = default)
    {
        UserProfile? shipper = (await _users.ListAsync(u => u.AuthUserId == authUserId, cancellationToken)).FirstOrDefault();
        if (shipper is null)
        {
            return Error.Validation("Complete your profile before posting a load.");
        }

        Load load = Load.Create(
            shipper.Id,
            Location.Create(GeoCoordinate.Create(request.OriginLat, request.OriginLng), request.OriginAddress),
            Location.Create(GeoCoordinate.Create(request.DestinationLat, request.DestinationLng), request.DestinationAddress),
            TimeWindow.Create(request.PickupStart, request.PickupEnd),
            LoadRequirement.Create(request.CargoType, Weight.FromKilograms(request.WeightKg)),
            request.OfferedPriceInr is decimal price ? Money.Of(price) : null);

        load.Post();
        await _loads.AddAsync(load, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return load.Id;
    }

    public async Task<Result<IReadOnlyList<LoadView>>> BrowseAvailableAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Load> loads = await _loads.ListAsync(l => l.Status == LoadStatus.Posted, cancellationToken);
        return Result<IReadOnlyList<LoadView>>.Success(loads.Select(Map).ToList());
    }

    public async Task<Result<LoadView>> GetAsync(Guid loadId, CancellationToken cancellationToken = default)
    {
        Load? load = await _loads.GetByIdAsync(loadId, cancellationToken);
        return load is null ? Error.NotFound("Load not found.") : Map(load);
    }

    public async Task<Result> AcceptAsync(Guid loadId, CancellationToken cancellationToken = default)
    {
        Load? load = await _loads.GetByIdAsync(loadId, cancellationToken);
        if (load is null)
        {
            return Result.Failure(Error.NotFound("Load not found."));
        }

        load.MarkMatched();
        load.Book();
        _loads.Update(load);
        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static LoadView Map(Load l) => new(
        l.Id, l.ShipperId, l.Origin.Address, l.Destination.Address,
        l.PickupWindow.Start, l.PickupWindow.End, l.Requirement.CargoType,
        l.Requirement.Weight.Kilograms, l.OfferedPrice?.Amount, l.Status);
}
