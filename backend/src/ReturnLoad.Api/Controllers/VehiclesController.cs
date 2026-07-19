using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReturnLoad.Api.Extensions;
using ReturnLoad.Api.Http;
using ReturnLoad.Application.Identity;
using ReturnLoad.Application.UseCases.Onboarding;
using ReturnLoad.Domain.Fleet;

namespace ReturnLoad.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/vehicles")]
[Authorize]
public sealed class VehiclesController : ControllerBase
{
    private readonly IVehicleService _vehicles;

    public VehiclesController(IVehicleService vehicles) => _vehicles = vehicles;

    /// <summary>A carrier registers a vehicle against a carrier id (fleet management).</summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.CanManageCarrierFleet)]
    public async Task<IActionResult> Register([FromBody] RegisterVehicleRequest request, CancellationToken cancellationToken)
    {
        var result = await _vehicles.RegisterAsync(request, cancellationToken);
        return result.ToApiResult(HttpContext, "Vehicle registered.");
    }

    /// <summary>A driver adds a vehicle to their own fleet (carrier resolved from the token).</summary>
    [HttpPost("mine")]
    public async Task<IActionResult> RegisterMine([FromBody] RegisterDriverVehicleRequest request, CancellationToken cancellationToken)
    {
        if (!HttpContext.TryGetUserId(out Guid authUserId))
        {
            return Unauthorized();
        }

        var result = await _vehicles.RegisterForDriverAsync(authUserId, request, cancellationToken);
        return result.ToApiResult(HttpContext, "Vehicle added. It will be reviewed by our team.");
    }

    /// <summary>The authenticated driver's fleet vehicles.</summary>
    [HttpGet("mine")]
    public async Task<IActionResult> Mine(CancellationToken cancellationToken)
    {
        if (!HttpContext.TryGetUserId(out Guid authUserId))
        {
            return Unauthorized();
        }

        var result = await _vehicles.ListForDriverAsync(authUserId, cancellationToken);
        return result.ToApiResult(HttpContext);
    }

    /// <summary>Vehicles for the ops console; <c>?status=Draft</c> is the pending-approval queue.</summary>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.InternalStaff)]
    public async Task<IActionResult> List([FromQuery] VehicleStatus? status, CancellationToken cancellationToken)
    {
        var result = await _vehicles.ListByStatusAsync(status, cancellationToken);
        return result.ToApiResult(HttpContext);
    }

    /// <summary>Ops approves a vehicle for matching once its mandatory documents are verified.</summary>
    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = AuthorizationPolicies.InternalStaff)]
    public async Task<IActionResult> Activate(Guid id, [FromQuery] bool documentsValid, CancellationToken cancellationToken)
    {
        var result = await _vehicles.ActivateAsync(id, documentsValid, cancellationToken);
        return result.ToApiResult(HttpContext, "Vehicle activated.");
    }
}
