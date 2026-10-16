using Domain.Entities;
using Domain.Enums;
using FluentValidation;
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
    private readonly IValidator<CancelBookingRequestDto> _validator;
    private readonly TimeProvider _timeProvider;

    public CancellationService(
        IBookingRepository bookingRepository,
        ICancellationPolicyService policyService,
        ISeatService seatService,
        INotificationService notificationService,
        IValidator<CancelBookingRequestDto> validator,
        TimeProvider timeProvider)
    {
        _bookingRepository = bookingRepository;
        _policyService = policyService;
        _seatService = seatService;
        _notificationService = notificationService;
        _validator = validator;
        _timeProvider = timeProvider;
    }

    public async Task<CancellationQuoteDto> GetQuoteAsync(string pnr, string lastName, CancellationToken cancellationToken = default)
    {
        var booking = await GetCancellableBookingAsync(pnr, lastName, cancellationToken);
        return await QuoteAsync(booking, cancellationToken);
    }

    public async Task<BookingDto> CancelAsync(CancelBookingRequestDto request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var booking = await GetCancellableBookingAsync(request.Pnr, request.LastName, cancellationToken);
        return await CancelBookingAsync(booking, request.Reason, cancellationToken);
    }

    public async Task<BookingDto> CancelByAdminAsync(string pnr, string? reason, CancellationToken cancellationToken = default)
    {
        var booking = await GetCancellableBookingAsync(pnr, null, cancellationToken);
        return await CancelBookingAsync(booking, reason, cancellationToken);
    }

    private async Task<BookingDto> CancelBookingAsync(Booking booking, string? reason, CancellationToken cancellationToken)
    {
        var quote = await QuoteAsync(booking, cancellationToken);

        var cancellation = new Cancellation
        {
            BookingId = booking.Id,
            RequestedAt = _timeProvider.GetUtcNow().UtcDateTime,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            AmountPaid = quote.AmountPaid,
            FeeAmount = quote.FeeAmount,
            RefundAmount = quote.RefundAmount
        };

        var payment = booking.Payments.Where(p => p.Status == PaymentStatus.Paid).OrderByDescending(p => p.PaidAt).FirstOrDefault();
        if (payment is not null)
        {
            // The refund is only requested here; it becomes Completed when the payment provider confirms it.
            cancellation.Refunds.Add(new Refund
            {
                PaymentId = payment.Id,
                Amount = quote.RefundAmount,
                Status = quote.IsRefundable && quote.RefundAmount > 0 ? RefundStatus.Pending : RefundStatus.NotEligible
            });
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

        if (booking.UserId is int userId)
        {
            var refundText = payment is null
                ? string.Empty
                : quote.RefundAmount > 0 ? $" An estimated refund of {quote.RefundAmount:0.00} has been requested." : " Under the fare rules no refund is due.";
            await _notificationService.QueueAsync(userId, booking.Id, NotificationType.BookingCancelled,
                "Booking Cancelled", $"Your booking {booking.Pnr} has been cancelled.{refundText}", cancellationToken);
        }

        await _bookingRepository.SaveChangesAsync(cancellationToken);
        return booking.ToDto();
    }

    /// <summary>lastName is null for admins, who can open any booking.</summary>
    private async Task<Booking> GetCancellableBookingAsync(string pnr, string? lastName, CancellationToken cancellationToken)
    {
        var booking = await _bookingRepository.GetByPnrAsync(BookingService.NormalizePnr(pnr), cancellationToken);
        if (booking is null || (lastName is not null && !BookingService.HasPassengerNamed(booking, lastName)))
        {
            throw new NotFoundException(BookingService.BookingNotFoundMessage);
        }

        switch (booking.Status)
        {
            case BookingStatus.Cancelled:
                throw new ConflictException("This booking has already been cancelled.");
            case BookingStatus.Expired:
            case BookingStatus.Completed:
                throw new BusinessRuleException("This booking can no longer be cancelled.");
        }

        if (FirstLeg(booking).Flight.DepartureAt <= _timeProvider.GetUtcNow().UtcDateTime)
        {
            throw new BusinessRuleException("The trip has already started, so the booking can no longer be cancelled online.");
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
