using Domain.Entities;

namespace Service.Services.Interfaces;

/// <summary>What happens to a booking once its payment is taken, wherever the payment came from.</summary>
public interface IBookingConfirmationService
{
    /// <summary>
    /// Claims the booking's seats and, only if every one is still free, confirms the booking, issues its tickets and tells the account holder.
    /// Returns false, changing nothing, when a seat was taken by someone else in the meantime. Changes are tracked, not saved: the caller
    /// saves them together with the payment, and the seat's row version plus the unique index on paid tickets stop two payments claiming
    /// the same seat even at the same instant.
    /// </summary>
    Task<bool> ConfirmAsync(Booking booking, decimal amountPaid, CancellationToken cancellationToken = default);
}
