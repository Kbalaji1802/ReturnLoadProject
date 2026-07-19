using ReturnLoad.Domain.Common;

namespace ReturnLoad.Domain.Bookings;

/// <summary>A driver requested to carry a load.</summary>
public sealed record BookingRequested(Guid BookingRequestId, Guid LoadId, Guid DriverProfileId, DateTimeOffset OccurredAtUtc) : IDomainEvent;

/// <summary>The load owner accepted a driver's request (a trip will be created).</summary>
public sealed record BookingAccepted(Guid BookingRequestId, Guid LoadId, Guid DriverProfileId, DateTimeOffset OccurredAtUtc) : IDomainEvent;

/// <summary>A driver's request was rejected (by the load owner) or superseded.</summary>
public sealed record BookingRejected(Guid BookingRequestId, Guid LoadId, DateTimeOffset OccurredAtUtc) : IDomainEvent;
