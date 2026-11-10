using Service.Helpers.DTOs.Payment;
using Service.Helpers.DTOs.Reservation;

namespace Service.Services.Interfaces;

public interface IPaymentService
{
    /// <summary>
    /// Pays a pending booking while its seat hold is still valid: records the payment, confirms the booking,
    /// issues the tickets, books the seats and notifies the account holder.
    /// </summary>
    Task<BookingDto> PayAsync(PaymentRequestDto request, CancellationToken cancellationToken = default);
}
