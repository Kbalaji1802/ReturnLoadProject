using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReturnLoad.Api.Extensions;
using ReturnLoad.Application.UseCases.Onboarding;

namespace ReturnLoad.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/carriers")]
[Authorize]
public sealed class CarriersController : ControllerBase
{
    private readonly ICarrierService _carriers;

    public CarriersController(ICarrierService carriers) => _carriers = carriers;

    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterCarrierRequest request, CancellationToken cancellationToken)
    {
        var result = await _carriers.RegisterAsync(request, cancellationToken);
        return result.ToApiResult(HttpContext, "Carrier registered.");
    }
}
