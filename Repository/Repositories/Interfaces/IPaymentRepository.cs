using Domain.Entities;

namespace Repository.Repositories.Interfaces;

public interface IPaymentRepository : IBaseRepository<Payment>
{
    /// <summary>The payment with its booking and the booking's passengers, without change tracking; null when there is none.</summary>
    Task<Payment?> GetWithBookingAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>The payment made through this Stripe Checkout Session (tracked, with its booking's reference); null when there is none.</summary>
    Task<Payment?> GetByCheckoutSessionIdAsync(string checkoutSessionId, CancellationToken cancellationToken = default);
}
