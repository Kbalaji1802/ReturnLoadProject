namespace ReturnLoad.Application.Identity;

/// <summary>Registration input. Email + password are required; phone is optional (no OTP yet).</summary>
public sealed record RegisterRequest(string Email, string Password, string? PhoneNumber, string? DeviceId);

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
