using System.Security.Cryptography;
using System.Text;
using Domain.Constants;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Payment;
using Service.Helpers.Exceptions;
using Service.Helpers.Settings;
using Service.Services.Interfaces;

namespace Service.Services;

public class CheckoutService : ICheckoutService
{
    internal const string UnavailableMessage = "Card payments are not available right now. Please try again later.";
    private const string PaymentNotFoundMessage = "Payment not found.";

    // Stripe's page may close at its own pace; the seats stay held a little longer so a payment made in its last second still finds them.
    private static readonly TimeSpan HoldMargin = TimeSpan.FromMinutes(2);

    private readonly IBookingRepository _bookingRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IPromoCodeRepository _promoCodeRepository;
    private readonly IBookingExpiryService _expiryService;
    private readonly IStripeCheckoutGateway _gateway;
    private readonly IValidator<CreateCheckoutSessionRequestDto> _validator;
    private readonly StripeSettings _stripe;
    private readonly AuthSettings _auth;
    private readonly TimeProvider _timeProvider;

    public CheckoutService(
        IBookingRepository bookingRepository,
        IPaymentRepository paymentRepository,
        IPromoCodeRepository promoCodeRepository,
        IBookingExpiryService expiryService,
        IStripeCheckoutGateway gateway,
        IValidator<CreateCheckoutSessionRequestDto> validator,
        IOptions<StripeSettings> stripe,
        IOptions<AuthSettings> auth,
        TimeProvider timeProvider)
    {
        _bookingRepository = bookingRepository;
        _paymentRepository = paymentRepository;
        _promoCodeRepository = promoCodeRepository;
        _expiryService = expiryService;
        _gateway = gateway;
        _validator = validator;
        _stripe = stripe.Value;
        _auth = auth.Value;
        _timeProvider = timeProvider;
    }

    public async Task<CheckoutSessionDto> CreateSessionAsync(int? userId, CreateCheckoutSessionRequestDto request, CancellationToken cancellationToken = default)
    {
        if (!_stripe.IsConfigured)
        {
            throw new ServiceUnavailableException(UnavailableMessage);
        }

        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var booking = await _bookingRepository.GetByPnrAsync(BookingService.NormalizePnr(request.Pnr), cancellationToken);
        if (booking is null || !CanPay(booking, userId, request.LastName))
        {
            throw new NotFoundException(BookingService.BookingNotFoundMessage);
        }

        if (booking.Status == BookingStatus.Confirmed || booking.Payments.Any(p => p.Status == PaymentStatus.Paid))
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

        var waiting = booking.Payments
            .Where(p => p.Status == PaymentStatus.Pending && p.Method == PaymentMethod.StripeCheckout)
            .OrderByDescending(p => p.Id)
            .ToList();

        // A page that is still open is offered again: a second page for the same booking could be paid twice.
        var open = waiting.FirstOrDefault(p => p.CheckoutUrl is not null && p.CheckoutExpiresAt > now.AddMinutes(1) && p.Amount == booking.TotalAmount);
        if (open is not null)
        {
            return new CheckoutSessionDto(open.Id, open.CheckoutUrl!, open.CheckoutExpiresAt!.Value);
        }

        foreach (var stale in waiting)
        {
            stale.Status = PaymentStatus.Cancelled;
        }

        await EnsurePromoCodeIsUsableAsync(booking, cancellationToken);
        var amountMinor = ToMinorUnits(booking.TotalAmount);

        var payment = new Payment
        {
            BookingId = booking.Id,
            Amount = booking.TotalAmount,
            Currency = AppConstants.Currency,
            Method = PaymentMethod.StripeCheckout,
            Status = PaymentStatus.Pending
        };
        booking.Payments.Add(payment);

        // Stripe needs at least 30 minutes; a minute more keeps the request inside that limit whatever the clocks say.
        var closesAt = now.AddMinutes(Math.Max(31, _stripe.CheckoutMinutes));
        ExtendHold(booking, closesAt + HoldMargin);
        _bookingRepository.Update(booking);
        await SaveAsync(cancellationToken);

        StripeCheckoutSession session;
        try
        {
            var site = _auth.FrontendBaseUrl.TrimEnd('/');
            session = await _gateway.CreateSessionAsync(new StripeCheckoutRequest(
                payment.Id,
                booking.Id,
                booking.Pnr,
                amountMinor,
                _stripe.Currency.ToLowerInvariant(),
                $"AEROVA booking {booking.Pnr}",
                booking.ContactEmail,
                // Fixed pages of this site only; Stripe fills in {CHECKOUT_SESSION_ID} itself.
                $"{site}/payment-success.html?payment={payment.Id}&session={{CHECKOUT_SESSION_ID}}",
                $"{site}/payment-cancel.html?payment={payment.Id}",
                closesAt), cancellationToken);
        }
        catch (ServiceUnavailableException)
        {
            payment.Status = PaymentStatus.Cancelled;
            await _bookingRepository.SaveChangesAsync(cancellationToken);
            throw;
        }

        payment.StripeCheckoutSessionId = session.Id;
        payment.CheckoutUrl = session.Url;
        payment.CheckoutExpiresAt = session.ExpiresAtUtc;
        await SaveAsync(cancellationToken);

        return new CheckoutSessionDto(payment.Id, session.Url, session.ExpiresAtUtc);
    }

