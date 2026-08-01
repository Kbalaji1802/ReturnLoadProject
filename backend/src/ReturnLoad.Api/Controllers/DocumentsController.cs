using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReturnLoad.Api.Extensions;
using ReturnLoad.Api.Http;
using ReturnLoad.Application.Identity;
using ReturnLoad.Application.UseCases.Documents;
using ReturnLoad.Application.UseCases.Onboarding;
using ReturnLoad.Domain.Documents;

namespace ReturnLoad.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/documents")]
[Authorize]
public sealed class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documents;
    private readonly IDriverOnboardingService _drivers;

    public DocumentsController(IDocumentService documents, IDriverOnboardingService drivers)
    {
        _documents = documents;
        _drivers = drivers;
    }

    /// <summary>
    /// A driver uploads one of <b>their own</b> documents (multipart). The owning driver is
    /// resolved from the authenticated token — the client never supplies an owner id, so a
    /// document can only ever attach to the caller's profile (closes M4.2 §8 S1).
    /// </summary>
    [HttpPost("driver-upload")]
    public async Task<IActionResult> DriverUpload(
        [FromForm] DocumentType type,
        [FromForm] string? documentNumber,
        [FromForm] DateOnly? expiresOn,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest();
        }

        if (!HttpContext.TryGetUserId(out Guid authUserId))
        {
            return Unauthorized();
        }

        var driver = await _drivers.GetForUserAsync(authUserId, cancellationToken);
        if (driver.IsFailure)
        {
            return driver.ToApiResult(HttpContext);
        }

        SubmitDocumentRequest request = new(
            DocumentOwnerType.Driver, driver.Value.Id, type, documentNumber, IssuedOn: null, ExpiresOn: expiresOn);
        await using Stream content = file.OpenReadStream();
        var result = await _documents.SubmitAsync(request, content, file.FileName, file.ContentType, file.Length, cancellationToken);
        return result.ToApiResult(HttpContext, "Document uploaded.");
    }

    /// <summary>
    /// Staff attaches a document for any owner (driver/vehicle/carrier) during onboarding —
    /// e.g. Operations acting on behalf of a party. A self-service driver uses
    /// <see cref="DriverUpload"/>; this arbitrary-owner path is staff-only.
    /// </summary>
    [HttpPost("upload")]
    [Authorize(Policy = AuthorizationPolicies.InternalStaff)]
    public async Task<IActionResult> Upload(
        [FromForm] DocumentOwnerType ownerType,
        [FromForm] Guid ownerId,
        [FromForm] DocumentType type,
        [FromForm] string? documentNumber,
        [FromForm] DateOnly? expiresOn,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest();
        }

        SubmitDocumentRequest request = new(ownerType, ownerId, type, documentNumber, IssuedOn: null, ExpiresOn: expiresOn);
        await using Stream content = file.OpenReadStream();
        var result = await _documents.SubmitAsync(request, content, file.FileName, file.ContentType, file.Length, cancellationToken);
        return result.ToApiResult(HttpContext, "Document uploaded.");
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] DocumentOwnerType ownerType, [FromQuery] Guid ownerId, CancellationToken cancellationToken)
    {
        var result = await _documents.ListForOwnerAsync(ownerType, ownerId, cancellationToken);
        return result.ToApiResult(HttpContext);
    }

    /// <summary>The Operations review queue.</summary>
    [HttpGet("pending")]
    [Authorize(Policy = AuthorizationPolicies.CanVerifyDocuments)]
    public async Task<IActionResult> Pending(CancellationToken cancellationToken)
    {
        var result = await _documents.ListPendingAsync(cancellationToken);
        return result.ToApiResult(HttpContext);
    }

    /// <summary>
    /// Streams a document's file for the Operations preview/download (Part 1). Staff-only — the
    /// reviewer opens it in the preview dialog (zoom/rotate/fullscreen) or downloads it.
    /// </summary>
    [HttpGet("{id:guid}/file")]
    [Authorize(Policy = AuthorizationPolicies.CanVerifyDocuments)]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var result = await _documents.GetFileAsync(id, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToApiResult(HttpContext);
        }

        DocumentFile file = result.Value;
        return File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>The document's append-only review history (approve/reject decisions with reasons).</summary>
    [HttpGet("{id:guid}/history")]
    [Authorize(Policy = AuthorizationPolicies.CanVerifyDocuments)]
    public async Task<IActionResult> History(Guid id, CancellationToken cancellationToken)
    {
        var result = await _documents.ListReviewHistoryAsync(id, cancellationToken);
        return result.ToApiResult(HttpContext);
    }

    /// <summary>Operations approves a document (verifies the driver when a licence is approved).</summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = AuthorizationPolicies.CanVerifyDocuments)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        HttpContext.TryGetUserId(out Guid adminUserId);
        var result = await _documents.ApproveAsync(id, adminUserId, cancellationToken);
        return result.ToApiResult(HttpContext, "Document approved.");
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = AuthorizationPolicies.CanVerifyDocuments)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectDocumentBody body, CancellationToken cancellationToken)
    {
        HttpContext.TryGetUserId(out Guid adminUserId);
        var result = await _documents.RejectAsync(id, body.Reason, adminUserId, cancellationToken);
        return result.ToApiResult(HttpContext, "Document rejected.");
    }
}

public sealed record RejectDocumentBody(string Reason);
