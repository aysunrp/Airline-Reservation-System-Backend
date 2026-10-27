using Domain.Entities;
using Domain.Enums;
using Service.Helpers.DTOs.Reservation;
using Service.Helpers.Exceptions;
using Service.Services;

namespace Service.Tests;

/// <summary>
/// Every booking of an account is confirmed by its own e-mailed link before it can be paid: the link goes to the contact address of
/// that booking, works once and only while the seats are held, and payment is refused until it was opened.
/// </summary>
public class BookingEmailConfirmationTests
{
    private const int Owner = 1;

    private readonly BookingTestContext _context = new();

    public BookingEmailConfirmationTests()
    {
        _context.AuthSettings.RequireBookingEmailConfirmation = true;
        _context.Stripe.SecretKey = "sk_test_unit";
        _context.Stripe.WebhookSecret = "whsec_unit";
        _context.Users.AddAsync(new User { Id = Owner, Email = "aysun@example.com", FirstName = "Aysun", LastName = "Panahli", PasswordHash = "x", Role = UserRole.User, EmailVerifiedAt = TestData.Now }).GetAwaiter().GetResult();
    }

    private static CreateBookingRequestDto Request(string contactEmail = "typed.by.hand@example.com", string? language = null)
    {
        var request = BookingTestContext.OneWay(BookingTestContext.Passenger("Aysun", 1990, (1, 103)));
        request.ContactEmail = contactEmail;
        request.Language = language;
        return request;
    }

    private async Task<BookingDto> BookAsync(string contactEmail = "typed.by.hand@example.com", string? language = null)
        => await _context.CreateBookingService().CreateBookingAsync(Owner, Request(contactEmail, language));

    private void Advance(TimeSpan by) => _context.Clock = new FixedTimeProvider(_context.Clock.GetUtcNow().UtcDateTime + by);

    private string LatestToken()
        => System.Text.RegularExpressions.Regex.Match(_context.EmailSender.Sent[^1].TextBody, @"confirm-booking\.html\?token=([A-Za-z0-9_\-]+)").Groups[1].Value;

    [Fact]
    public async Task ANewBooking_SendsItsOwnLinkToTheAddressTypedOnTheBooking_AndWaitsForIt()
    {
        var booking = await BookAsync(language: "az");

        Assert.False(booking.ContactConfirmed);
        var message = Assert.Single(_context.EmailSender.Sent);
        Assert.Equal("typed.by.hand@example.com", message.To);
        Assert.Contains(booking.Pnr, message.Subject);
        Assert.Contains("https://aerova.test/confirm-booking.html?token=", message.TextBody);
        Assert.Contains("təsdiqləyin", message.Subject);

        var stored = Assert.Single(_context.Tokens.All);
        Assert.Equal(EmailTokenPurpose.BookingConfirm, stored.Purpose);
        Assert.Equal(booking.Id, stored.BookingId);
        Assert.Equal(64, stored.TokenHash.Length);
        Assert.Equal(booking.ExpiresAt, stored.ExpiresAt);
    }

    [Fact]
    public async Task EachBooking_GetsAnotherLink_EvenForTheSameAccount()
    {
        var first = await BookAsync();
        var second = await _context.CreateBookingService().CreateBookingAsync(Owner, BookingWith("Orkhan", 104));

        Assert.Equal(2, _context.EmailSender.Sent.Count);
        Assert.NotEqual(_context.Tokens.All[0].TokenHash, _context.Tokens.All[1].TokenHash);
        Assert.Equal(new[] { first.Id, second.Id }, _context.Tokens.All.Select(t => t.BookingId!.Value).ToArray());

        static CreateBookingRequestDto BookingWith(string name, int seat)
        {
            var request = BookingTestContext.OneWay(BookingTestContext.Passenger(name, 1988, (1, seat)));
            request.ContactEmail = "typed.by.hand@example.com";
            return request;
        }
    }

    [Fact]
    public async Task PaymentIsRefused_UntilTheLinkIsOpened_AndThenWorks()
    {
        var booking = await BookAsync();

        var refused = await Assert.ThrowsAsync<BusinessRuleException>(() => _context.CreateCheckoutService().CreateSessionAsync(Owner, new() { Pnr = booking.Pnr }));
        Assert.Equal("booking_unconfirmed", refused.Code);
        Assert.Empty(_context.Gateway.Created);

        await _context.CreateBookingEmailConfirmation().ConfirmAsync(LatestToken());

        var session = await _context.CreateCheckoutService().CreateSessionAsync(Owner, new() { Pnr = booking.Pnr });
        Assert.False(string.IsNullOrEmpty(session.CheckoutUrl));
        Assert.True((await _context.CreateBookingService().GetByPnrAsync(booking.Pnr, "Panahli")).ContactConfirmed);
    }

