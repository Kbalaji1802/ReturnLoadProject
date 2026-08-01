using Microsoft.Extensions.Options;
using ReturnLoad.Application.Abstractions.Persistence;
using ReturnLoad.Application.Abstractions.Storage;
using ReturnLoad.Application.UseCases.Notifications;
using ReturnLoad.Domain.Documents;
using ReturnLoad.Domain.Fleet;
using ReturnLoad.Domain.Identity;
using ReturnLoad.Shared.Api;
using ReturnLoad.Shared.Configuration;
using ReturnLoad.Shared.Results;

namespace ReturnLoad.Application.UseCases.Documents;

public sealed record SubmitDocumentRequest(
    DocumentOwnerType OwnerType,
    Guid OwnerId,
    DocumentType Type,
    string? DocumentNumber,
    DateOnly? IssuedOn,
    DateOnly? ExpiresOn);

/// <summary>
/// A document as its owner (driver) sees it (Part 8): includes the rejection reason and uploaded
/// date so a rejected document tells the driver what to fix before re-uploading.
/// </summary>
public sealed record DocumentView(
    Guid Id,
    DocumentOwnerType OwnerType,
    Guid OwnerId,
    DocumentType Type,
    string? DocumentNumber,
    VerificationStatus VerificationStatus,
    DocumentStatus Status,
    DateOnly? ExpiresOn,
    DateTimeOffset UploadedAtUtc,
    string? RejectionReason);

/// <summary>
/// A pending document enriched for the Operations verification queue (Part 1): who uploaded it,
/// driver/company/vehicle context, and the document's own fields — everything the reviewer needs to
/// decide without leaving the row.
/// </summary>
public sealed record PendingDocumentView(
    Guid Id,
    DocumentOwnerType OwnerType,
    Guid OwnerId,
    DocumentType Type,
    string? DocumentNumber,
    VerificationStatus VerificationStatus,
    DateOnly? ExpiresOn,
    DateTimeOffset UploadedAtUtc,
    string? DriverName,
    string? DriverPhotoUrl,
    string? Company,
    string? VehicleRegistration);

/// <summary>A file streamed back to an authorised viewer (admin preview / download, Part 1).</summary>
public sealed record DocumentFile(Stream Content, string ContentType, string FileName);

/// <summary>One entry in a document's append-only review history (Part 8).</summary>
public sealed record DocumentReviewView(
    DocumentReviewDecision Decision, string? Reason, Guid DecidedByUserId, DateTimeOffset DecidedAtUtc);

public interface IDocumentService
{
    Task<Result<Guid>> SubmitAsync(SubmitDocumentRequest request, Stream content, string fileName, string contentType, long sizeBytes, CancellationToken cancellationToken = default);

    /// <summary>Operations approves a document; approving a driver's licence verifies the driver.</summary>
    Task<Result> ApproveAsync(Guid documentId, Guid decidedByUserId = default, CancellationToken cancellationToken = default);

