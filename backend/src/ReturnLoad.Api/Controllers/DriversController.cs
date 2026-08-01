using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReturnLoad.Api.Extensions;
using ReturnLoad.Api.Http;
using ReturnLoad.Application.Identity;
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

    /// <summary>The caller's own driver profile (id + verification status).</summary>
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        if (!HttpContext.TryGetUserId(out Guid authUserId))
        {
            return Unauthorized();
        }

        var result = await _drivers.GetForUserAsync(authUserId, cancellationToken);
        return result.ToApiResult(HttpContext);
    }

    /// <summary>
    /// The caller sets their own operational availability (Part 3): Available / Offline / OnLeave /
    /// VehicleService. Only <c>Available</c> drivers are matched; the change takes effect on the next
    /// match query. <c>Busy</c> is system-managed and rejected here.
    /// </summary>
    [HttpPut("me/availability")]
    public async Task<IActionResult> SetAvailability([FromBody] SetDriverAvailabilityRequest request, CancellationToken cancellationToken)
    {
        if (!HttpContext.TryGetUserId(out Guid authUserId))
        {
            return Unauthorized();
        }

        var result = await _drivers.SetAvailabilityAsync(
            authUserId, request.Availability, request.Latitude, request.Longitude, cancellationToken);
        return result.ToApiResult(HttpContext, "Availability updated.");
    }

    /// <summary>Lists drivers (internal review view). Staff-only — a driver uses <c>me</c>.</summary>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.InternalStaff)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await _drivers.ListAsync(cancellationToken);
        return result.ToApiResult(HttpContext);
    }

    /// <summary>The full driver profile for the admin driver-details page (Part 11). Staff-only.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.InternalStaff)]
    public async Task<IActionResult> Detail(Guid id, CancellationToken cancellationToken)
    {
        var result = await _drivers.GetDetailAsync(id, cancellationToken);
        return result.ToApiResult(HttpContext);
    }
}
