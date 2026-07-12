using ReturnLoad.Domain.Common;

namespace ReturnLoad.Domain.Administration;

/// <summary>
/// An immutable who/when/what record for a sensitive action (domain map §12;
/// <c>01_PROJECT_RULES.md</c> §6; Trust &amp; Safety §5). <b>Append-only</b>: it exposes no
/// mutators — correcting the record means writing a new entry, never editing this one.
/// Captures actor, action, subject, reason, and optional before/after state plus the
/// request correlation id (the M1 <c>traceId</c>) so an entry ties back to its request.
/// <para><b>Invariants:</b> actor and action are required; occurred-at is set at creation.</para>
/// </summary>
public sealed class AuditLog : AggregateRoot<Guid>
{
    private AuditLog(
        Guid id,
        string actor,
        string action,
        string? subjectType,
        Guid? subjectId,
        string? reason,
        string? beforeState,
        string? afterState,
        string? correlationId)
        : base(id)
    {
        Actor = actor;
        Action = action;
        SubjectType = subjectType;
        SubjectId = subjectId;
        Reason = reason;
        BeforeState = beforeState;
        AfterState = afterState;
        CorrelationId = correlationId;
        OccurredAtUtc = DateTimeOffset.UtcNow;
    }

    private AuditLog()
    {
    }

    /// <summary>Who performed the action (user id or system identifier).</summary>
    public string Actor { get; } = null!;

    /// <summary>What happened (e.g. <c>DocumentVerified</c>, <c>DriverBlocked</c>).</summary>
    public string Action { get; } = null!;

    public string? SubjectType { get; }

    public Guid? SubjectId { get; }

    public string? Reason { get; }

    public string? BeforeState { get; }

    public string? AfterState { get; }

    /// <summary>The request correlation id (M1 <c>traceId</c>), if the action came from a request.</summary>
    public string? CorrelationId { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public static AuditLog Record(
        string actor,
        string action,
        string? subjectType = null,
        Guid? subjectId = null,
        string? reason = null,
        string? beforeState = null,
        string? afterState = null,
        string? correlationId = null)
    {
        string who = Guard.AgainstNullOrWhiteSpace(actor, "Actor", "audit_actor_required");
        string what = Guard.AgainstNullOrWhiteSpace(action, "Action", "audit_action_required");
        return new AuditLog(Guid.NewGuid(), who, what, subjectType, subjectId, reason, beforeState, afterState, correlationId);
    }
}
