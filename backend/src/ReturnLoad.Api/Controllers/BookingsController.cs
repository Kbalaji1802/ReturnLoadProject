using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReturnLoad.Api.Extensions;
using ReturnLoad.Api.Http;
using ReturnLoad.Application.Identity;
using ReturnLoad.Application.UseCases.Bookings;

namespace ReturnLoad.Api.Controllers;

/// <summary>
/// The booking-request workflow (M4.3 Steps 3–4). A driver requests a load; the load owner
/// (Shipper role) decides. Accepting creates the trip and assigns the load.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/bookings")]
[Authorize]
public sealed class BookingsController : ControllerBase
{
    private readonly IBookingService _bookings;

    public BookingsController(IBookingService bookings) => _bookings = bookings;

    /// <summary>A driver requests to carry a load with one of their vehicles.</summary>
    [HttpPost("requests")]
    public async Task<IActionResult> SendRequest([FromBody] CreateBookingRequestBody body, CancellationToken cancellationToken)
    {
        if (!HttpContext.TryGetUserId(out Guid authUserId))
        {
            return Unauthorized();
        }

        var result = await _bookings.RequestAsync(authUserId, body.LoadId, body.VehicleId, cancellationToken);
        return result.ToApiResult(HttpContext, "Request sent to the load owner.");
    }

    /// <summary>The driver's own booking requests.</summary>
    [HttpGet("mine")]
    public async Task<IActionResult> Mine(CancellationToken cancellationToken)
    {
        if (!HttpContext.TryGetUserId(out Guid authUserId))
        {
            return Unauthorized();
        }

        var result = await _bookings.ListMineAsync(authUserId, cancellationToken);
        return result.ToApiResult(HttpContext);
    }

    /// <summary>The requests on a load the caller owns (Load Owner).</summary>
    [HttpGet("for-load/{loadId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.CanPostLoads)]
    public async Task<IActionResult> ForLoad(Guid loadId, CancellationToken cancellationToken)
    {
        if (!HttpContext.TryGetUserId(out Guid authUserId))
        {
            return Unauthorized();
        }

        var result = await _bookings.ListForLoadAsync(authUserId, loadId, cancellationToken);
        return result.ToApiResult(HttpContext);
    }

    /// <summary>Load owner accepts a request — creates the trip and assigns the load.</summary>
    [HttpPost("requests/{id:guid}/accept")]
    [Authorize(Policy = AuthorizationPolicies.CanPostLoads)]
    public async Task<IActionResult> Accept(Guid id, CancellationToken cancellationToken)
    {
        if (!HttpContext.TryGetUserId(out Guid authUserId))
        {
            return Unauthorized();
        }

        var result = await _bookings.AcceptAsync(authUserId, id, cancellationToken);
        return result.ToApiResult(HttpContext, "Request accepted — trip created.");
    }

    /// <summary>Load owner rejects a request.</summary>
    [HttpPost("requests/{id:guid}/reject")]
    [Authorize(Policy = AuthorizationPolicies.CanPostLoads)]
    public async Task<IActionResult> Reject(Guid id, CancellationToken cancellationToken)
    {
        if (!HttpContext.TryGetUserId(out Guid authUserId))
        {
            return Unauthorized();
        }

        var result = await _bookings.RejectAsync(authUserId, id, cancellationToken);
        return result.ToApiResult(HttpContext, "Request rejected.");
    }
}

public sealed record CreateBookingRequestBody(Guid LoadId, Guid VehicleId);
