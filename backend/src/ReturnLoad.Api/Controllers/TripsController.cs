using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReturnLoad.Api.Extensions;
using ReturnLoad.Application.UseCases.Trips;
using ReturnLoad.Domain.Trips;

namespace ReturnLoad.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/trips")]
[Authorize]
public sealed class TripsController : ControllerBase
{
    private readonly ITripService _trips;

    public TripsController(ITripService trips) => _trips = trips;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTripRequest request, CancellationToken cancellationToken)
    {
        var result = await _trips.CreateAsync(request, cancellationToken);
        return result.ToApiResult(HttpContext, "Trip created.");
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await _trips.GetAsync(id, cancellationToken);
        return result.ToApiResult(HttpContext);
    }

    /// <summary>Advances the trip lifecycle (assigned → started → in-transit → completed / cancelled).</summary>
    [HttpPost("{id:guid}/status/{target}")]
    public async Task<IActionResult> Advance(Guid id, TripStatus target, CancellationToken cancellationToken)
    {
        var result = await _trips.AdvanceAsync(id, target, cancellationToken);
        return result.ToApiResult(HttpContext, $"Trip {target}.");
    }

    [HttpPost("{id:guid}/tracking")]
    public async Task<IActionResult> RecordTracking(Guid id, [FromBody] RecordTrackingRequest request, CancellationToken cancellationToken)
    {
        var result = await _trips.RecordTrackingAsync(id, request, cancellationToken);
        return result.ToApiResult(HttpContext, "Tracking recorded.");
    }

    [HttpGet("{id:guid}/tracking")]
    public async Task<IActionResult> GetTracking(Guid id, CancellationToken cancellationToken)
    {
        var result = await _trips.GetTrackingAsync(id, cancellationToken);
        return result.ToApiResult(HttpContext);
    }
}