    /// <summary>Operations rejects a document with a required reason; the owner is notified to re-upload.</summary>
    Task<Result> RejectAsync(Guid documentId, string reason, Guid decidedByUserId = default, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<DocumentView>>> ListForOwnerAsync(DocumentOwnerType ownerType, Guid ownerId, CancellationToken cancellationToken = default);

    /// <summary>The Operations review queue — documents awaiting a decision, enriched for display.</summary>
    Task<Result<IReadOnlyList<PendingDocumentView>>> ListPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>Streams a document's file for preview/download (authorised at the API edge).</summary>
    Task<Result<DocumentFile>> GetFileAsync(Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>The document's full, append-only Operations decision history, newest first (Part 8).</summary>
    Task<Result<IReadOnlyList<DocumentReviewView>>> ListReviewHistoryAsync(Guid documentId, CancellationToken cancellationToken = default);
}

internal sealed class DocumentService : IDocumentService
{
    private readonly IRepository<Document> _documents;
    private readonly IRepository<DocumentReview> _reviews;
    private readonly IRepository<DriverProfile> _drivers;
    private readonly IRepository<UserProfile> _users;
    private readonly IRepository<Carrier> _carriers;
    private readonly IRepository<Vehicle> _vehicles;
    private readonly IRepository<Association> _associations;
    private readonly IFileStorageService _storage;
    private readonly FileUploadOptions _uploadOptions;
    private readonly INotificationService _notify;
    private readonly IUnitOfWork _uow;

    public DocumentService(
        IRepository<Document> documents,
        IRepository<DocumentReview> reviews,
        IRepository<DriverProfile> drivers,
        IRepository<UserProfile> users,
        IRepository<Carrier> carriers,
        IRepository<Vehicle> vehicles,
        IRepository<Association> associations,
        IFileStorageService storage,
        IOptions<FileUploadOptions> uploadOptions,
        INotificationService notify,
        IUnitOfWork uow)
    {
        _documents = documents;
        _reviews = reviews;
        _drivers = drivers;
        _users = users;
        _carriers = carriers;
        _vehicles = vehicles;
        _associations = associations;
        _storage = storage;
        _uploadOptions = uploadOptions.Value;
        _notify = notify;
        _uow = uow;
    }

    public async Task<Result<Guid>> SubmitAsync(
        SubmitDocumentRequest request, Stream content, string fileName, string contentType, long sizeBytes, CancellationToken cancellationToken = default)
    {
        // The caller supplies the size (upload streams are often non-seekable, so
        // content.Length is unreliable here).
        long size = sizeBytes > 0 ? sizeBytes : (content.CanSeek ? content.Length : 0);
        IReadOnlyList<ApiError> errors = FileUploadValidator.Validate(fileName, contentType, size, _uploadOptions);
        if (errors.Count > 0)
        {
            return Error.Validation(errors[0].Message);
        }

        StoredFile stored = await _storage.SaveAsync(new FileUploadRequest(content, fileName, contentType), cancellationToken);

        // A replacement supersedes the prior document of the same type for this owner (Part 8):
        // archive the old one so it never gates transactability, but keep it for the audit trail.
        IReadOnlyList<Document> priors = await _documents.ListAsync(
            d => d.OwnerType == request.OwnerType && d.OwnerId == request.OwnerId
                && d.Type == request.Type && d.Status == DocumentStatus.Active,
            cancellationToken);
        foreach (Document prior in priors)
        {
            prior.Archive();
            _documents.Update(prior);
        }

        Document document = Document.Submit(
            request.OwnerType, request.OwnerId, request.Type, stored.Key, request.DocumentNumber, request.IssuedOn, request.ExpiresOn);

        await _documents.AddAsync(document, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return document.Id;
    }

    public async Task<Result> ApproveAsync(Guid documentId, Guid decidedByUserId = default, CancellationToken cancellationToken = default)
    {
        Document? document = await _documents.GetByIdAsync(documentId, cancellationToken);
        if (document is null)
        {
            return Result.Failure(Error.NotFound("Document not found."));
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateOnly today = DateOnly.FromDateTime(now.UtcDateTime);
        document.StartReview();
        document.Verify(today, now);
        _documents.Update(document);
        await _reviews.AddAsync(DocumentReview.Approved(document.Id, decidedByUserId, now), cancellationToken);

        // Trust & Safety pre-trip gate (MVP): a driver becomes verified once their licence
        // document is verified. (Full KYC+DL+... set is enforced in a later milestone.)
        if (document is { OwnerType: DocumentOwnerType.Driver, Type: DocumentType.DrivingLicence })
        {
            DriverProfile? driver = await _drivers.GetByIdAsync(document.OwnerId, cancellationToken);
            if (driver is { Status: DriverStatus.Pending })
            {
                driver.MarkVerified();
                _drivers.Update(driver);
            }
        }

        if (document.OwnerType == DocumentOwnerType.Driver)
        {
            await _notify.NotifyDriverAsync(document.OwnerId, "Document approved", "Your document was verified by our team.", cancellationToken);
        }

        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> RejectAsync(Guid documentId, string reason, Guid decidedByUserId = default, CancellationToken cancellationToken = default)
    {
        Document? document = await _documents.GetByIdAsync(documentId, cancellationToken);
        if (document is null)
        {
            return Result.Failure(Error.NotFound("Document not found."));
        }

        document.Reject(reason);
        _documents.Update(document);
        await _reviews.AddAsync(
            DocumentReview.Rejected(document.Id, reason, decidedByUserId, DateTimeOffset.UtcNow), cancellationToken);

        // The owner must learn why so they can re-upload (Part 8). Reason is echoed in the inbox.
        if (document.OwnerType == DocumentOwnerType.Driver)
        {
            await _notify.NotifyDriverAsync(
                document.OwnerId, "Document rejected", $"Your document was rejected: {reason}. Please upload a corrected copy.", cancellationToken);
        }

        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<DocumentView>>> ListForOwnerAsync(
        DocumentOwnerType ownerType, Guid ownerId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Document> docs = await _documents.ListAsync(
            d => d.OwnerType == ownerType && d.OwnerId == ownerId, cancellationToken);

        return Result<IReadOnlyList<DocumentView>>.Success(docs.Select(Map).ToList());
    }

    public async Task<Result<IReadOnlyList<PendingDocumentView>>> ListPendingAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Document> docs = await _documents.ListAsync(
            d => d.VerificationStatus == VerificationStatus.Submitted || d.VerificationStatus == VerificationStatus.UnderReview,
            cancellationToken);

        // Enrich each pending row with driver / company / vehicle context (Part 1). N+1 is fine at
        // review-queue scale; caches keep repeated owners cheap within a single call.
        Dictionary<Guid, UserProfile?> userCache = [];
        Dictionary<Guid, Carrier?> carrierCache = [];
        List<PendingDocumentView> views = [];
        foreach (Document d in docs)
        {
            views.Add(await EnrichAsync(d, userCache, carrierCache, cancellationToken));
        }

        // Oldest submissions first — Operations clears the longest-waiting driver first.
        return Result<IReadOnlyList<PendingDocumentView>>.Success(
            views.OrderBy(v => v.UploadedAtUtc).ToList());
    }

    public async Task<Result<DocumentFile>> GetFileAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        Document? document = await _documents.GetByIdAsync(documentId, cancellationToken);
        if (document is null)
        {
            return Error.NotFound("Document not found.");
        }

        FileContent? file = await _storage.GetAsync(document.StorageKey, cancellationToken);
        return file is null
            ? Error.NotFound("The document file is no longer available.")
            : new DocumentFile(file.Content, file.ContentType, file.FileName);
    }

    public async Task<Result<IReadOnlyList<DocumentReviewView>>> ListReviewHistoryAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<DocumentReview> history = await _reviews.ListAsync(r => r.DocumentId == documentId, cancellationToken);
        IReadOnlyList<DocumentReviewView> views = history
            .OrderByDescending(r => r.DecidedAtUtc)
            .Select(r => new DocumentReviewView(r.Decision, r.Reason, r.DecidedByUserId, r.DecidedAtUtc))
            .ToList();
        return Result<IReadOnlyList<DocumentReviewView>>.Success(views);
    }

    private async Task<PendingDocumentView> EnrichAsync(
        Document d, Dictionary<Guid, UserProfile?> userCache, Dictionary<Guid, Carrier?> carrierCache, CancellationToken cancellationToken)
    {
        string? driverName = null, driverPhotoUrl = null, company = null, vehicleReg = null;
        Guid? carrierId = null;

        switch (d.OwnerType)
        {
            case DocumentOwnerType.Driver:
            {
                DriverProfile? driver = await _drivers.GetByIdAsync(d.OwnerId, cancellationToken);
                if (driver is not null)
                {
                    UserProfile? user = await GetUserAsync(driver.UserProfileId, userCache, cancellationToken);
                    driverName = user?.FullName;
                    driverPhotoUrl = user?.PhotoUrl;
                    carrierId = await ResolveCarrierForMemberAsync(driver.UserProfileId, cancellationToken);
                }

                break;
            }

            case DocumentOwnerType.Vehicle:
            {
                Vehicle? vehicle = await _vehicles.GetByIdAsync(d.OwnerId, cancellationToken);
                if (vehicle is not null)
                {
                    vehicleReg = vehicle.Registration.Value;
                    carrierId = vehicle.CarrierId;
                    (driverName, driverPhotoUrl) = await ResolveCarrierDriverAsync(vehicle.CarrierId, userCache, cancellationToken);
                }

                break;
            }

            case DocumentOwnerType.Carrier:
            {
                carrierId = d.OwnerId;
                (driverName, driverPhotoUrl) = await ResolveCarrierDriverAsync(d.OwnerId, userCache, cancellationToken);
                break;
            }
        }

        if (carrierId is Guid cid)
        {
            company = (await GetCarrierAsync(cid, carrierCache, cancellationToken))?.LegalName;
            vehicleReg ??= (await _vehicles.ListAsync(v => v.CarrierId == cid, cancellationToken))
                .FirstOrDefault()?.Registration.Value;
        }

        return new PendingDocumentView(
            d.Id, d.OwnerType, d.OwnerId, d.Type, d.DocumentNumber, d.VerificationStatus, d.ExpiresOn,
            d.CreatedAtUtc, driverName, driverPhotoUrl, company, vehicleReg);
    }

    private async Task<Guid?> ResolveCarrierForMemberAsync(Guid userProfileId, CancellationToken cancellationToken)
    {
        Association? association = (await _associations.ListAsync(
            a => a.MemberUserProfileId == userProfileId && a.Role == AssociationRole.Driver && a.Status != AssociationStatus.Revoked,
            cancellationToken)).FirstOrDefault();
        return association?.CarrierId;
    }

    private async Task<(string? Name, string? PhotoUrl)> ResolveCarrierDriverAsync(
        Guid carrierId, Dictionary<Guid, UserProfile?> userCache, CancellationToken cancellationToken)
    {
        Association? driverLink = (await _associations.ListAsync(
            a => a.CarrierId == carrierId && a.Role == AssociationRole.Driver && a.Status != AssociationStatus.Revoked,
            cancellationToken)).FirstOrDefault();
        if (driverLink is null)
        {
            return (null, null);
        }

        UserProfile? user = await GetUserAsync(driverLink.MemberUserProfileId, userCache, cancellationToken);
        return (user?.FullName, user?.PhotoUrl);
    }

    private async Task<UserProfile?> GetUserAsync(Guid id, Dictionary<Guid, UserProfile?> cache, CancellationToken cancellationToken)
    {
        if (!cache.TryGetValue(id, out UserProfile? user))
        {
            user = await _users.GetByIdAsync(id, cancellationToken);
            cache[id] = user;
        }

        return user;
    }

    private async Task<Carrier?> GetCarrierAsync(Guid id, Dictionary<Guid, Carrier?> cache, CancellationToken cancellationToken)
    {
        if (!cache.TryGetValue(id, out Carrier? carrier))
        {
            carrier = await _carriers.GetByIdAsync(id, cancellationToken);
            cache[id] = carrier;
        }

        return carrier;
    }

    private static DocumentView Map(Document d) =>
        new(d.Id, d.OwnerType, d.OwnerId, d.Type, d.DocumentNumber, d.VerificationStatus, d.Status, d.ExpiresOn, d.CreatedAtUtc, d.RejectionReason);
}
