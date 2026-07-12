namespace ReturnLoad.Infrastructure.Identity.Tokens;

/// <summary>Builds signed JWT access tokens from a descriptor (ADR-0013 claim set).</summary>
internal interface ITokenService
{
    AccessToken CreateAccessToken(AccessTokenDescriptor descriptor);
}

/// <summary>Inputs for an access token.</summary>
public sealed record AccessTokenDescriptor(
    Guid UserId,
    string? Email,
    IReadOnlyCollection<string> Roles,
    int PermissionsVersion,
    string? DeviceId);

/// <summary>A signed access token and the instant it expires.</summary>
public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);
