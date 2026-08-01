using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReturnLoad.Api.Extensions;
using ReturnLoad.Api.Http;
using ReturnLoad.Application.UseCases.Reviews;

namespace ReturnLoad.Api.Controllers;

/// <summary>
/// Post-trip reviews (Business Bible §8). Either party on a completed trip reviews the other once.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/reviews")]
[Authorize]
public sealed class ReviewsController : ControllerBase
{
    private readonly IReviewService _reviews;

    public ReviewsController(IReviewService reviews) => _reviews = reviews;

    /// <summary>Leave a review for the counterparty on a completed trip.</summary>
    [HttpPost("trip/{tripId:guid}")]
    public async Task<IActionResult> Submit(Guid tripId, [FromBody] SubmitReviewRequest request, CancellationToken cancellationToken)
    {
        if (!HttpContext.TryGetUserId(out Guid authUserId))
        {
            return Unauthorized();
        }

        var result = await _reviews.SubmitAsync(authUserId, tripId, request, cancellationToken);
        return result.ToApiResult(HttpContext, "Review submitted.");
    }

    /// <summary>Reviews left on a trip.</summary>
    [HttpGet("trip/{tripId:guid}")]
    public async Task<IActionResult> ForTrip(Guid tripId, CancellationToken cancellationToken)
    {
        var result = await _reviews.ListForTripAsync(tripId, cancellationToken);
        return result.ToApiResult(HttpContext);
    }

    /// <summary>Reviews written about a user (newest first) — for the admin details pages.</summary>
    [HttpGet("for-user/{userProfileId:guid}")]
    public async Task<IActionResult> ForUser(Guid userProfileId, CancellationToken cancellationToken)
    {
        var result = await _reviews.ListForSubjectAsync(userProfileId, cancellationToken);
        return result.ToApiResult(HttpContext);
    }

    /// <summary>A user's average rating + review count.</summary>
    [HttpGet("summary/{userProfileId:guid}")]
    public async Task<IActionResult> Summary(Guid userProfileId, CancellationToken cancellationToken)
    {
        RatingSummary summary = await _reviews.GetSummaryAsync(userProfileId, cancellationToken);
        return Ok(summary);
    }
}