    public async Task<PaymentStatusDto> GetStatusAsync(int paymentId, int? userId, bool isAdmin, string? checkoutSessionId, CancellationToken cancellationToken = default)
    {
        var payment = await _paymentRepository.GetWithBookingAsync(paymentId, cancellationToken)
                      ?? throw new NotFoundException(PaymentNotFoundMessage);

        var allowed = isAdmin
                      || (userId is int id && payment.Booking.UserId == id)
                      || SameSecret(payment.StripeCheckoutSessionId, checkoutSessionId);
        if (!allowed)
        {
            throw new NotFoundException(PaymentNotFoundMessage);
        }

        return new PaymentStatusDto(payment.Id, payment.Status, payment.Booking.Pnr, payment.Booking.Status, payment.Amount, payment.Currency, payment.PaidAt);
    }

    /// <summary>A booking of an account is paid by that account; a guest booking by someone who knows a passenger's last name.</summary>
    private static bool CanPay(Booking booking, int? userId, string? lastName)
        => booking.UserId is int owner ? userId == owner : BookingService.HasPassengerNamed(booking, lastName);

    /// <summary>Cents for USD. The stored total is already rounded to the cent, so the conversion is exact.</summary>
    public static long ToMinorUnits(decimal amount)
    {
        var minor = amount * 100m;
        if (amount <= 0 || minor != decimal.Truncate(minor))
        {
            throw new BusinessRuleException("This booking has no amount that can be charged.");
        }

        return (long)minor;
    }

    /// <summary>The seats wait for the payment page; they are never held for less time than they already were.</summary>
    private static void ExtendHold(Booking booking, DateTime holdUntil)
    {
        if (booking.ExpiresAt is null || booking.ExpiresAt < holdUntil)
        {
            booking.ExpiresAt = holdUntil;
        }

        foreach (var seat in booking.Passengers.SelectMany(p => p.Tickets).Select(t => t.FlightSeat).OfType<FlightSeat>())
        {
            if (seat.Status == SeatStatus.Held && (seat.HeldUntil is null || seat.HeldUntil < booking.ExpiresAt))
            {
                seat.HeldUntil = booking.ExpiresAt;
            }
        }
    }

    private async Task EnsurePromoCodeIsUsableAsync(Booking booking, CancellationToken cancellationToken)
    {
        if (booking.PromoCodeId is not int promoCodeId)
        {
            return;
        }

        var promoCode = await _promoCodeRepository.GetByIdAsync(promoCodeId, cancellationToken);
        if (promoCode is { MaxUses: not null } && promoCode.UsedCount >= promoCode.MaxUses)
        {
            throw new BusinessRuleException("This promo code has reached its usage limit. Please book again without it.");
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _bookingRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("This booking was changed at the same moment, for example in another window. Please refresh and try again.");
        }
    }

    private static bool SameSecret(string? stored, string? given)
        => !string.IsNullOrEmpty(stored) && !string.IsNullOrEmpty(given)
           && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(stored), Encoding.UTF8.GetBytes(given));
}
