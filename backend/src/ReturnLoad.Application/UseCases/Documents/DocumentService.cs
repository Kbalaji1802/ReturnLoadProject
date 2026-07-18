using Microsoft.Extensions.Options;
using ReturnLoad.Application.Abstractions.Persistence;
using ReturnLoad.Application.Abstractions.Storage;
using ReturnLoad.Domain.Documents;
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

public sealed record DocumentView(
    Guid Id,
    DocumentOwnerType OwnerType,
    Guid OwnerId,
    DocumentType Type,
    VerificationStatus VerificationStatus,
    DateOnly? ExpiresOn);

public interface IDocumentService
{
    Task<Result<Guid>> SubmitAsync(SubmitDocumentRequest request, Stream content, string fileName, string contentType, CancellationToken cancellationToken = default);

    /// <summary>Operations approves a document; approving a driver's licence verifies the driver.</summary>
    Task<Result> ApproveAsync(Guid documentId, CancellationToken cancellationToken = default);

    Task<Result> RejectAsync(Guid documentId, string reason, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<DocumentView>>> ListForOwnerAsync(DocumentOwnerType ownerType, Guid ownerId, CancellationToken cancellationToken = default);
}

internal sealed class DocumentService : IDocumentService
{
    private readonly IRepository<Document> _documents;
    private readonly IRepository<DriverProfile> _drivers;
    private readonly IFileStorageService _storage;
    private readonly FileUploadOptions _uploadOptions;
    private readonly IUnitOfWork _uow;

    public DocumentService(
        IRepository<Document> documents,
        IRepository<DriverProfile> drivers,
        IFileStorageService storage,
        IOptions<FileUploadOptions> uploadOptions,
        IUnitOfWork uow)
    {
        _documents = documents;
        _drivers = drivers;
        _storage = storage;
        _uploadOptions = uploadOptions.Value;
        _uow = uow;
    }

    public async Task<Result<Guid>> SubmitAsync(
        SubmitDocumentRequest request, Stream content, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        long size = content.CanSeek ? content.Length : 0;
        IReadOnlyList<ApiError> errors = FileUploadValidator.Validate(fileName, contentType, size, _uploadOptions);
        if (errors.Count > 0)
        {
            return Error.Validation(errors[0].Message);
        }

        StoredFile stored = await _storage.SaveAsync(new FileUploadRequest(content, fileName, contentType), cancellationToken);

        Document document = Document.Submit(
            request.OwnerType, request.OwnerId, request.Type, stored.Key, request.DocumentNumber, request.IssuedOn, request.ExpiresOn);

        await _documents.AddAsync(document, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return document.Id;
    }

    public async Task<Result> ApproveAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        Document? document = await _documents.GetByIdAsync(documentId, cancellationToken);
        if (document is null)
        {
            return Result.Failure(Error.NotFound("Document not found."));
        }

        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        document.StartReview();
        document.Verify(today, DateTimeOffset.UtcNow);
        _documents.Update(document);

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

        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> RejectAsync(Guid documentId, string reason, CancellationToken cancellationToken = default)
    {
        Document? document = await _documents.GetByIdAsync(documentId, cancellationToken);
        if (document is null)
        {
            return Result.Failure(Error.NotFound("Document not found."));
        }

        document.Reject(reason);
        _documents.Update(document);
        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<DocumentView>>> ListForOwnerAsync(
        DocumentOwnerType ownerType, Guid ownerId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Document> docs = await _documents.ListAsync(
            d => d.OwnerType == ownerType && d.OwnerId == ownerId, cancellationToken);

        IReadOnlyList<DocumentView> views = docs
            .Select(d => new DocumentView(d.Id, d.OwnerType, d.OwnerId, d.Type, d.VerificationStatus, d.ExpiresOn))
            .ToList();
        return Result<IReadOnlyList<DocumentView>>.Success(views);
    }
}