    [Fact]
    public async Task TheLink_WorksOnce_AndAnotherOpeningSaysTheBookingIsConfirmed()
    {
        var booking = await BookAsync();
        var token = LatestToken();
        var service = _context.CreateBookingEmailConfirmation();

        await service.ConfirmAsync(token);

        Assert.NotNull((await _context.Bookings.GetByIdAsync(booking.Id))!.ContactConfirmedAt);
        var again = await Assert.ThrowsAsync<BusinessRuleException>(() => service.ConfirmAsync(token));
        Assert.Equal("link_confirmed", again.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("a-token-that-nobody-ever-sent-0123456789abcdef")]
    public async Task AWrongLink_IsRefused_WithoutSayingAnythingAboutBookings(string token)
    {
        await BookAsync();

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => _context.CreateBookingEmailConfirmation().ConfirmAsync(token));

        Assert.Equal("link_invalid", error.Code);
    }

    [Fact]
    public async Task TheLink_DiesWithTheSeatHold()
    {
        var booking = await BookAsync();
        var token = LatestToken();

        Advance(TimeSpan.FromMinutes(16));

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => _context.CreateBookingEmailConfirmation().ConfirmAsync(token));
        Assert.Equal("link_expired", error.Code);
        Assert.Null((await _context.Bookings.GetByIdAsync(booking.Id))!.ContactConfirmedAt);
    }

    [Fact]
    public async Task ACancelledBooking_CannotBeConfirmedByItsLink()
    {
        var booking = await BookAsync();
        var token = LatestToken();
        (await _context.Bookings.GetByIdAsync(booking.Id))!.Status = BookingStatus.Cancelled;

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => _context.CreateBookingEmailConfirmation().ConfirmAsync(token));

        Assert.Equal("link_expired", error.Code);
    }

    [Fact]
    public async Task Resend_IsLimitedToOnePerMinute_AndTheOlderLinkStopsWorking()
    {
        var booking = await BookAsync();
        var firstToken = LatestToken();
        var service = _context.CreateBookingEmailConfirmation();

        await Assert.ThrowsAsync<TooManyAttemptsException>(() => service.ResendAsync(Owner, booking.Pnr, null));

        Advance(TimeSpan.FromSeconds(61));
        service = _context.CreateBookingEmailConfirmation();
        await service.ResendAsync(Owner, booking.Pnr, null);

        Assert.Equal(2, _context.EmailSender.Sent.Count);
        var used = await Assert.ThrowsAsync<BusinessRuleException>(() => service.ConfirmAsync(firstToken));
        Assert.Equal("link_used", used.Code);
        await service.ConfirmAsync(LatestToken());

        await Assert.ThrowsAsync<ConflictException>(() => service.ResendAsync(Owner, booking.Pnr, null));
    }

    [Fact]
    public async Task Resend_IsOnlyForTheAccountThatMadeTheBooking()
    {
        var booking = await BookAsync();
        Advance(TimeSpan.FromSeconds(61));

        await Assert.ThrowsAsync<NotFoundException>(() => _context.CreateBookingEmailConfirmation().ResendAsync(2, booking.Pnr, null));
        await Assert.ThrowsAsync<NotFoundException>(() => _context.CreateBookingEmailConfirmation().ResendAsync(Owner, "ZZZZZZ", null));
    }

    [Fact]
    public async Task Resend_SaysSoWhenEmailCannotBeSent()
    {
        var booking = await BookAsync();
        Advance(TimeSpan.FromSeconds(61));
        _context.EmailSender.Fail = true;

        await Assert.ThrowsAsync<ServiceUnavailableException>(() => _context.CreateBookingEmailConfirmation().ResendAsync(Owner, booking.Pnr, null));
    }

    [Fact]
    public async Task AFailedFirstMessage_DoesNotFailTheBooking_ItJustStaysUnconfirmed()
    {
        _context.EmailSender.Fail = true;

        var booking = await BookAsync();

        Assert.False(booking.ContactConfirmed);
        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.Empty(_context.EmailSender.Sent);
    }

    [Fact]
    public async Task WithoutSmtp_OrWithTheRuleOff_TheBookingIsConfirmedAtOnce_AndNoMailIsSent()
    {
        _context.EmailSender.IsConfigured = false;
        var withoutSmtp = await BookAsync();

        _context.EmailSender.IsConfigured = true;
        _context.AuthSettings.RequireBookingEmailConfirmation = false;
        var ruleOff = await _context.CreateBookingService().CreateBookingAsync(Owner, BookingTestContext.OneWay(BookingTestContext.Passenger("Orkhan", 1988, (1, 104))));

        Assert.True(withoutSmtp.ContactConfirmed);
        Assert.True(ruleOff.ContactConfirmed);
        Assert.Empty(_context.EmailSender.Sent);
        Assert.Empty(_context.Tokens.All);
    }
}
