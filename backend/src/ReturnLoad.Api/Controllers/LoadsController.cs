using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReturnLoad.Api.Extensions;
using ReturnLoad.Api.Http;
using ReturnLoad.Application.Identity;
using ReturnLoad.Application.UseCases.Loads;

namespace ReturnLoad.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/loads")]
[Authorize]
public sealed class LoadsController : ControllerBase
{
    private readonly ILoadService _loads;

    public LoadsController(ILoadService loads) => _loads = loads;

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

    /// <summary>Browse available (posted) loads.</summary>
    [HttpGet("available")]
    public async Task<IActionResult> Available(CancellationToken cancellationToken)
    {
        var result = await _loads.BrowseAvailableAsync(cancellationToken);
        return result.ToApiResult(HttpContext);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await _loads.GetAsync(id, cancellationToken);
        return result.ToApiResult(HttpContext);
    }

    /// <summary>Accept an available load (matches + books it).</summary>
    [HttpPost("{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id, CancellationToken cancellationToken)
    {
        var result = await _loads.AcceptAsync(id, cancellationToken);
        return result.ToApiResult(HttpContext, "Load accepted.");
    }
}
