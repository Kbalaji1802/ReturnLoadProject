using ReturnLoad.Domain.Common;

namespace ReturnLoad.Domain.Loads;

/// <summary>A shipper created a load.</summary>
public sealed record LoadCreated(Guid LoadId, Guid ShipperId, DateTimeOffset OccurredAtUtc) : IDomainEvent;

/// <summary>A load was posted and is now open for matching.</summary>
public sealed record LoadPosted(Guid LoadId, DateTimeOffset OccurredAtUtc) : IDomainEvent;
