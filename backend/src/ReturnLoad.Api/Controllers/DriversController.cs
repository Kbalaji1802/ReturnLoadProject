using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReturnLoad.Api.Extensions;
using ReturnLoad.Api.Http;
using ReturnLoad.Application.UseCases.Onboarding;

namespace ReturnLoad.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/drivers")]
[Authorize]
public sealed class DriversController : ControllerBase
{
    private readonly IDriverOnboardingService _drivers;

    public DriversController(IDriverOnboardingService drivers) => _drivers = drivers;

    /// <summary>Registers the current authenticated user as a driver.</summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDriverRequest request, CancellationToken cancellationToken)
    {
        if (!HttpContext.TryGetUserId(out Guid authUserId))
        {
            return Unauthorized();
        }

        var result = await _drivers.RegisterAsync(authUserId, request, cancellationToken);
        return result.ToApiResult(HttpContext, "Driver registered.");
    }
}
