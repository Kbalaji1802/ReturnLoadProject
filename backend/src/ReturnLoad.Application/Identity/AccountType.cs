namespace ReturnLoad.Application.Identity;

/// <summary>
/// The marketplace side a self-service registrant signs up as (M4.2, ADR-0017). The public may
/// only register as one of these two sides — a <b>Driver</b> (fills empty return legs) or a
/// <b>Load Owner</b> (posts cargo). Internal/staff roles (<see cref="Roles.Internal"/>) are never
/// self-assignable; they are granted by an administrator. Each type maps to a stable
/// authorization role (see <see cref="AccountTypeExtensions.ToRole"/>).
/// </summary>
public enum AccountType
{
    /// <summary>A person who operates a truck. Granted <see cref="Roles.Driver"/>.</summary>
    Driver = 0,

    /// <summary>A shipper who posts loads. Labelled "Load Owner" in the UI; granted <see cref="Roles.Shipper"/>.</summary>
    LoadOwner = 1,
}

/// <summary>Maps a public <see cref="AccountType"/> to the role granted at registration.</summary>
public static class AccountTypeExtensions
{
    public static string ToRole(this AccountType accountType) => accountType switch
    {
        AccountType.Driver => Roles.Driver,
        AccountType.LoadOwner => Roles.Shipper,
        _ => throw new ArgumentOutOfRangeException(nameof(accountType), accountType, "Unsupported account type."),
    };
}
