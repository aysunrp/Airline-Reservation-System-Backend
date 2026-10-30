using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Payment;
using Service.Helpers.DTOs.Reservation;
using Service.Helpers.Exceptions;
using Service.Helpers.Mappings;
using Service.Helpers.Settings;
using Service.Helpers.Validators;
using Service.Services.Interfaces;

namespace Service.Services;

/// <summary>
/// The pretend payment: a valid request is treated as approved without any provider. It only works where Payments:AllowMockPayments
/// is on and Stripe is not configured (local demos and tests); real payments go through <see cref="CheckoutService"/>.
/// </summary>
public class PaymentService : IPaymentService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IPromoCodeRepository _promoCodeRepository;
    private readonly IBookingExpiryService _expiryService;
    private readonly IBookingConfirmationService _confirmation;
    private readonly IValidator<PaymentRequestDto> _validator;
    private readonly TimeProvider _timeProvider;
    private readonly StripeSettings _stripe;
    private readonly PaymentSettings _payments;

    public PaymentService(
        IBookingRepository bookingRepository,
        IPromoCodeRepository promoCodeRepository,
        IBookingExpiryService expiryService,
        IBookingConfirmationService confirmation,
        IValidator<PaymentRequestDto> validator,
        TimeProvider timeProvider,
        IOptions<StripeSettings> stripe,
        IOptions<PaymentSettings> payments)
    {
        _bookingRepository = bookingRepository;
        _promoCodeRepository = promoCodeRepository;
        _expiryService = expiryService;
        _confirmation = confirmation;
        _validator = validator;
        _timeProvider = timeProvider;
        _stripe = stripe.Value;
        _payments = payments.Value;
    }

    public async Task<BookingDto> PayAsync(PaymentRequestDto request, CancellationToken cancellationToken = default)
    {
        // Card details must never reach this server once real payments exist.
        if (_stripe.IsConfigured)
        {
            throw new BusinessRuleException("Kartla ödəniş təhlükəsiz Stripe səhifəsi vasitəsilə aparılır. Ödənişi orada edin.");
        }

        if (!_payments.AllowMockPayments)
        {
            throw new ServiceUnavailableException(CheckoutService.UnavailableMessage);
        }

        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var booking = await _bookingRepository.GetByPnrAsync(BookingService.NormalizePnr(request.Pnr), cancellationToken)
                      ?? throw new NotFoundException("Bron tapılmadı.");

        if (booking.Status == BookingStatus.Confirmed)
        {
            throw new ConflictException("Bu bron artıq ödənilib.");
        }

        if (booking.Status != BookingStatus.Pending)
        {
            throw new BusinessRuleException("Bu bronu artıq ödəmək mümkün deyil.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (booking.ExpiresAt is not null && booking.ExpiresAt <= now)
        {
            await _expiryService.ReleaseExpiredAsync(cancellationToken);
            throw new BusinessRuleException("Bu bronun ödəniş vaxtı bitib və oturacaqlar azad edilib. Yenidən bron edin.");
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

        if (!await _confirmation.ConfirmAsync(booking, booking.TotalAmount, cancellationToken))
        {
            throw new ConflictException("Seçilmiş oturacaqlardan bir və ya bir neçəsi indicə tutuldu. Başqa oturacaqlar seçin.");
        }

        try
        {
            await _bookingRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictException("Ödəniş emal olunarkən bron dəyişdirilib. Bronu yoxlayıb yenidən cəhd edin.");
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
            throw new BusinessRuleException("Bu promo kodun istifadə limiti dolub. Onsuz yenidən bron edin.");
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
