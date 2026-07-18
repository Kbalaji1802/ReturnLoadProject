using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReturnLoad.Application.Abstractions.Geo;

namespace ReturnLoad.Api.Controllers;

/// <summary>
/// Location intelligence (M4.2): address autocomplete and road distance/ETA, backed by the
/// pluggable <see cref="ILocationSearchService"/> / <see cref="IRouteService"/> (OpenStreetMap
/// in dev). Clients use <c>search</c> to resolve a typed place to coordinates + structured
/// address, then <c>route</c> to preview distance — the platform derives distance/ETA, the user
/// never calculates them.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/geo")]
[Authorize]
public sealed class GeoController : ControllerBase
{
    private readonly ILocationSearchService _search;
    private readonly IRouteService _route;

    public GeoController(ILocationSearchService search, IRouteService route)
    {
        _search = search;
        _route = route;
    }

    /// <summary>Autocomplete: resolve a typed query to ranked, geocoded places.</summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, CancellationToken cancellationToken)
    {
        IReadOnlyList<PlaceResult> places = await _search.SearchAsync(q, cancellationToken);
        return Ok(places);
    }

    /// <summary>Road distance (km) + estimated duration between two points; 404 if no route.</summary>
    [HttpGet("route")]
    public async Task<IActionResult> Route(
        [FromQuery] double originLat,
        [FromQuery] double originLng,
        [FromQuery] double destLat,
        [FromQuery] double destLng,
        CancellationToken cancellationToken)
    {
        RouteResult? route = await _route.GetRouteAsync(originLat, originLng, destLat, destLng, cancellationToken);
        return route is null ? NotFound() : Ok(route);
    }
}
