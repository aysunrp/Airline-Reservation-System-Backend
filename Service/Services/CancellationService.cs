using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Reservation;
using Service.Helpers.Exceptions;
using Service.Helpers.Mappings;
using Service.Services.Interfaces;

namespace Service.Services;

public class CancellationService : ICancellationService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly ICancellationPolicyService _policyService;
    private readonly ISeatService _seatService;
    private readonly INotificationService _notificationService;
    private readonly IRefundService _refundService;
    private readonly IValidator<CancelBookingRequestDto> _validator;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CancellationService> _logger;

    public CancellationService(
        IBookingRepository bookingRepository,
        ICancellationPolicyService policyService,
        ISeatService seatService,
        INotificationService notificationService,
        IRefundService refundService,
        IValidator<CancelBookingRequestDto> validator,
        TimeProvider timeProvider,
        ILogger<CancellationService> logger)
    {
        _bookingRepository = bookingRepository;
        _policyService = policyService;
        _seatService = seatService;
        _notificationService = notificationService;
        _refundService = refundService;
        _validator = validator;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<CancellationQuoteDto> GetQuoteAsync(string pnr, string lastName, int? userId = null, CancellationToken cancellationToken = default)
    {
        var booking = await GetCancellableBookingAsync(pnr, lastName, userId, cancellationToken);
        return await QuoteAsync(booking, cancellationToken);
    }

    public async Task<BookingDto> CancelAsync(CancelBookingRequestDto request, int? userId = null, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var booking = await GetCancellableBookingAsync(request.Pnr, request.LastName, userId, cancellationToken);
        return await CancelBookingAsync(booking, request.Reason, userId, cancellationToken, request.Language);
    }

    public async Task<BookingDto> CancelOwnBookingAsync(string pnr, int userId, string? reason, string? language, CancellationToken cancellationToken = default)
    {
        var booking = await GetCancellableBookingAsync(pnr, null, userId, cancellationToken, byOwner: true);
        return await CancelBookingAsync(booking, reason, userId, cancellationToken, language);
    }

    public async Task<BookingDto> CancelByAdminAsync(string pnr, string? reason, int? adminUserId = null, CancellationToken cancellationToken = default)
    {
        var booking = await GetCancellableBookingAsync(pnr, null, null, cancellationToken, admin: true);
        return await CancelBookingAsync(booking, reason, adminUserId, cancellationToken);
    }

    private async Task<BookingDto> CancelBookingAsync(Booking booking, string? reason, int? actorUserId, CancellationToken cancellationToken, string? language = null)
    {
        var quote = await QuoteAsync(booking, cancellationToken);

        var cancellation = new Cancellation
        {
            BookingId = booking.Id,
            RequestedAt = _timeProvider.GetUtcNow().UtcDateTime,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            AmountPaid = quote.AmountPaid,
            FeeAmount = quote.FeeAmount,
            RefundAmount = quote.RefundAmount,
            Language = string.IsNullOrWhiteSpace(language) ? null : Service.Helpers.Mail.EmailTemplates.NormalizeLanguage(language)
        };

        var payment = booking.Payments.Where(p => p.Status == PaymentStatus.Paid).OrderByDescending(p => p.PaidAt).FirstOrDefault();
        Refund? refund = null;
        if (payment is not null)
        {
            // The refund is only requested here; it becomes Completed when the payment provider confirms it.
            refund = new Refund
            {
                PaymentId = payment.Id,
                Amount = quote.RefundAmount,
                Currency = payment.Currency,
                Reason = cancellation.Reason,
                InitiatedByUserId = actorUserId,
                Status = quote.IsRefundable && quote.RefundAmount > 0 ? RefundStatus.Pending : RefundStatus.NotEligible
            };
            cancellation.Refunds.Add(refund);
        }

        booking.Cancellation = cancellation;
        booking.Status = BookingStatus.Cancelled;
        booking.ExpiresAt = null;
        _bookingRepository.Update(booking);

        var tickets = booking.Passengers.SelectMany(p => p.Tickets).ToList();
        foreach (var ticket in tickets)
        {
            ticket.Status = TicketStatus.Cancelled;
        }

        await _seatService.ReleaseSeatsAsync(tickets.Select(t => t.FlightSeatId).OfType<int>().ToList(), cancellationToken);

        // A hold that was never paid is not a trip: releasing it (for example when the traveller changes seats) is not news.
        if (booking.UserId is int userId && payment is not null)
        {
            // Requested, not paid back: the money is only said to be on its way once Stripe confirms it (a refund notification follows).
            var refundText = refund is { Status: RefundStatus.Pending }
                ? $" A refund of {quote.RefundAmount:0.00} {payment.Currency} has been requested to the card you paid with; we will tell you when it is confirmed."
                : " Under the fare rules no refund is due.";
            await _notificationService.QueueOnceAsync($"booking:{booking.Id}:cancelled", userId, booking.Id, NotificationType.BookingCancelled,
                "Booking Cancelled", $"Your booking {booking.Pnr} has been cancelled.{refundText}", cancellationToken);
        }

        try
        {
            await _bookingRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // The booking changed while it was being cancelled (a second cancel, or a payment arriving): nothing was saved.
            throw new ConflictException("Bu bron eyni anda dəyişdirilib. Onu yoxlayıb yenidən cəhd edin.");
        }

        // The booking is cancelled for good once it is saved. Asking Stripe happens after that, so a failure there can only
        // leave the refund queued or failed; it can never undo the cancellation or send the money twice.
        if (refund is { Status: RefundStatus.Pending } && payment!.Method == PaymentMethod.StripeCheckout)
        {
            try
            {
                await _refundService.SubmitAsync(refund.Id, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Refund {RefundId} of booking {Pnr} could not be sent to Stripe; the background job will try again.", refund.Id, booking.Pnr);
            }
        }

        return booking.ToDto();
    }

    /// <summary>
    /// lastName is null for admins, who can open any booking. A booking made under an account can only be cancelled by that account:
    /// knowing the reference and a passenger's name is not enough to cancel someone's paid trip.
    /// </summary>
    private async Task<Booking> GetCancellableBookingAsync(string pnr, string? lastName, int? userId, CancellationToken cancellationToken, bool admin = false, bool byOwner = false)
    {
        var booking = await _bookingRepository.GetByPnrAsync(BookingService.NormalizePnr(pnr), cancellationToken);
        // Through the confirmed link the account itself is the proof (it must own the booking); otherwise a passenger's last name is.
        var known = byOwner ? booking?.UserId is int bookedBy && bookedBy == userId : admin || (booking is not null && BookingService.HasPassengerNamed(booking, lastName));
        if (booking is null || !known)
        {
            throw new NotFoundException(BookingService.BookingNotFoundMessage);
        }

        if (!admin && booking.UserId is int owner)
        {
            if (userId is null)
            {
                throw new UnauthorizedException("Bronu ləğv etmək üçün onu yaratmış hesaba daxil olun.");
            }

            if (userId != owner)
            {
                throw new NotFoundException(BookingService.BookingNotFoundMessage);
            }
        }

        switch (booking.Status)
        {
            case BookingStatus.Cancelled:
                throw new ConflictException("Bu bron artıq ləğv edilib.");
            case BookingStatus.Expired:
            case BookingStatus.Completed:
                throw new BusinessRuleException("Bu bronu artıq ləğv etmək mümkün deyil.");
        }

        if (FirstLeg(booking).Flight.DepartureAt <= _timeProvider.GetUtcNow().UtcDateTime)
        {
            throw new BusinessRuleException("Səyahət artıq başlayıb, ona görə bronu onlayn ləğv etmək mümkün deyil.");
        }

        return booking;
    }

    // Fare rules are taken from the first flight: its cabin and the days left until it departs.
    private Task<CancellationQuoteDto> QuoteAsync(Booking booking, CancellationToken cancellationToken)
    {
        var firstLeg = FirstLeg(booking);
        var amountPaid = booking.Payments.Where(p => p.Status == PaymentStatus.Paid).Sum(p => p.Amount);
        return _policyService.GetQuoteAsync(firstLeg.CabinClass, firstLeg.Flight.DepartureAt, amountPaid, cancellationToken);
    }

    private static BookingFlight FirstLeg(Booking booking) => booking.Flights.OrderBy(f => f.LegOrder).First();
}
