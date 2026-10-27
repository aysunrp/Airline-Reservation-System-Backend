using Domain.Enums;
using Microsoft.Extensions.Options;
using Repository.Repositories.Interfaces;
using Service.Helpers.Settings;
using Service.Services.Interfaces;

namespace Service.Services;

/// <summary>
/// A mock payment provider for refunds. Timings count from the cancellation: after
/// <see cref="BookingLifecycleSettings.RefundProcessingAfterMinutes"/> a refund is Processing, after
/// <see cref="BookingLifecycleSettings.RefundCompletedAfterMinutes"/> it is Completed and the customer is told.
/// A payment that was refunded in full is marked Refunded.
/// </summary>
public class RefundProcessingService : IRefundProcessingService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly INotificationService _notificationService;
    private readonly BookingLifecycleSettings _settings;
    private readonly TimeProvider _timeProvider;

    public RefundProcessingService(
        IBookingRepository bookingRepository,
        INotificationService notificationService,
        IOptions<BookingLifecycleSettings> settings,
        TimeProvider timeProvider)
    {
        _bookingRepository = bookingRepository;
        _notificationService = notificationService;
        _settings = settings.Value;
        _timeProvider = timeProvider;
    }

    public async Task<RefundProcessingResult> ProcessDueAsync(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        int processing = 0, completed = 0;

        foreach (var booking in await _bookingRepository.GetWithOpenRefundsAsync(cancellationToken))
        {
            var cancellation = booking.Cancellation!;
            var age = now - cancellation.RequestedAt;

            // Stripe refunds are sent to Stripe and finished by its webhook; the pretend provider never touches them.
            foreach (var refund in cancellation.Refunds.Where(r => r.Status is RefundStatus.Pending or RefundStatus.Processing && booking.Payments.Any(p => p.Id == r.PaymentId && p.Method != PaymentMethod.StripeCheckout)))
            {
                if (age >= TimeSpan.FromMinutes(_settings.RefundCompletedAfterMinutes))
                {
                    refund.Status = RefundStatus.Completed;
                    refund.ProcessedAt = now;
                    refund.ProviderReference = $"MOCK-REFUND-{Guid.NewGuid():N}";
                    completed++;

                    var payment = booking.Payments.FirstOrDefault(p => p.Id == refund.PaymentId);
                    if (payment is not null && refund.Amount >= payment.Amount)
                    {
                        payment.Status = PaymentStatus.Refunded;
                    }

                    if (booking.UserId is int userId)
                    {
                        await _notificationService.QueueAsync(userId, booking.Id, NotificationType.RefundCompleted, "Refund Completed",
                            $"Your refund of {refund.Amount:0.00} for booking {booking.Pnr} has been completed.", cancellationToken);
                    }
                }
                else if (refund.Status == RefundStatus.Pending && age >= TimeSpan.FromMinutes(_settings.RefundProcessingAfterMinutes))
                {
                    refund.Status = RefundStatus.Processing;
                    processing++;
                }
            }
        }

        if (processing + completed > 0)
        {
            await _bookingRepository.SaveChangesAsync(cancellationToken);
        }

        return new RefundProcessingResult(processing, completed);
    }
}
