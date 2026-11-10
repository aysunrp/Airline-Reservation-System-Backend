using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.Logging;
using Repository.Repositories.Interfaces;
using Service.Helpers;
using Service.Helpers.DTOs.Reservation;
using Service.Helpers.Exceptions;
using Service.Services.Interfaces;

namespace Service.Services;

/// <summary>
/// Sends the refunds a cancellation asked for to Stripe and keeps their state true to what Stripe reports. A refund is one row per
/// cancelled booking and payment, so asking twice can never pay back twice; the total of refunds on a payment never passes what was paid.
/// </summary>
public class RefundService : IRefundService
{
    /// <summary>A refund still unsent after this long was missed by the cancel request itself and is sent by the background job.</summary>
    private static readonly TimeSpan SendGrace = TimeSpan.FromMinutes(1);

    private readonly IRefundRepository _refunds;
    private readonly IStripeRefundGateway _gateway;
    private readonly INotificationService _notifications;
    private readonly IAuditLogService _audit;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RefundService> _logger;

    public RefundService(
        IRefundRepository refunds,
        IStripeRefundGateway gateway,
        INotificationService notifications,
        IAuditLogService audit,
        TimeProvider timeProvider,
        ILogger<RefundService> logger)
    {
        _refunds = refunds;
        _gateway = gateway;
        _notifications = notifications;
        _audit = audit;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task SubmitAsync(int refundId, CancellationToken cancellationToken = default)
    {
        var refund = await _refunds.GetWithDetailsAsync(refundId, cancellationToken);
        if (refund is null)
        {
            return;
        }

        await SendAsync(refund, cancellationToken);
    }

    public async Task<int> SubmitDueAsync(CancellationToken cancellationToken = default)
    {
        var due = await _refunds.GetUnsentStripeRefundsAsync(_timeProvider.GetUtcNow().UtcDateTime - SendGrace, 20, cancellationToken);
        foreach (var refund in due)
        {
            await SendAsync(refund, cancellationToken);
        }

        return due.Count;
    }

    public async Task<string> ApplyProviderStateAsync(Refund refund, string? providerRefundId, string providerStatus, string? failureReason, CancellationToken cancellationToken = default)
    {
        var status = StripeRefundStatusMapper.ToRefundStatus(providerStatus);

        if (providerRefundId is not null && refund.ProviderReference is not null && refund.ProviderReference != providerRefundId)
        {
            return "Rejected: the refund does not match the one Stripe made for this request";
        }

        var providerStatusText = providerStatus.Length > 40 ? providerStatus[..40] : providerStatus;
        if (refund.Status == status && refund.ProviderStatus == providerStatusText && (providerRefundId is null || refund.ProviderReference == providerRefundId))
        {
            return $"Already {status}";
        }

        // A refund only moves forward: a late "pending" never undoes a result, and a result is never swapped for another.
        // The one step back is real: a refund that succeeded can still fail later (the card was closed, say). Stripe then returns
        // the money to AEROVA's balance and the customer has to be paid back another way.
        var returnedByBank = refund.Status == RefundStatus.Completed && status == RefundStatus.Failed;
        if (refund.Status is not (RefundStatus.Pending or RefundStatus.Processing) && !returnedByBank)
        {
            return $"Ignored: the refund is already {refund.Status}";
        }

        refund.ProviderReference ??= providerRefundId;
        refund.ProviderStatus = providerStatusText;
        refund.Status = status;
        _refunds.Update(refund);

        switch (status)
        {
            case RefundStatus.Processing:
                await NotifyAsync(refund, "processing", cancellationToken);
                break;
            case RefundStatus.Completed:
                refund.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
                refund.FailureReason = null;
                await CompleteAsync(refund, cancellationToken);
                break;
            case RefundStatus.Failed:
            case RefundStatus.Cancelled:
                refund.FailureReason = Shorten(failureReason ?? providerStatus);
                await NotifyAsync(refund, returnedByBank ? "returned" : "failed", cancellationToken);
                break;
        }

        if (returnedByBank)
        {
            refund.ProcessedAt = null;
            if (refund.Payment.Status == PaymentStatus.Refunded)
            {
                refund.Payment.Status = PaymentStatus.Paid;
            }

            _logger.LogError("Refund {RefundId} failed after it had completed; the money is back in the Stripe balance and the customer must be paid back another way.", refund.Id);
            return "Failed after it had completed: pay the customer back another way";
        }

        return status.ToString();
    }

    public async Task<AdminRefundDto> RetryAsync(int refundId, AuditActor actor, CancellationToken cancellationToken = default)
    {
        var refund = await _refunds.GetWithDetailsAsync(refundId, cancellationToken)
                     ?? throw new NotFoundException("Refund not found.");

        if (refund.Payment.Method != PaymentMethod.StripeCheckout)
        {
            throw new BusinessRuleException("Only refunds of Stripe payments can be sent again.");
        }

        if (refund.Status is not (RefundStatus.Failed or RefundStatus.Cancelled))
        {
            throw new ConflictException("Only a refund that failed or was cancelled can be sent again.");
        }

        var oldReference = refund.ProviderReference;
        refund.Attempts++;
        refund.Status = RefundStatus.Pending;
        refund.ProviderReference = null;
        refund.ProviderStatus = null;
        refund.FailureReason = null;
        refund.ProcessedAt = null;
        _refunds.Update(refund);

        await _audit.StageAsync(actor, AuditActions.RefundRetried, "Refund", refund.Id.ToString(),
            $"Booking {refund.Cancellation.Booking.Pnr}, attempt {refund.Attempts}" + (oldReference is null ? "" : $", the earlier Stripe refund was {oldReference}"), cancellationToken);
        await _refunds.SaveChangesAsync(cancellationToken);

        await SendAsync(refund, cancellationToken);
        return ToDto(refund);
    }

    public async Task<IReadOnlyList<AdminRefundDto>> GetAllAsync(RefundStatus? status, int take, CancellationToken cancellationToken = default)
        => (await _refunds.GetPageAsync(status, Math.Clamp(take, 1, 500), cancellationToken)).Select(ToDto).ToList();

    private async Task SendAsync(Refund refund, CancellationToken cancellationToken)
    {
        if (refund.Status != RefundStatus.Pending || refund.ProviderReference is not null || refund.Payment.Method != PaymentMethod.StripeCheckout)
        {
            return;
        }

        var payment = refund.Payment;
        var problem = await CheckAsync(refund, payment, cancellationToken);
        if (problem is not null)
        {
            refund.Status = RefundStatus.Failed;
            refund.FailureReason = problem;
            _refunds.Update(refund);
            await NotifyAsync(refund, "failed", cancellationToken);
            await _refunds.SaveChangesAsync(cancellationToken);
            return;
        }

        StripeRefundResult result;
        try
        {
            result = await _gateway.CreateRefundAsync(
                new StripeRefundRequest(refund.Id, refund.Attempts, payment.StripePaymentIntentId!, CheckoutService.ToMinorUnits(refund.Amount), refund.Currency.ToLowerInvariant(), refund.Cancellation.Booking.Pnr),
                cancellationToken);
        }
        catch (ServiceUnavailableException)
        {
            // Stripe is out of reach: the refund stays Pending and the background job sends it again.
            _logger.LogWarning("Refund {RefundId} could not be sent to Stripe yet; it stays queued.", refund.Id);
            return;
        }

        await ApplyProviderStateAsync(refund, result.Id, result.Status, result.FailureReason, cancellationToken);
        await _refunds.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Everything that must hold before money is sent back; the answer is the reason it does not, or null.</summary>
    private async Task<string?> CheckAsync(Refund refund, Payment payment, CancellationToken cancellationToken)
    {
        if (payment.Status != PaymentStatus.Paid || string.IsNullOrWhiteSpace(payment.StripePaymentIntentId))
        {
            return "The payment has no confirmed Stripe charge to refund.";
        }

        if (!string.Equals(refund.Currency, payment.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return "The refund currency differs from the payment currency.";
        }

        if (refund.Amount <= 0)
        {
            return "The refund amount must be above zero.";
        }

        var others = (await _refunds.GetByPaymentIdAsync(payment.Id, cancellationToken))
            .Where(r => r.Id != refund.Id && r.Status is RefundStatus.Pending or RefundStatus.Processing or RefundStatus.Completed)
            .Sum(r => r.Amount);
        if (others + refund.Amount > payment.Amount)
        {
            return "The refunds would be more than the amount paid.";
        }

        try
        {
            CheckoutService.ToMinorUnits(refund.Amount);
        }
        catch (BusinessRuleException)
        {
            return "The refund amount is not a whole number of cents.";
        }

        return null;
    }

    private async Task CompleteAsync(Refund refund, CancellationToken cancellationToken)
    {
        var payment = refund.Payment;
        var paidBack = (await _refunds.GetByPaymentIdAsync(payment.Id, cancellationToken))
            .Where(r => r.Id != refund.Id && r.Status == RefundStatus.Completed)
            .Sum(r => r.Amount) + refund.Amount;
        if (paidBack >= payment.Amount)
        {
            payment.Status = PaymentStatus.Refunded;
        }

        await NotifyAsync(refund, "completed", cancellationToken);
    }

    /*
     * One notification (and so one e-mail) per refund, attempt and status reached; Stripe may report the same change several times.
     * The texts only say what is true at that status: "started" while Stripe processes it, "confirmed" once it succeeded, and never
     * that money arrived when the refund failed. Sending the e-mail is a separate step, so it can never change the refund.
     */
    private async Task NotifyAsync(Refund refund, string stage, CancellationToken cancellationToken)
    {
        var booking = refund.Cancellation.Booking;
        if (booking.UserId is not int userId)
        {
            return;
        }

        var amount = $"{refund.Amount:0.00} {refund.Currency}";
        var (type, title, message) = stage switch
        {
            "processing" => (NotificationType.RefundPending, "Refund Started",
                $"The refund of {amount} for booking {booking.Pnr} has been started to the card you paid with and is being processed. We will tell you when it is confirmed."),
            "completed" => (NotificationType.RefundCompleted, "Refund Completed",
                $"The refund of {amount} for booking {booking.Pnr} has been confirmed and sent to the card you paid with. Banks usually show it within 5 to 10 business days."),
            _ => (NotificationType.RefundFailed, "Refund Not Completed",
                $"The refund of {amount} for booking {booking.Pnr} could not be completed. Your booking stays cancelled and the amount is still owed to you; please contact AEROVA support with your booking reference.")
        };

        await _notifications.QueueOnceAsync($"refund:{refund.Id}:{refund.Attempts}:{stage}", userId, booking.Id, type, title, message, cancellationToken);
    }

    private static string Shorten(string text) => text.Length > 300 ? text[..300] : text;

    private static AdminRefundDto ToDto(Refund r) => new(
        r.Id,
        r.Cancellation.Booking.Pnr,
        r.Amount,
        r.Currency,
        r.Status,
        r.Reason,
        r.FailureReason,
        r.ProviderReference,
        r.ProviderStatus,
        r.Cancellation.AmountPaid,
        r.Cancellation.FeeAmount,
        r.Attempts,
        r.InitiatedByUser?.Email ?? (r.InitiatedByUserId is null ? "Guest" : $"User {r.InitiatedByUserId}"),
        r.CreatedAt,
        r.UpdatedAt,
        r.ProcessedAt);
}
