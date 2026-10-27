using Domain.Entities;
using Domain.Enums;
using Service.Helpers.DTOs.Reservation;
using Service.Helpers.Exceptions;

namespace Service.Tests;

/// <summary>
/// Cancelling a paid booking is confirmed by e-mail: asking sends a link to the account's address and changes nothing; the booking is
/// cancelled only when the link is confirmed. The mail server is <see cref="FakeEmailSender"/>: nothing is sent.
/// "Now" is 1 Nov 2026, the trip leaves on 10 Nov: Business is refunded in full.
/// </summary>
public class CancellationLinkTests
{
    private const int Owner = 1;
    private readonly BookingTestContext _context = new();

    public CancellationLinkTests()
    {
        _context.AuthSettings.RequireCancellationEmailConfirmation = true;
        _context.Users.AddAsync(new User
        {
            Id = Owner, Email = "aysun.account@example.com", FirstName = "Aysun", LastName = "Panahli", PasswordHash = "x", EmailVerifiedAt = TestData.Now
        }).GetAwaiter().GetResult();
    }

    private async Task<BookingDto> BookAsync(bool pay = true, int seat = 101)
    {
        var request = BookingTestContext.OneWay(BookingTestContext.Passenger("Aysun", 1990, (1, seat)));
        request.Legs[0].CabinClass = seat < 103 ? CabinClass.Business : CabinClass.Economy;
        request.ContactEmail = "typed.contact@example.com";
        var booking = await _context.CreateBookingService().CreateBookingAsync(Owner, request);
        return pay ? await _context.CreatePaymentService().PayAsync(BookingTestContext.CardPayment(booking.Pnr)) : booking;
    }

    private static CancelBookingRequestDto Cancel(string pnr, string? language = "az") => new() { Pnr = pnr, LastName = "Panahli", Reason = "Change of plans", Language = language };

    private string LatestToken()
        => System.Text.RegularExpressions.Regex.Match(_context.EmailSender.Sent[^1].TextBody, @"cancel-confirm\.html\?token=([A-Za-z0-9_\-]+)").Groups[1].Value;

    private void Later(TimeSpan by) => _context.Clock = new FixedTimeProvider(_context.Clock.GetUtcNow().UtcDateTime + by);

    private async Task<Booking> StoredAsync(string pnr) => (await _context.Bookings.GetByPnrAsync(pnr))!;

    [Fact]
    public async Task AskingToCancel_OnlySendsALinkToTheAccount_AndChangesNothing()
    {
        var booking = await BookAsync();

        var result = await _context.CreateCancellationLinkService().RequestAsync(Cancel(booking.Pnr), Owner);

        Assert.Null(result.Booking);
        Assert.Equal((true, "a" + new string('*', 12) + "@example.com", 30), (result.Link!.ConfirmationSent, result.Link.Email, result.Link.ExpiresInMinutes));
        var stored = await StoredAsync(booking.Pnr);
        Assert.Equal(BookingStatus.Confirmed, stored.Status);
        Assert.Null(stored.Cancellation);

        var mail = Assert.Single(_context.EmailSender.Sent);
        Assert.Equal("aysun.account@example.com", mail.To);
        Assert.Contains(booking.Pnr, mail.Subject);
        Assert.Contains("ləğvini təsdiqləyin", mail.Subject);
        Assert.Contains("https://aerova.test/cancel-confirm.html?token=", mail.TextBody);
        Assert.Contains("AV 205 GYD → LHR", mail.TextBody);
        Assert.Contains(booking.Price.Total.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " USD", mail.TextBody);

        var token = Assert.Single(_context.Tokens.All);
        Assert.Equal((EmailTokenPurpose.CancelBooking, (int?)booking.Id), (token.Purpose, token.BookingId));
        Assert.Equal(64, token.TokenHash.Length);
        Assert.DoesNotContain(LatestToken(), token.TokenHash);
    }

