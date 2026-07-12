using ReturnLoad.Domain.Common;
using ReturnLoad.Domain.ValueObjects;

namespace ReturnLoad.Domain.Identity;

/// <summary>
/// The business profile of a platform person — the domain counterpart of the authentication
/// account (ADR-0013 keeps auth lean; profile data lives here, keyed by <see cref="AuthUserId"/>).
/// Root of the person-side aggregates: a Driver or Dispatcher references a UserProfile.
/// <para><b>Invariants:</b> linked to exactly one auth account; full name and mobile required.</para>
/// </summary>
public sealed class UserProfile : AggregateRoot<Guid>
{
    private UserProfile(Guid id, Guid authUserId, string fullName, MobileNumber mobile, EmailAddress? email, Language language)
        : base(id)
    {
        AuthUserId = authUserId;
        FullName = fullName;
        Mobile = mobile;
        Email = email;
        PreferredLanguage = language;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    private UserProfile()
    {
    }

    /// <summary>Links to the authentication account (Infrastructure ApplicationUser).</summary>
    public Guid AuthUserId { get; }

    public string FullName { get; private set; } = null!;

    public MobileNumber Mobile { get; private set; } = null!;

    public EmailAddress? Email { get; private set; }

    public Language PreferredLanguage { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public static UserProfile Create(
        Guid authUserId,
        string fullName,
        MobileNumber mobile,
        EmailAddress? email = null,
        Language language = Language.English)
    {
        Guard.AgainstDefault(authUserId, "Auth user id", "user_auth_required");
        string name = Guard.AgainstNullOrWhiteSpace(fullName, "Full name", "user_name_required");
        ArgumentNullException.ThrowIfNull(mobile);
        return new UserProfile(Guid.NewGuid(), authUserId, name, mobile, email, language);
    }

    public void ChangeContact(MobileNumber mobile, EmailAddress? email)
    {
        ArgumentNullException.ThrowIfNull(mobile);
        Mobile = mobile;
        Email = email;
    }

    public void SetPreferredLanguage(Language language) => PreferredLanguage = language;
}
