using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ReturnLoad.Api.Extensions;
using ReturnLoad.Api.Http;
using ReturnLoad.Application.Abstractions.Identity;
using ReturnLoad.Application.Identity;
using ReturnLoad.Shared.Api;

namespace ReturnLoad.Api.Controllers;

/// <summary>
/// Authentication endpoints (M2, ADR-0013): register, login, refresh, logout, logout-all.
/// Every response is the standard envelope (ADR-0008); requests are validated (400) and
/// credential failures return 401 via <c>ResultExtensions</c>. High-risk endpoints carry
/// the M1.5 <c>sensitive</c> rate-limit policy. No OTP / email / document verification.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    /// <summary>Creates a new account and returns an initial token pair.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(SecurityExtensions.SensitiveRateLimitPolicy)]
    [ProducesResponseType(typeof(ApiResponse<AuthTokens>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        [FromServices] IValidator<RegisterRequest> validator,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await _auth.RegisterAsync(request, cancellationToken);
        return result.ToApiResult(HttpContext, "Account created.");
    }

    /// <summary>Authenticates a user and returns a token pair.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(SecurityExtensions.SensitiveRateLimitPolicy)]
    [ProducesResponseType(typeof(ApiResponse<AuthTokens>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        [FromServices] IValidator<LoginRequest> validator,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await _auth.LoginAsync(request, cancellationToken);
        return result.ToApiResult(HttpContext, "Signed in.");
    }

    /// <summary>Exchanges a refresh token for a new token pair (rotating).</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(SecurityExtensions.SensitiveRateLimitPolicy)]
    [ProducesResponseType(typeof(ApiResponse<AuthTokens>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshRequest request,
        [FromServices] IValidator<RefreshRequest> validator,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await _auth.RefreshAsync(request, cancellationToken);
        return result.ToApiResult(HttpContext, "Token refreshed.");
    }

    /// <summary>Revokes the refresh token for this device.</summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken cancellationToken)
    {
        var result = await _auth.LogoutAsync(request, cancellationToken);
        return result.ToApiResult(HttpContext, "Signed out.");
    }

    /// <summary>Revokes every session for the current user.</summary>
    [HttpPost("logout-all")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> LogoutAll(CancellationToken cancellationToken)
    {
        if (!HttpContext.TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _auth.LogoutAllAsync(userId, cancellationToken);
        return result.ToApiResult(HttpContext, "Signed out of all devices.");
    }
}
