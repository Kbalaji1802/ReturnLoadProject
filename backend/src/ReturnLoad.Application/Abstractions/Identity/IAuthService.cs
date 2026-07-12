using ReturnLoad.Application.Identity;
using ReturnLoad.Shared.Results;

namespace ReturnLoad.Application.Abstractions.Identity;

/// <summary>
/// The authentication core (M2, ADR-0013). Orchestrates the identity store and token
/// services behind the M1 <c>Result</c> pattern so the API layer stays thin and every
/// outcome maps cleanly to the standard envelope (ADR-0008). Implemented in Infrastructure
/// (it depends on ASP.NET Core Identity). <b>No</b> OTP, email verification, or document
/// verification here — those plug in later.
/// </summary>
public interface IAuthService
{
    /// <summary>Creates a new account and returns an initial token pair.</summary>
    Task<Result<AuthTokens>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);

    /// <summary>Validates credentials and returns a token pair; applies lockout on repeated failure.</summary>
    Task<Result<AuthTokens>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>Rotates a refresh token, returning a fresh pair; detects and punishes reuse.</summary>
    Task<Result<AuthTokens>> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default);

    /// <summary>Revokes the refresh token for the current device (single-session logout).</summary>
    Task<Result> LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default);

    /// <summary>Revokes every session for the user and invalidates outstanding access tokens.</summary>
    Task<Result> LogoutAllAsync(Guid userId, CancellationToken cancellationToken = default);
}
