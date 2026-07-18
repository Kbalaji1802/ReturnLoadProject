using ReturnLoad.Domain.ValueObjects;
using ReturnLoad.Shared.Results;

namespace ReturnLoad.Application.Abstractions.Payments;

/// <summary>
/// Settlement/commission abstraction — <b>interfaces only</b> for the MVP. The platform does
/// not hold or move money (ADR-0005): it records the commission owed per completed booking;
/// driver and shipper settle directly. A collected-payments gateway is a future ADR and
/// plugs in behind this seam without touching business code. No implementation ships yet.
/// </summary>
public interface IPaymentService
{
    /// <summary>Records the commission the platform is owed for a completed booking.</summary>
    Task<Result> RecordCommissionAsync(Guid bookingId, Money commission, CancellationToken cancellationToken = default);

    /// <summary>Retrieves the commission recorded against a booking, if any.</summary>
    Task<Result<Money?>> GetCommissionAsync(Guid bookingId, CancellationToken cancellationToken = default);
}
