using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReturnLoad.Api.Extensions;
using ReturnLoad.Api.Http;
using ReturnLoad.Application.Identity;
using ReturnLoad.Application.UseCases.Loads;
using ReturnLoad.Application.UseCases.Matching;

namespace ReturnLoad.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/loads")]
[Authorize]
public sealed class LoadsController : ControllerBase
{
    private readonly ILoadService _loads;
    private readonly IMatchingService _matching;

    public LoadsController(ILoadService loads, IMatchingService matching)
    {
        _loads = loads;
        _matching = matching;
    }

    /// <summary>Shipper posts a load.</summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.CanPostLoads)]
    public async Task<IActionResult> Post([FromBody] PostLoadRequest request, CancellationToken cancellationToken)
    {
        if (!HttpContext.TryGetUserId(out Guid authUserId))
        {
            return Unauthorized();
        }

        var result = await _loads.PostAsync(authUserId, request, cancellationToken);
        return result.ToApiResult(HttpContext, "Load posted.");
    }

    /// <summary>The full posted-loads board (internal ops view). A driver uses <c>matched</c>.</summary>
    [HttpGet("available")]
    [Authorize(Policy = AuthorizationPolicies.InternalStaff)]
    public async Task<IActionResult> Available(CancellationToken cancellationToken)
    {
        var result = await _loads.BrowseAvailableAsync(cancellationToken);
        return result.ToApiResult(HttpContext);
    }

    /// <summary>
    /// The posted loads the authenticated driver's fleet can carry, <b>ranked best-first</b> with
    /// a score + reason (M5). Optional <c>lat</c>/<c>lng</c> = the driver's current location, which
    /// makes pickup proximity drive the ranking. Zero matches is a valid result.
    /// </summary>
    [HttpGet("matched")]
    public async Task<IActionResult> Matched([FromQuery] double? lat, [FromQuery] double? lng, CancellationToken cancellationToken)
    {
        if (!HttpContext.TryGetUserId(out Guid authUserId))
        {
            return Unauthorized();
        }

        var result = await _matching.FindCompatibleLoadsAsync(authUserId, lat, lng, cancellationToken);
        return result.ToApiResult(HttpContext);
    }

    /// <summary>The Load Owner's own loads across every status (My Loads).</summary>
    [HttpGet("mine")]
    [Authorize(Policy = AuthorizationPolicies.CanPostLoads)]
    public async Task<IActionResult> Mine(CancellationToken cancellationToken)
    {
        if (!HttpContext.TryGetUserId(out Guid authUserId))
        {
            return Unauthorized();
        }

        var result = await _loads.ListMineAsync(authUserId, cancellationToken);
        return result.ToApiResult(HttpContext);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await _loads.GetAsync(id, cancellationToken);
        return result.ToApiResult(HttpContext);
    }

}
