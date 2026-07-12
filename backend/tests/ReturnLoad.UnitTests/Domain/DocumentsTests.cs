using ReturnLoad.Domain.Common;
using ReturnLoad.Domain.Documents;

namespace ReturnLoad.UnitTests.Domain;

public sealed class DocumentsTests
{
    private static readonly DateOnly Today = new(2026, 7, 12);

    private static Document SubmitInsurance(DateOnly? expiresOn) =>
        Document.Submit(DocumentOwnerType.Vehicle, Guid.NewGuid(), DocumentType.Insurance, "storage/key.pdf", "POL123", new DateOnly(2026, 1, 1), expiresOn);

    [Fact]
    public void Submit_starts_submitted_and_raises_uploaded_event()
    {
        Document doc = SubmitInsurance(new DateOnly(2027, 1, 1));
        Assert.Equal(VerificationStatus.Submitted, doc.VerificationStatus);
        Assert.Contains(doc.DomainEvents, e => e is DocumentUploaded);
    }

    [Fact]
    public void Verify_transitions_to_verified_and_raises_event()
    {
        Document doc = SubmitInsurance(new DateOnly(2027, 1, 1));
        doc.StartReview();
        doc.Verify(Today, DateTimeOffset.UtcNow);

        Assert.Equal(VerificationStatus.Verified, doc.VerificationStatus);
        Assert.True(doc.IsValidAsOf(Today));
        Assert.Contains(doc.DomainEvents, e => e is DocumentVerified);
    }

    [Fact]
    public void Expired_document_cannot_be_verified_fail_closed()
    {
        Document doc = SubmitInsurance(new DateOnly(2026, 6, 1)); // already expired vs Today
        doc.StartReview();
        Assert.Throws<DomainException>(() => doc.Verify(Today, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Expire_flips_a_verified_document_out_of_valid()
    {
        Document doc = SubmitInsurance(new DateOnly(2027, 1, 1));
        doc.Verify(Today, DateTimeOffset.UtcNow);
        Assert.True(doc.IsValidAsOf(Today));

        doc.Expire();
        Assert.Equal(VerificationStatus.Expired, doc.VerificationStatus);
        Assert.False(doc.IsValidAsOf(Today));
    }

    [Fact]
    public void Reject_requires_a_reason()
    {
        Document doc = SubmitInsurance(null);
        Assert.Throws<DomainException>(() => doc.Reject("  "));
        doc.Reject("Illegible scan");
        Assert.Equal(VerificationStatus.Rejected, doc.VerificationStatus);
    }

    [Fact]
    public void Expiry_before_issue_is_rejected()
    {
        Assert.Throws<DomainException>(() =>
            Document.Submit(DocumentOwnerType.Driver, Guid.NewGuid(), DocumentType.DrivingLicence, "k",
                issuedOn: new DateOnly(2026, 5, 1), expiresOn: new DateOnly(2026, 1, 1)));
    }
}
