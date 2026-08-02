using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReturnLoad.Api.Extensions;
using ReturnLoad.Api.Http;
using ReturnLoad.Application.UseCases.Profiles;

namespace ReturnLoad.Api.Controllers;

/// <summary>
/// The signed-in account's own platform profile. Drivers get one as a side effect of
/// <c>drivers/register</c>; this is the equivalent for everyone else — notably load owners,
/// who otherwise cannot satisfy the "Complete your profile" gate on posting a load.
/// Role-agnostic by design: it creates the profile for whoever is authenticated.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/profile")]
[Authorize]
public sealed class ProfileController : ControllerBase
{
    private readonly IUserProfileService _profiles;

    public ProfileController(IUserProfileService profiles) => _profiles = profiles;

    /// <summary>The caller's own profile. 404 when they have not created one yet — the client
    /// uses that to decide whether to show the profile form.</summary>
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        if (!HttpContext.TryGetUserId(out Guid authUserId))
        {
            return Unauthorized();
        }

        var result = await _profiles.GetMineAsync(authUserId, cancellationToken);
        return result.ToApiResult(HttpContext);
    }

    /// <summary>Creates the caller's profile. Conflicts if one already exists.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateUserProfileRequest request, CancellationToken cancellationToken)
    {
        if (!HttpContext.TryGetUserId(out Guid authUserId))
        {
            return Unauthorized();
        }

        var result = await _profiles.CreateAsync(authUserId, request, cancellationToken);
        return result.ToApiResult(HttpContext, "Profile created.");
    }
}
