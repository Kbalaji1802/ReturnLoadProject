using ReturnLoad.Domain.Common;

namespace ReturnLoad.Domain.Trips;

/// <summary>A trip was created for a vehicle + driver.</summary>
public sealed record TripCreated(Guid TripId, Guid VehicleId, Guid DriverProfileId, DateTimeOffset OccurredAtUtc) : IDomainEvent;

/// <summary>A trip started (the driver is under way).</summary>
public sealed record TripStarted(Guid TripId, DateTimeOffset OccurredAtUtc) : IDomainEvent;

/// <summary>A trip completed.</summary>
public sealed record TripCompleted(Guid TripId, DateTimeOffset OccurredAtUtc) : IDomainEvent;
