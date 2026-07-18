using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReturnLoad.Api.Extensions;
using ReturnLoad.Application.Identity;
using ReturnLoad.Application.UseCases.Documents;
using ReturnLoad.Domain.Documents;

namespace ReturnLoad.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/documents")]
[Authorize]
public sealed class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documents;

    public DocumentsController(IDocumentService documents) => _documents = documents;

    /// <summary>Uploads a document (multipart) for a driver/vehicle/carrier owner.</summary>
    [HttpPost("upload")]
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
        var result = await _documents.SubmitAsync(request, content, file.FileName, file.ContentType, cancellationToken);
        return result.ToApiResult(HttpContext, "Document uploaded.");
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] DocumentOwnerType ownerType, [FromQuery] Guid ownerId, CancellationToken cancellationToken)
    {
        var result = await _documents.ListForOwnerAsync(ownerType, ownerId, cancellationToken);
        return result.ToApiResult(HttpContext);
    }

    /// <summary>Operations approves a document (verifies the driver when a licence is approved).</summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = AuthorizationPolicies.CanVerifyDocuments)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        var result = await _documents.ApproveAsync(id, cancellationToken);
        return result.ToApiResult(HttpContext, "Document approved.");
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = AuthorizationPolicies.CanVerifyDocuments)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectDocumentBody body, CancellationToken cancellationToken)
    {
        var result = await _documents.RejectAsync(id, body.Reason, cancellationToken);
        return result.ToApiResult(HttpContext, "Document rejected.");
    }
}

public sealed record RejectDocumentBody(string Reason);
