using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Payment;
using Service.Helpers.DTOs.Reservation;
using Service.Helpers.Exceptions;
using Service.Helpers.Mappings;
using Service.Helpers.Validators;
using Service.Services.Interfaces;

namespace Service.Services;

/// <summary>
/// Payments are recorded against a mock provider: no real payment service is contacted yet,
/// so a valid request is treated as approved.
/// </summary>
public class PaymentService : IPaymentService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IFlightSeatRepository _flightSeatRepository;
    private readonly IPromoCodeRepository _promoCodeRepository;
    private readonly IBookingExpiryService _expiryService;
    private readonly INotificationService _notificationService;
    private readonly IValidator<PaymentRequestDto> _validator;
    private readonly TimeProvider _timeProvider;

    public PaymentService(
        IBookingRepository bookingRepository,
        IFlightSeatRepository flightSeatRepository,
        IPromoCodeRepository promoCodeRepository,
        IBookingExpiryService expiryService,
        INotificationService notificationService,
        IValidator<PaymentRequestDto> validator,
        TimeProvider timeProvider)
    {
        _bookingRepository = bookingRepository;
        _flightSeatRepository = flightSeatRepository;
        _promoCodeRepository = promoCodeRepository;
        _expiryService = expiryService;
        _notificationService = notificationService;
        _validator = validator;
        _timeProvider = timeProvider;
    }

    public async Task<BookingDto> PayAsync(PaymentRequestDto request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var booking = await _bookingRepository.GetByPnrAsync(BookingService.NormalizePnr(request.Pnr), cancellationToken)
                      ?? throw new NotFoundException("Booking not found.");

        if (booking.Status == BookingStatus.Confirmed)
        {
            throw new ConflictException("This booking has already been paid.");
        }

        if (booking.Status != BookingStatus.Pending)
        {
            throw new BusinessRuleException("This booking can no longer be paid.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (booking.ExpiresAt is not null && booking.ExpiresAt <= now)
        {
            await _expiryService.ReleaseExpiredAsync(cancellationToken);
            throw new BusinessRuleException("The time to pay for this booking has run out and the seats were released. Please book again.");
        }

        await UsePromoCodeAsync(booking, cancellationToken);

        var cardDigits = PaymentRequestValidator.Digits(request.CardNumber);
        var isCard = request.Method == PaymentMethod.Card;
        booking.Payments.Add(new Payment
        {
            BookingId = booking.Id,
            Amount = booking.TotalAmount,
            Method = request.Method,
            Status = PaymentStatus.Paid,
            ProviderReference = $"MOCK-{Guid.NewGuid():N}",
            CardLast4 = isCard ? cardDigits[^4..] : null,
            CardBrand = isCard ? DetectCardBrand(cardDigits) : null,
            PaidAt = now
        });

        booking.Status = BookingStatus.Confirmed;
        booking.ExpiresAt = null;
        _bookingRepository.Update(booking);

        var tickets = booking.Passengers.SelectMany(p => p.Tickets).ToList();
        foreach (var ticket in tickets)
        {
            ticket.Status = TicketStatus.Issued;
        }

        var seatIds = tickets.Select(t => t.FlightSeatId).OfType<int>().ToList();
        foreach (var seat in await _flightSeatRepository.GetTrackedByIdsAsync(seatIds, cancellationToken))
        {
            seat.Status = SeatStatus.Booked;
            seat.HeldUntil = null;
            _flightSeatRepository.Update(seat);
        }

        if (booking.UserId is int userId)
        {
            await _notificationService.QueueAsync(userId, booking.Id, NotificationType.BookingConfirmed,
                "Booking Confirmed", $"Your booking {booking.Pnr} is confirmed.", cancellationToken);
            await _notificationService.QueueAsync(userId, booking.Id, NotificationType.PaymentSuccessful,
                "Payment Successful", $"We received your payment of {booking.TotalAmount:0.00} for booking {booking.Pnr}.", cancellationToken);
        }

        try
        {
            await _bookingRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictException("The booking was changed while the payment was processed. Please check the booking and try again.");
        }

        return booking.ToDto();
    }

    private async Task UsePromoCodeAsync(Booking booking, CancellationToken cancellationToken)
    {
        if (booking.PromoCodeId is not int promoCodeId)
        {
            return;
        }

        var promoCode = await _promoCodeRepository.GetByIdAsync(promoCodeId, cancellationToken);
        if (promoCode is null)
        {
            return;
        }

        // The discount was granted when the booking was priced; the use is counted once it is paid.
        if (promoCode.MaxUses is not null && promoCode.UsedCount >= promoCode.MaxUses)
        {
            throw new BusinessRuleException("This promo code has reached its usage limit. Please book again without it.");
        }

        promoCode.UsedCount++;
        _promoCodeRepository.Update(promoCode);
    }

    private static string DetectCardBrand(string digits)
    {
        if (digits.StartsWith('4'))
        {
            return "Visa";
        }

        var firstTwo = int.Parse(digits[..2]);
        var firstFour = int.Parse(digits[..4]);
        if (firstTwo is >= 51 and <= 55 || firstFour is >= 2221 and <= 2720)
        {
            return "Mastercard";
        }

        return firstTwo is 34 or 37 ? "Amex" : "Card";
    }
}
