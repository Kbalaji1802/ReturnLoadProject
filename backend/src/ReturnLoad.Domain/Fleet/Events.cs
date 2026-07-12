using ReturnLoad.Domain.Common;

namespace ReturnLoad.Domain.Fleet;

/// <summary>A vehicle was registered under a carrier.</summary>
public sealed record VehicleRegistered(Guid VehicleId, Guid CarrierId, DateTimeOffset OccurredAtUtc) : IDomainEvent;
