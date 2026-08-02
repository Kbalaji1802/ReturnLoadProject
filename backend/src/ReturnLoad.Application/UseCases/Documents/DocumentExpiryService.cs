using Microsoft.Extensions.Options;
using ReturnLoad.Application.Abstractions.Persistence;
using ReturnLoad.Application.UseCases.Notifications;
using ReturnLoad.Domain.Documents;
using ReturnLoad.Domain.Fleet;
using ReturnLoad.Domain.Identity;

namespace ReturnLoad.Application.UseCases.Documents;

/// <summary>
/// Tunables for the expiry sweep (RC-2 Part 13). Config-driven per 01_PROJECT_RULES.md §8 — the
/// thresholds and cadence are operational policy, not constants.
/// </summary>
public sealed class DocumentExpiryOptions
{
    public const string SectionName = "DocumentExpiry";

    /// <summary>Days-before-expiry at which to warn. Each fires once per document.</summary>
    public IList<int> ThresholdDays { get; set; } = [30, 15, 7, 1];

    /// <summary>How often the sweep runs.</summary>
    public TimeSpan SweepInterval { get; set; } = TimeSpan.FromHours(6);

    /// <summary>Set false to disable the sweep entirely (e.g. in a worker-less environment).</summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Warns document holders before their compliance documents lapse, and once when they have
/// (RC-2 Part 13). An expired licence, insurance, permit, fitness, pollution certificate or RC
/// makes a driver or vehicle ineligible to be matched (MATCHING_ENGINE.md §2 filters 7–8), so
/// silent expiry takes a truck off the road with no warning.
/// </summary>
public interface IDocumentExpiryService
{
    /// <summary>
    /// Sends any reminders now due, relative to <paramref name="asOf"/>. Idempotent: a threshold
    /// already recorded for a document is skipped, so repeated runs send nothing new.
    /// Returns how many reminders were sent.
    /// </summary>
    Task<int> SweepAsync(DateOnly asOf, CancellationToken cancellationToken = default);
}

internal sealed class DocumentExpiryService : IDocumentExpiryService
{
    private readonly IRepository<Document> _documents;
    private readonly IRepository<DocumentExpiryReminder> _reminders;
    private readonly IRepository<Vehicle> _vehicles;
    private readonly IRepository<Association> _associations;
    private readonly IRepository<DriverProfile> _drivers;
    private readonly INotificationService _notify;
    private readonly DocumentExpiryOptions _options;
    private readonly IUnitOfWork _uow;

    public DocumentExpiryService(
        IRepository<Document> documents,
        IRepository<DocumentExpiryReminder> reminders,
        IRepository<Vehicle> vehicles,
        IRepository<Association> associations,
        IRepository<DriverProfile> drivers,
        INotificationService notify,
        IOptions<DocumentExpiryOptions> options,
        IUnitOfWork uow)
    {
        _documents = documents;
        _reminders = reminders;
        _vehicles = vehicles;
        _associations = associations;
        _drivers = drivers;
        _notify = notify;
        _options = options.Value;
        _uow = uow;
    }

    public async Task<int> SweepAsync(DateOnly asOf, CancellationToken cancellationToken = default)
    {
        // Only live documents matter. An archived one has been superseded by a re-upload, and a
        // rejected one is already blocking the holder for a louder reason than a pending expiry.
        IReadOnlyList<Document> documents = await _documents.ListAsync(
            d => d.Status == DocumentStatus.Active
                && d.VerificationStatus == VerificationStatus.Verified
                && d.ExpiresOn != null,
            cancellationToken);

        if (documents.Count == 0)
        {
            return 0;
        }

        // Widest threshold bounds what could possibly be due, so a document expiring next year is
        // not re-examined on every tick for a year.
        int widest = _options.ThresholdDays.Count == 0 ? 0 : _options.ThresholdDays.Max();
        int sent = 0;

        foreach (Document document in documents)
        {
            DateOnly expiresOn = document.ExpiresOn!.Value;
            int daysRemaining = expiresOn.DayNumber - asOf.DayNumber;
            if (daysRemaining > widest)
            {
                continue;
            }

            int? threshold = ResolveThreshold(daysRemaining);
            if (threshold is not int due)
            {
                continue;
            }

            bool alreadySent = await _reminders.ExistsAsync(
                r => r.DocumentId == document.Id && r.ThresholdDays == due, cancellationToken);
            if (alreadySent)
            {
                continue;
            }

            await NotifyHoldersAsync(document, due, daysRemaining, cancellationToken);
            await _reminders.AddAsync(
                DocumentExpiryReminder.Sent(document.Id, due, DateTimeOffset.UtcNow), cancellationToken);
            sent++;
        }

        if (sent > 0)
        {
            await _uow.SaveChangesAsync(cancellationToken);
        }

        return sent;
    }

