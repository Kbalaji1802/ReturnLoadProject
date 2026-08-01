using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReturnLoad.Api.Extensions;
using ReturnLoad.Api.Http;
using ReturnLoad.Application.Identity;
using ReturnLoad.Application.UseCases.Tracking;
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
    private readonly ITrackingService _tracking;

    public TripsController(ITripService trips, ITrackingService tracking)
    {
        _trips = trips;
        _tracking = tracking;
    }

    /// <summary>True when the caller is internal staff (may read any trip's tracking).</summary>
    private bool IsPrivileged => Roles.Internal.Any(User.IsInRole);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTripRequest request, CancellationToken cancellationToken)
    {
        var result = await _trips.CreateAsync(request, cancellationToken);
        return result.ToApiResult(HttpContext, "Trip created.");
    }

    /// <summary>All trips (ops/admin console).</summary>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.InternalStaff)]
    public async Task<IActionResult> All(CancellationToken cancellationToken)
    {
        var result = await _trips.ListAllAsync(cancellationToken);
        return result.ToApiResult(HttpContext);
    }

    /// <summary>The authenticated driver's trips — current + history (My Trips).</summary>
    [HttpGet("mine")]
    public async Task<IActionResult> Mine(CancellationToken cancellationToken)
    {
        if (!HttpContext.TryGetUserId(out Guid authUserId))
        {
            return Unauthorized();
        }

        var result = await _trips.ListMineAsync(authUserId, cancellationToken);
        return result.ToApiResult(HttpContext);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await _trips.GetAsync(id, cancellationToken);
        return result.ToApiResult(HttpContext);
    }

    /// <summary>An enriched trip (driver name, vehicle, load) for the admin trip-details page. Staff-only.</summary>
    [HttpGet("{id:guid}/detail")]
    [Authorize(Policy = AuthorizationPolicies.InternalStaff)]
    public async Task<IActionResult> Detail(Guid id, CancellationToken cancellationToken)
    {
        var result = await _trips.GetDetailAsync(id, cancellationToken);
        return result.ToApiResult(HttpContext);
    }

    /// <summary>The trip fulfilling a load — so the load owner can track their shipment.</summary>
    [HttpGet("for-load/{loadId:guid}")]
    public async Task<IActionResult> ForLoad(Guid loadId, CancellationToken cancellationToken)
    {
        if (!HttpContext.TryGetUserId(out Guid authUserId))
        {
            return Unauthorized();
        }

        var result = await _trips.GetForLoadAsync(authUserId, loadId, IsPrivileged, cancellationToken);
        return result.ToApiResult(HttpContext);
    }

    /// <summary>
    /// Advances the trip lifecycle one legal step (Part 5). The caller is authorised against the
    /// trip: the assigned driver runs the driving steps, the load owner confirms the pickup/delivery
    /// gates (the driver may self-advance a gate after the configured wait), staff may override.
    /// </summary>
    [HttpPost("{id:guid}/status/{target}")]
    public async Task<IActionResult> Advance(Guid id, TripStatus target, CancellationToken cancellationToken)
    {
        if (!HttpContext.TryGetUserId(out Guid authUserId))
        {
            return Unauthorized();
        }

        var result = await _trips.AdvanceAsync(authUserId, id, target, IsPrivileged, cancellationToken);
        return result.ToApiResult(HttpContext, $"Trip {target}.");
    }

    /// <summary>The assigned driver uploads a live location point for an active trip (M6).</summary>
    [HttpPost("{id:guid}/location")]
    public async Task<IActionResult> RecordLocation(Guid id, [FromBody] RecordLocationRequest request, CancellationToken cancellationToken)
    {
        if (!HttpContext.TryGetUserId(out Guid authUserId))
        {
            return Unauthorized();
        }

        var result = await _tracking.RecordLocationAsync(authUserId, id, request, cancellationToken);
        return result.ToApiResult(HttpContext, "Location recorded.");
    }

    /// <summary>The trip's tracking breadcrumb (driver / load owner / staff).</summary>
    [HttpGet("{id:guid}/tracking")]
    public async Task<IActionResult> GetTracking(Guid id, CancellationToken cancellationToken)
    {
        if (!HttpContext.TryGetUserId(out Guid authUserId))
        {
            return Unauthorized();
        }

        var result = await _tracking.GetHistoryAsync(authUserId, id, IsPrivileged, cancellationToken);
        return result.ToApiResult(HttpContext);
    }

    /// <summary>The live position + distance remaining + ETA (owner/admin "Where is my truck?").</summary>
    [HttpGet("{id:guid}/tracking/live")]
    public async Task<IActionResult> GetLive(Guid id, CancellationToken cancellationToken)
    {
        if (!HttpContext.TryGetUserId(out Guid authUserId))
        {
            return Unauthorized();
        }

        var result = await _tracking.GetLiveAsync(authUserId, id, IsPrivileged, cancellationToken);
        return result.ToApiResult(HttpContext);
    }

    /// <summary>Trip analytics: distance, duration, average/max speed, idle time.</summary>
    [HttpGet("{id:guid}/tracking/summary")]
    public async Task<IActionResult> GetSummary(Guid id, CancellationToken cancellationToken)
    {
        if (!HttpContext.TryGetUserId(out Guid authUserId))
        {
            return Unauthorized();
        }

        var result = await _tracking.GetSummaryAsync(authUserId, id, IsPrivileged, cancellationToken);
        return result.ToApiResult(HttpContext);
    }
}
