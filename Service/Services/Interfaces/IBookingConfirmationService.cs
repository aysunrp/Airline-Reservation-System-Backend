using Domain.Entities;

namespace Service.Services.Interfaces;

/// <summary>What happens to a booking once its payment is taken, wherever the payment came from.</summary>
public interface IBookingConfirmationService
{
    /// <summary>
    /// Confirms the booking, issues its tickets, books its seats and tells the account holder. Changes are tracked, not saved:
    /// the caller saves them together with the payment.
    /// </summary>
    Task ConfirmAsync(Booking booking, decimal amountPaid, CancellationToken cancellationToken = default);
}
