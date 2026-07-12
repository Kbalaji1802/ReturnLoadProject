using ReturnLoad.Domain.Common;

namespace ReturnLoad.Domain.Identity;

/// <summary>A carrier organisation was registered.</summary>
public sealed record CarrierRegistered(Guid CarrierId, DateTimeOffset OccurredAtUtc) : IDomainEvent;

/// <summary>A driver registered and awaits verification (Trust &amp; Safety pre-trip gate).</summary>
public sealed record DriverRegistered(Guid DriverProfileId, Guid UserProfileId, DateTimeOffset OccurredAtUtc) : IDomainEvent;

/// <summary>A driver's verifications passed and they became <see cref="DriverStatus.Active"/>.</summary>
public sealed record DriverVerified(Guid DriverProfileId, DateTimeOffset OccurredAtUtc) : IDomainEvent;
