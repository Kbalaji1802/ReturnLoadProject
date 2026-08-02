namespace ReturnLoad.Application.Identity;

/// <summary>
/// Registration input. Email + password are required; phone is optional (no OTP yet).
/// <paramref name="AccountType"/> selects the marketplace side (Driver or Load Owner) and
/// determines the role granted at sign-up (M4.2, ADR-0017) — the public can never self-assign
/// internal/admin roles. Defaults to <see cref="Identity.AccountType.Driver"/> when omitted.
/// </summary>
/// <param name="FullName">
/// Supplied together with <paramref name="PhoneNumber"/>, this creates the account's
/// <c>UserProfile</c> during registration, so a new load owner can post a load immediately
/// rather than hitting a "Complete your profile" gate with no form behind it. Optional to keep
/// the contract backwards-compatible; clients that omit it can create the profile later via
/// <c>POST /profile</c>.
/// </param>
public sealed record RegisterRequest(
    string Email, string Password, string? PhoneNumber, string? DeviceId,
    AccountType AccountType = AccountType.Driver, string? FullName = null);

/// <summary>Login input.</summary>
public sealed record LoginRequest(string Email, string Password, string? DeviceId);

/// <summary>Exchange a refresh token for a fresh token pair.</summary>
public sealed record RefreshRequest(string RefreshToken, string? DeviceId);

/// <summary>Revoke a single session (this device).</summary>
public sealed record LogoutRequest(string RefreshToken);

/// <summary>
/// The token pair returned on register / login / refresh. The access token is a JWT;
/// the refresh token is opaque and single-use (rotated on each refresh). Transported in
/// the response body (ADR-0013) — the client stores them securely.
/// </summary>
public sealed record AuthTokens(
    string AccessToken,
    string TokenType,
    int ExpiresInSeconds,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);