    [Fact]
    public async Task ConfirmingTheLink_CancelsTheBooking_WithTheReasonAndLanguageGivenEarlier()
    {
        var booking = await BookAsync();
        await _context.CreateCancellationLinkService().RequestAsync(Cancel(booking.Pnr), Owner);

        var cancelled = await _context.CreateCancellationLinkService().ConfirmAsync(new ConfirmCancellationRequestDto { Token = LatestToken() });

        Assert.Equal(BookingStatus.Cancelled, cancelled.Status);
        var stored = await StoredAsync(booking.Pnr);
        Assert.Equal(("Change of plans", "az"), (stored.Cancellation!.Reason, stored.Cancellation.Language));
        Assert.Equal(booking.Price.Total, cancelled.Cancellation!.RefundAmount);
        Assert.Single(_context.Refunds.All);
        Assert.NotNull(_context.Tokens.All.Single().UsedAt);

        var again = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _context.CreateCancellationLinkService().ConfirmAsync(new ConfirmCancellationRequestDto { Token = LatestToken() }));
        Assert.Equal("link_confirmed", again.Code);
        Assert.Single(_context.Refunds.All);
    }

    [Fact]
    public async Task AnExpiredLink_CancelsNothing()
    {
        var booking = await BookAsync();
        await _context.CreateCancellationLinkService().RequestAsync(Cancel(booking.Pnr), Owner);
        var token = LatestToken();
        Later(TimeSpan.FromMinutes(31));

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _context.CreateCancellationLinkService().ConfirmAsync(new ConfirmCancellationRequestDto { Token = token }));

        Assert.Equal("link_expired", error.Code);
        Assert.Equal(BookingStatus.Confirmed, (await StoredAsync(booking.Pnr)).Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("a-token-that-nobody-was-ever-sent-0123456789")]
    public async Task AWrongLink_CancelsNothing(string token)
    {
        var booking = await BookAsync();

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _context.CreateCancellationLinkService().ConfirmAsync(new ConfirmCancellationRequestDto { Token = token }));

        Assert.Equal("link_invalid", error.Code);
        Assert.Equal(BookingStatus.Confirmed, (await StoredAsync(booking.Pnr)).Status);
    }

    [Fact]
    public async Task AskingAgain_WaitsAMinute_AndTheNewLinkReplacesTheOldOne()
    {
        var booking = await BookAsync();
        await _context.CreateCancellationLinkService().RequestAsync(Cancel(booking.Pnr), Owner);
        var first = LatestToken();

        await Assert.ThrowsAsync<TooManyAttemptsException>(() => _context.CreateCancellationLinkService().RequestAsync(Cancel(booking.Pnr), Owner));

        Later(TimeSpan.FromSeconds(61));
        await _context.CreateCancellationLinkService().RequestAsync(Cancel(booking.Pnr), Owner);

        var used = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _context.CreateCancellationLinkService().ConfirmAsync(new ConfirmCancellationRequestDto { Token = first }));
        Assert.Equal("link_used", used.Code);
        await _context.CreateCancellationLinkService().ConfirmAsync(new ConfirmCancellationRequestDto { Token = LatestToken() });
        Assert.Equal(BookingStatus.Cancelled, (await StoredAsync(booking.Pnr)).Status);
    }

    [Fact]
    public async Task AHoldThatWasNeverPaid_IsReleasedAtOnce_WithoutAnyEmail()
    {
        var booking = await BookAsync(pay: false);

        var result = await _context.CreateCancellationLinkService().RequestAsync(Cancel(booking.Pnr), Owner);

        Assert.Null(result.Link);
        Assert.Equal(BookingStatus.Cancelled, result.Booking!.Status);
        Assert.Empty(_context.EmailSender.Sent);
        Assert.Empty(_context.Tokens.All);
    }

    [Fact]
    public async Task WithTheRuleOff_APaidBookingIsCancelledAtOnce()
    {
        _context.AuthSettings.RequireCancellationEmailConfirmation = false;
        var booking = await BookAsync();

        var result = await _context.CreateCancellationLinkService().RequestAsync(Cancel(booking.Pnr), Owner);

        Assert.Equal(BookingStatus.Cancelled, result.Booking!.Status);
        Assert.Empty(_context.Tokens.All);
    }

    [Fact]
    public async Task NobodyElse_CanAskForTheLink()
    {
        var booking = await BookAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => _context.CreateCancellationLinkService().RequestAsync(Cancel(booking.Pnr), 99));
        await Assert.ThrowsAsync<UnauthorizedException>(() => _context.CreateCancellationLinkService().RequestAsync(Cancel(booking.Pnr), null));

        Assert.Empty(_context.EmailSender.Sent);
        Assert.Empty(_context.Tokens.All);
    }

    [Fact]
    public async Task ACancelledBooking_GetsNoLink()
    {
        var booking = await BookAsync();
        await _context.CreateCancellationLinkService().RequestAsync(Cancel(booking.Pnr), Owner);
        await _context.CreateCancellationLinkService().ConfirmAsync(new ConfirmCancellationRequestDto { Token = LatestToken() });
        Later(TimeSpan.FromSeconds(61));

        await Assert.ThrowsAsync<ConflictException>(() => _context.CreateCancellationLinkService().RequestAsync(Cancel(booking.Pnr), Owner));
        Assert.Single(_context.EmailSender.Sent);
    }

    [Fact]
    public async Task WhenTheMailCannotGoOut_ItSaysSo_AndTheBookingStaysAsItWas()
    {
        var booking = await BookAsync();
        _context.EmailSender.Fail = true;

        await Assert.ThrowsAsync<ServiceUnavailableException>(() => _context.CreateCancellationLinkService().RequestAsync(Cancel(booking.Pnr), Owner));

        Assert.Equal(BookingStatus.Confirmed, (await StoredAsync(booking.Pnr)).Status);
    }
}
