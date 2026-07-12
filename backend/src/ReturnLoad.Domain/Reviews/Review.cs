using ReturnLoad.Domain.Common;

namespace ReturnLoad.Domain.Reviews;

/// <summary>
/// A post-trip review one party leaves for the other, carrying a <see cref="Rating"/> and an
/// optional comment (Business Bible §8: ratings are the post-trip trust signal; Trust &amp;
/// Safety §1 contrasts them with pre-trip verification).
/// <para><b>Invariants:</b> a trip, an author, and a subject are required; you cannot review
/// yourself; the comment is bounded in length.</para>
/// </summary>
public sealed class Review : AggregateRoot<Guid>
{
    public const int MaxCommentLength = 2000;

    private Review(Guid id, Guid tripId, Guid authorUserProfileId, Guid subjectUserProfileId, Rating rating, string? comment)
        : base(id)
    {
        TripId = tripId;
        AuthorUserProfileId = authorUserProfileId;
        SubjectUserProfileId = subjectUserProfileId;
        Rating = rating;
        Comment = comment;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    private Review()
    {
    }

    public Guid TripId { get; }

    public Guid AuthorUserProfileId { get; }

    public Guid SubjectUserProfileId { get; }

    public Rating Rating { get; private set; } = null!;

    public string? Comment { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public static Review Leave(
        Guid tripId,
        Guid authorUserProfileId,
        Guid subjectUserProfileId,
        Rating rating,
        string? comment = null)
    {
        Guard.AgainstDefault(tripId, "Trip id", "review_trip_required");
        Guard.AgainstDefault(authorUserProfileId, "Author id", "review_author_required");
        Guard.AgainstDefault(subjectUserProfileId, "Subject id", "review_subject_required");
        ArgumentNullException.ThrowIfNull(rating);
        Guard.Against(authorUserProfileId == subjectUserProfileId, "You cannot review yourself.", "review_self");
        Guard.Against((comment?.Length ?? 0) > MaxCommentLength, $"Comment cannot exceed {MaxCommentLength} characters.", "review_comment_too_long");

        string? trimmed = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        return new Review(Guid.NewGuid(), tripId, authorUserProfileId, subjectUserProfileId, rating, trimmed);
    }
}
