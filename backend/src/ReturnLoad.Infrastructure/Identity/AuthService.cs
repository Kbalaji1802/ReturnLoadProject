using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using ReturnLoad.Application.Abstractions.Identity;
using ReturnLoad.Application.Identity;
using ReturnLoad.Infrastructure.Identity.Tokens;
using ReturnLoad.Shared.Results;

namespace ReturnLoad.Infrastructure.Identity;

/// <summary>
/// The authentication core (ADR-0013): registration, login (with lockout), refresh-token
/// rotation, and logout — orchestrating ASP.NET Core Identity's <see cref="UserManager{T}"/>
/// with our JWT + refresh services. Returns <see cref="Result"/> so the API maps every
/// outcome to the standard envelope (ADR-0008). Auth events are logged as security events;
/// persistent AuditLog rows arrive with the domain model (T-002).
/// </summary>
internal sealed class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly ITokenService _tokens;
    private readonly IRefreshTokenService _refreshTokens;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<ApplicationUser> users,
        ITokenService tokens,
        IRefreshTokenService refreshTokens,
        ILogger<AuthService> logger)
    {
        _users = users;
        _tokens = tokens;
        _refreshTokens = refreshTokens;
        _logger = logger;
    }

    public async Task<Result<AuthTokens>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        string email = request.Email.Trim();
        ApplicationUser user = new()
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            PhoneNumber = request.PhoneNumber,
            // No email/phone verification yet (later milestone): new accounts are Active so
            // they can authenticate. When verification ships, this becomes PendingVerification.
            AccountStatus = AccountStatus.Active,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        IdentityResult result = await _users.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return MapIdentityFailure(result);
        }

        LogSecurity("AccountRegistered", "Account registered for {Email}", email);
        return await IssueTokensAsync(user, request.DeviceId, cancellationToken);
    }

    public async Task<Result<AuthTokens>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        string email = request.Email.Trim();
        ApplicationUser? user = await _users.FindByEmailAsync(email);

        // Uniform "invalid credentials" to avoid user enumeration (design review §5).
        if (user is null)
        {
            LogSecurity("LoginFailed", "Login failed (unknown account) for {Email}", email);
            return Error.Unauthorized("Invalid email or password.");
        }

        if (await _users.IsLockedOutAsync(user))
        {
            LogSecurity("LoginBlocked", "Login blocked (locked out) for {Email}", email);
            return Error.Unauthorized("Account is temporarily locked. Please try again later.");
        }

        if (user.AccountStatus is AccountStatus.Suspended or AccountStatus.Disabled)
        {
            LogSecurity("LoginBlocked", "Login blocked (status {Status}) for {Email}", user.AccountStatus, email);
            return Error.Unauthorized("This account cannot sign in.");
        }

        if (!await _users.CheckPasswordAsync(user, request.Password))
        {
            await _users.AccessFailedAsync(user); // increments; locks at the configured threshold
            LogSecurity("LoginFailed", "Login failed (bad password) for {Email}", email);
            return Error.Unauthorized("Invalid email or password.");
        }

        await _users.ResetAccessFailedCountAsync(user); // reset the counter on success (ADR-0013)
        LogSecurity("LoginSucceeded", "Login succeeded for {Email}", email);
        return await IssueTokensAsync(user, request.DeviceId, cancellationToken);
    }

    public async Task<Result<AuthTokens>> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default)
    {
        Result<RefreshRotation> rotation = await _refreshTokens.RotateAsync(request.RefreshToken, request.DeviceId, cancellationToken);
        if (rotation.IsFailure)
        {
            LogSecurity("TokenRefreshFailed", "Refresh failed: {Reason}", rotation.Error.Message);
            return rotation.Error;
        }

        ApplicationUser? user = await _users.FindByIdAsync(rotation.Value.UserId.ToString());
        if (user is null || user.AccountStatus is AccountStatus.Suspended or AccountStatus.Disabled)
        {
            return Error.Unauthorized("This account cannot sign in.");
        }

        AccessToken access = await CreateAccessTokenAsync(user, request.DeviceId);
        LogSecurity("TokenRefreshed", "Access token refreshed for user {UserId}", user.Id);
        return ToAuthTokens(access, rotation.Value.NewToken);
    }

    public async Task<Result> LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default)
    {
        await _refreshTokens.RevokeAsync(request.RefreshToken, cancellationToken);
        return Result.Success();
    }

    public async Task<Result> LogoutAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await _refreshTokens.RevokeAllForUserAsync(userId, cancellationToken);

        // Bump permissionsVersion + security stamp so outstanding access tokens are treated
        // as stale once version enforcement is active, and Identity-issued tokens invalidate.
        ApplicationUser? user = await _users.FindByIdAsync(userId.ToString());
        if (user is not null)
        {
            user.PermissionsVersion++;
            user.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await _users.UpdateAsync(user);
            await _users.UpdateSecurityStampAsync(user);
        }

        LogSecurity("LoggedOutAllDevices", "All sessions revoked for user {UserId}", userId);
        return Result.Success();
    }

    private async Task<Result<AuthTokens>> IssueTokensAsync(ApplicationUser user, string? deviceId, CancellationToken cancellationToken)
    {
        AccessToken access = await CreateAccessTokenAsync(user, deviceId);
        IssuedRefreshToken refresh = await _refreshTokens.IssueAsync(user.Id, deviceId, cancellationToken);
        return ToAuthTokens(access, refresh);
    }

    private async Task<AccessToken> CreateAccessTokenAsync(ApplicationUser user, string? deviceId)
    {
        IList<string> roles = await _users.GetRolesAsync(user);
        AccessTokenDescriptor descriptor = new(user.Id, user.Email, [.. roles], user.PermissionsVersion, deviceId);
        return _tokens.CreateAccessToken(descriptor);
    }

    private static Result<AuthTokens> ToAuthTokens(AccessToken access, IssuedRefreshToken refresh)
    {
        int expiresIn = Math.Max(0, (int)(access.ExpiresAt - DateTimeOffset.UtcNow).TotalSeconds);
        return new AuthTokens(access.Token, "Bearer", expiresIn, refresh.RawToken, refresh.ExpiresAt);
    }

    private static Result<AuthTokens> MapIdentityFailure(IdentityResult result)
    {
        if (result.Errors.Any(e => e.Code.Contains("Duplicate", StringComparison.OrdinalIgnoreCase)))
        {
            return Error.Conflict("An account with this email already exists.");
        }

        string message = string.Join(" ", result.Errors.Select(e => e.Description));
        return Error.Validation(string.IsNullOrWhiteSpace(message) ? "Registration failed." : message);
    }

    private void LogSecurity(string securityEvent, string messageTemplate, params object?[] args)
    {
        using (_logger.BeginScope(new Dictionary<string, object> { ["SecurityEvent"] = securityEvent }))
        {
            _logger.LogInformation(messageTemplate, args);
        }
    }
}