    /// <summary>
    /// The threshold a document with <paramref name="daysRemaining"/> left is due for, or null.
    /// <para>
    /// Picks the <b>smallest</b> configured threshold still at or above the days remaining, so a
    /// sweep that misses a day — or a document created inside the window — reports the nearest
    /// accurate warning rather than a stale wider one. Anything past expiry maps to the single
    /// expired notice, which fires once no matter how long it has been lapsed.
    /// </para>
    /// </summary>
    private int? ResolveThreshold(int daysRemaining)
    {
        if (daysRemaining <= 0)
        {
            return DocumentExpiryReminder.ExpiredThreshold;
        }

        int? best = null;
        foreach (int threshold in _options.ThresholdDays)
        {
            if (threshold >= daysRemaining && (best is null || threshold < best))
            {
                best = threshold;
            }
        }

        return best;
    }

    /// <summary>
    /// Routes the warning to whoever can act on it: the driver for a personal document, and every
    /// driver attached to the owning carrier for a vehicle or company document — they are the ones
    /// who lose the ability to be matched when it lapses.
    /// </summary>
    private async Task NotifyHoldersAsync(
        Document document, int threshold, int daysRemaining, CancellationToken cancellationToken)
    {
        string subject = threshold == DocumentExpiryReminder.ExpiredThreshold
            ? $"{document.Type} has expired"
            : $"{document.Type} expires in {daysRemaining} day{(daysRemaining == 1 ? string.Empty : "s")}";

        string body = threshold == DocumentExpiryReminder.ExpiredThreshold
            ? $"Your {document.Type} expired on {document.ExpiresOn:dd MMM yyyy}. Upload a current copy — you cannot be matched to loads until it is verified again."
            : $"Your {document.Type} expires on {document.ExpiresOn:dd MMM yyyy}. Upload a renewed copy before then to keep receiving loads.";

        switch (document.OwnerType)
        {
            case DocumentOwnerType.Driver:
                await _notify.NotifyDriverAsync(document.OwnerId, subject, body, cancellationToken);
                break;

            case DocumentOwnerType.Vehicle:
                Vehicle? vehicle = await _vehicles.GetByIdAsync(document.OwnerId, cancellationToken);
                if (vehicle is not null)
                {
                    await NotifyCarrierDriversAsync(vehicle.CarrierId, subject, body, cancellationToken);
                }

                break;

            case DocumentOwnerType.Carrier:
                await NotifyCarrierDriversAsync(document.OwnerId, subject, body, cancellationToken);
                break;
        }
    }

    private async Task NotifyCarrierDriversAsync(
        Guid carrierId, string subject, string body, CancellationToken cancellationToken)
    {
        IReadOnlyList<Association> members = await _associations.ListAsync(
            a => a.CarrierId == carrierId
                && a.Role == AssociationRole.Driver
                && a.Status != AssociationStatus.Revoked,
            cancellationToken);

        foreach (Association member in members)
        {
            // The association points at the person; the notification targets their user profile.
            DriverProfile? driver = (await _drivers.ListAsync(
                d => d.UserProfileId == member.MemberUserProfileId, cancellationToken)).FirstOrDefault();
            if (driver is not null)
            {
                await _notify.NotifyUserAsync(member.MemberUserProfileId, subject, body, cancellationToken);
            }
        }
    }
}
