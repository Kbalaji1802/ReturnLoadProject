using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReturnLoad.Api.Extensions;
using ReturnLoad.Application.Identity;
using ReturnLoad.Application.UseCases.Onboarding;

namespace ReturnLoad.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/vehicles")]
[Authorize(Policy = AuthorizationPolicies.CanManageCarrierFleet)]
public sealed class VehiclesController : ControllerBase
{
    private readonly IVehicleService _vehicles;

    public VehiclesController(IVehicleService vehicles) => _vehicles = vehicles;

    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterVehicleRequest request, CancellationToken cancellationToken)
    {
        var result = await _vehicles.RegisterAsync(request, cancellationToken);
        return result.ToApiResult(HttpContext, "Vehicle registered.");
    }

    /// <summary>Activates a vehicle for matching once its mandatory documents are verified.</summary>
    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, [FromQuery] bool documentsValid, CancellationToken cancellationToken)
    {
        var result = await _vehicles.ActivateAsync(id, documentsValid, cancellationToken);
        return result.ToApiResult(HttpContext, "Vehicle activated.");
    }
}
