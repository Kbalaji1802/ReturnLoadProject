using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ReturnLoad.Shared.Api;

namespace ReturnLoad.Api.Controllers;

/// <summary>
/// Human-friendly, versioned health endpoint. Machine probes for orchestrators
/// live at <c>/health/live</c> (liveness) and <c>/health/ready</c> (readiness).
/// This is the ONLY endpoint exposed in the foundation (no business APIs yet).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/health")]
public sealed class HealthController : ControllerBase
{
    /// <summary>Reports that the API process is up and serving requests.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<HealthStatusResponse>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<HealthStatusResponse>> Get()
    {
        HealthStatusResponse status = new(
            Status: "Healthy",
            Service: "ReturnLoad.Api",
            TimestampUtc: DateTimeOffset.UtcNow);

        return Ok(ApiResponse<HealthStatusResponse>.Ok(status));
    }
}

/// <summary>The payload returned by <see cref="HealthController"/>.</summary>
public sealed record HealthStatusResponse(string Status, string Service, DateTimeOffset TimestampUtc);
