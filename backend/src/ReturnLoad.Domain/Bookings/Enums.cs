namespace ReturnLoad.Domain.Bookings;

/// <summary>
/// Lifecycle of a <see cref="BookingRequest"/> — a driver's request to carry a load (M4.3
/// Steps 3–4). The load owner decides: a Pending request becomes Accepted (a trip is created)
/// or Rejected; the driver may Withdraw a still-Pending request.
/// </summary>
public enum BookingRequestStatus
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2,
    Withdrawn = 3,
}
