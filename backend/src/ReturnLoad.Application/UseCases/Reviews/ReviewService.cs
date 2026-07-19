using ReturnLoad.Application.Abstractions.Persistence;
using ReturnLoad.Application.UseCases.Notifications;
using ReturnLoad.Domain.Identity;
using ReturnLoad.Domain.Loads;
using ReturnLoad.Domain.Reviews;
using ReturnLoad.Domain.Trips;
using ReturnLoad.Shared.Results;

namespace ReturnLoad.Application.UseCases.Reviews;

public sealed record SubmitReviewRequest(int Stars, string? Comment);

public sealed record ReviewView(
    Guid Id, Guid TripId, Guid AuthorUserProfileId, Guid SubjectUserProfileId,
    int Stars, string? Comment, DateTimeOffset CreatedAtUtc);

/// <summary>A user's aggregate reputation: average stars over N reviews.</summary>
public sealed record RatingSummary(double Average, int Count);

/// <summary>
/// Post-trip reviews (Business Bible §8). Either party on a <b>completed</b> trip reviews the
/// other exactly once; the average feeds trust surfaces and the matching ranking. Author and
/// subject are derived from the trip (driver ↔ load owner) — the client never asserts who it is.
/// </summary>
public interface IReviewService
{
    Task<Result<Guid>> SubmitAsync(Guid authUserId, Guid tripId, SubmitReviewRequest request, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ReviewView>>> ListForTripAsync(Guid tripId, CancellationToken cancellationToken = default);

    /// <summary>Average rating + count for a user profile (subject of reviews).</summary>
    Task<RatingSummary> GetSummaryAsync(Guid subjectUserProfileId, CancellationToken cancellationToken = default);
}

internal sealed class ReviewService : IReviewService
{
    private readonly IRepository<Review> _reviews;
    private readonly IRepository<Trip> _trips;
    private readonly IRepository<UserProfile> _users;
    private readonly IRepository<DriverProfile> _drivers;
    private readonly IRepository<Load> _loads;
    private readonly INotificationService _notify;
    private readonly IUnitOfWork _uow;

    public ReviewService(
        IRepository<Review> reviews,
        IRepository<Trip> trips,
        IRepository<UserProfile> users,
        IRepository<DriverProfile> drivers,
        IRepository<Load> loads,
        INotificationService notify,
        IUnitOfWork uow)
    {
        _reviews = reviews;
        _trips = trips;
        _users = users;
        _drivers = drivers;
        _loads = loads;
        _notify = notify;
        _uow = uow;
    }

    public async Task<Result<Guid>> SubmitAsync(Guid authUserId, Guid tripId, SubmitReviewRequest request, CancellationToken cancellationToken = default)
    {
        Trip? trip = await _trips.GetByIdAsync(tripId, cancellationToken);
        if (trip is null)
        {
            return Error.NotFound("Trip not found.");
        }

        if (trip.Status != TripStatus.Completed)
        {
            return Error.Conflict("You can review only after the trip is completed.");
        }

        UserProfile? author = (await _users.ListAsync(u => u.AuthUserId == authUserId, cancellationToken)).FirstOrDefault();
        if (author is null)
        {
            return Error.Validation("Complete your profile first.");
        }

        // Derive the subject: driver reviews the load owner, or the load owner reviews the driver.
        DriverProfile? driver = await _drivers.GetByIdAsync(trip.DriverProfileId, cancellationToken);
        Load? load = trip.LoadId is Guid loadId ? await _loads.GetByIdAsync(loadId, cancellationToken) : null;

        Guid? subjectId = null;
        if (driver is not null && driver.UserProfileId == author.Id)
        {
            subjectId = load?.ShipperId; // driver -> owner
        }
        else if (load is not null && load.ShipperId == author.Id)
        {
            subjectId = driver?.UserProfileId; // owner -> driver
        }

        if (subjectId is not Guid subject || subject == Guid.Empty)
        {
            return Error.Unauthorized("Only the trip's driver or load owner can leave a review.");
        }

        bool already = await _reviews.ExistsAsync(r => r.TripId == tripId && r.AuthorUserProfileId == author.Id, cancellationToken);
        if (already)
        {
            return Error.Conflict("You have already reviewed this trip.");
        }

        Review review = Review.Leave(tripId, author.Id, subject, Rating.Of(request.Stars), request.Comment);
        await _reviews.AddAsync(review, cancellationToken);
        await _notify.NotifyUserAsync(subject, "New review", $"You received a {request.Stars}★ review for a completed trip.", cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return review.Id;
    }

    public async Task<Result<IReadOnlyList<ReviewView>>> ListForTripAsync(Guid tripId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Review> reviews = await _reviews.ListAsync(r => r.TripId == tripId, cancellationToken);
        return Result<IReadOnlyList<ReviewView>>.Success(reviews.Select(Map).ToList());
    }

    public async Task<RatingSummary> GetSummaryAsync(Guid subjectUserProfileId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Review> reviews = await _reviews.ListAsync(r => r.SubjectUserProfileId == subjectUserProfileId, cancellationToken);
        if (reviews.Count == 0)
        {
            return new RatingSummary(0, 0);
        }

        double average = reviews.Average(r => r.Rating.Stars);
        return new RatingSummary(Math.Round(average, 2), reviews.Count);
    }

    private static ReviewView Map(Review r) =>
        new(r.Id, r.TripId, r.AuthorUserProfileId, r.SubjectUserProfileId, r.Rating.Stars, r.Comment, r.CreatedAtUtc);
}
