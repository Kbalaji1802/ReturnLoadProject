namespace ReturnLoad.Shared.Configuration;

/// <summary>
/// Password strength policy, bound from the "PasswordPolicy" configuration section.
/// Defined in Shared so the Identity module (M2, Application layer) can enforce it
/// without depending on the API host. M1.5 only carries the <b>values</b>; enforcement
/// (registration, reset) arrives with authentication (03_TECHNICAL_BIBLE.md §7).
/// </summary>
public sealed class PasswordPolicyOptions
{
    public const string SectionName = "PasswordPolicy";

    /// <summary>Minimum length. Defaults to 12 — long passphrases beat short complexity.</summary>
    public int MinLength { get; init; } = 12;

    /// <summary>Upper bound, guarding against denial-of-service via huge inputs to the hasher.</summary>
    public int MaxLength { get; init; } = 128;

    public bool RequireUppercase { get; init; } = true;

    public bool RequireLowercase { get; init; } = true;

    public bool RequireDigit { get; init; } = true;

    public bool RequireNonAlphanumeric { get; init; } = true;
}
