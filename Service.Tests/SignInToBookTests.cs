using Domain.Enums;
using Service.Helpers.Exceptions;

namespace Service.Tests;

/// <summary>The rule that only a signed-in account can start a booking.</summary>
public class SignInToBookTests
{
    private readonly BookingTestContext _context = new();

    public SignInToBookTests()
    {
        _context.AuthSettings.RequireSignInToBook = true;
    }

    [Fact]
    public async Task AGuest_CannotBook_AndNothingIsHeld()
    {
        var request = BookingTestContext.OneWay(BookingTestContext.Passenger("Aysun", 1990, (1, 103)));

        var error = await Assert.ThrowsAsync<UnauthorizedException>(() => _context.CreateBookingService().CreateBookingAsync(null, request));

        Assert.Equal("Sign in to book a flight.", error.Message);
        Assert.Empty(_context.Bookings.All);
        Assert.Equal(SeatStatus.Available, _context.Data.Seat(103).Status);
    }

    [Fact]
    public async Task ASignedInAccount_CanBook_AndTheBookingIsItsOwn()
    {
        var request = BookingTestContext.OneWay(BookingTestContext.Passenger("Aysun", 1990, (1, 103)));

        var booking = await _context.CreateBookingService().CreateBookingAsync(1, request);

        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.Equal(1, Assert.Single(_context.Bookings.All).UserId);
    }

    [Fact]
    public async Task ThePriceQuote_StaysOpenToGuests_BecauseItSavesAndHoldsNothing()
    {
        var request = BookingTestContext.OneWay(BookingTestContext.Passenger("Aysun", 1990, (1, 103)));

        var quote = await _context.CreateBookingService().QuoteAsync(request);

        Assert.True(quote.Total > 0);
        Assert.Empty(_context.Bookings.All);
    }
}
