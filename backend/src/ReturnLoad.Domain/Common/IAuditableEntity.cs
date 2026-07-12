namespace ReturnLoad.Domain.Common;

/// <summary>
/// Marks an entity that carries a who/when audit trail
/// (01_PROJECT_RULES.md §6: "Every table has an audit trail for who/when on
/// sensitive records"). These fields are populated by the persistence layer, not
/// by application callers, so the trail cannot be forged from the outside.
/// </summary>
public interface IAuditableEntity
{
    DateTimeOffset CreatedAtUtc { get; }

    string? CreatedBy { get; }

    DateTimeOffset? UpdatedAtUtc { get; }

    string? UpdatedBy { get; }
}
