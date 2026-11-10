using Domain.Enums;
using FluentValidation;
using Service.Helpers.DTOs.Payment;
using Service.Helpers.Exceptions;

namespace Service.Tests;

public class PaymentServiceTests
{
    private readonly BookingTestContext _context = new();

    private async Task<string> CreateBookingAsync(int? userId = 1, string? promoCode = null)
    {
        var request = BookingTestContext.OneWay(BookingTestContext.Passenger("Aysun", 1990, (1, 103)));
        request.PromoCode = promoCode;
        return (await _context.CreateBookingService().CreateBookingAsync(userId, request)).Pnr;
    }

    [Fact]
    public async Task CardPayment_ConfirmsBookingIssuesTicketsAndBooksSeats()
    {
        var pnr = await CreateBookingAsync(promoCode: "AEROVA10");

        var result = await _context.CreatePaymentService().PayAsync(BookingTestContext.CardPayment(pnr.ToLowerInvariant()));

        Assert.Equal(BookingStatus.Confirmed, result.Status);
        Assert.Null(result.ExpiresAt);
        Assert.Equal(PaymentStatus.Paid, result.PaymentStatus);
        Assert.All(result.Passengers.SelectMany(p => p.Tickets), t => Assert.Equal(TicketStatus.Issued, t.Status));
        Assert.Equal(SeatStatus.Booked, _context.Data.Seat(103).Status);
        Assert.Null(_context.Data.Seat(103).HeldUntil);

        var payment = (await _context.Bookings.GetByPnrAsync(pnr))!.Payments.Single();
        Assert.Equal(result.Price.Total, payment.Amount);
        Assert.Equal("1111", payment.CardLast4);
        Assert.Equal("Visa", payment.CardBrand);
        Assert.StartsWith("MOCK-", payment.ProviderReference);

        Assert.Equal(4, (await _context.PromoCodes.GetByIdAsync(5))!.UsedCount);
        Assert.Equal(
            new[] { NotificationType.BookingConfirmed, NotificationType.PaymentSuccessful },
            (await _context.Notifications.GetAllAsync()).Select(n => n.Type));
    }

    [Fact]
    public async Task WalletPayment_ForGuest_StoresNoCardAndSendsNoNotification()
    {
        var pnr = await CreateBookingAsync(userId: null);

        await _context.CreatePaymentService().PayAsync(new PaymentRequestDto { Pnr = pnr, Method = PaymentMethod.ApplePay });

        var payment = (await _context.Bookings.GetByPnrAsync(pnr))!.Payments.Single();
        Assert.Null(payment.CardLast4);
        Assert.Empty(await _context.Notifications.GetAllAsync());
    }

    [Fact]
    public async Task PayingTwice_ThrowsConflict()
    {
        var pnr = await CreateBookingAsync();
        var service = _context.CreatePaymentService();
        await service.PayAsync(BookingTestContext.CardPayment(pnr));

        await Assert.ThrowsAsync<ConflictException>(() => service.PayAsync(BookingTestContext.CardPayment(pnr)));
    }

    [Fact]
    public async Task PaymentAfterHoldExpired_IsRejectedAndSeatsReleased()
    {
        var pnr = await CreateBookingAsync();
        _context.Clock = new FixedTimeProvider(TestData.Now.AddMinutes(20));

        await Assert.ThrowsAsync<BusinessRuleException>(() => _context.CreatePaymentService().PayAsync(BookingTestContext.CardPayment(pnr)));

        Assert.Equal(BookingStatus.Expired, (await _context.Bookings.GetByPnrAsync(pnr))!.Status);
        Assert.Equal(SeatStatus.Available, _context.Data.Seat(103).Status);
    }

    [Fact]
    public async Task UnknownBooking_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _context.CreatePaymentService().PayAsync(BookingTestContext.CardPayment("ZZZZZZ")));
    }

    [Theory]
    [InlineData("4111 1111 1111 1112", 12, 2028, "123")] // fails the Luhn check
    [InlineData("4111 1111 1111 1111", 10, 2026, "123")] // expired last month
    [InlineData("4111 1111 1111 1111", 12, 2028, "12")]  // CVV too short
    public async Task InvalidCard_ThrowsValidation(string number, int month, int year, string cvv)
    {
        var pnr = await CreateBookingAsync();
        var request = BookingTestContext.CardPayment(pnr);
        request.CardNumber = number;
        request.ExpiryMonth = month;
        request.ExpiryYear = year;
        request.Cvv = cvv;

        await Assert.ThrowsAsync<ValidationException>(() => _context.CreatePaymentService().PayAsync(request));
    }
}
