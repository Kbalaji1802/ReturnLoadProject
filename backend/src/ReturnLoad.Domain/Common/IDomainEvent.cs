namespace ReturnLoad.Domain.Common;

/// <summary>
/// A fact that has happened in the domain (e.g. a driver was verified, a trip started).
/// Aggregates raise events; the application layer dispatches them after persistence.
/// Events are immutable and past-tense. Kept intentionally minimal — no dispatching or
/// handler machinery lives in the Domain layer (that is an application concern).
/// </summary>
public interface IDomainEvent
{
    /// <summary>When the event occurred (UTC).</summary>
    DateTimeOffset OccurredAtUtc { get; }
}
