namespace ReturnLoad.Domain.Common;

/// <summary>
/// Raised when a domain invariant or rule is violated (e.g. constructing an entity in an
/// invalid state, or an illegal state transition). Invariants are enforced inside the
/// domain — never left to callers — so an invalid entity can never exist
/// (01_PROJECT_RULES.md §4, §5: typed boundaries, fail loudly). Carries a stable
/// machine-readable <see cref="Code"/> so the application layer can map it to an API
/// error without string-matching the message.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string message, string code = "domain_rule_violation")
        : base(message) => Code = code;

    /// <summary>Stable, machine-readable rule identifier (e.g. <c>capacity_must_be_positive</c>).</summary>
    public string Code { get; }
}
