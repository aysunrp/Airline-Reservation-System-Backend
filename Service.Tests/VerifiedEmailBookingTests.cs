using Domain.Entities;
using Domain.Enums;
using Service.Helpers.Exceptions;

namespace Service.Tests;

/// <summary>A signed-in customer needs a confirmed e-mail address to book, but only while the API can send the confirmation e-mail at all.</summary>
public class VerifiedEmailBookingTests
{
    private readonly BookingTestContext _context = new();

    private async Task<User> AddUserAsync(UserRole role = UserRole.User, DateTime? verifiedAt = null)
    {
        var user = new User { Id = 1, Email = "aysun@example.com", FirstName = "Aysun", LastName = "Panahli", PasswordHash = "x", Role = role, EmailVerifiedAt = verifiedAt };
        await _context.Users.AddAsync(user);
        return user;
    }

    private static CreateBookingRequestDtoFactory Request => new();

    private sealed class CreateBookingRequestDtoFactory
    {
        public Service.Helpers.DTOs.Reservation.CreateBookingRequestDto Create()
            => BookingTestContext.OneWay(BookingTestContext.Passenger("Aysun", 1990, (1, 103)));
    }

    [Fact]
    public async Task AnUnverifiedCustomer_CannotBook_UntilTheAddressIsConfirmed()
    {
        _context.AuthSettings.RequireVerifiedEmailForBooking = true;
        var user = await AddUserAsync();

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => _context.CreateBookingService().CreateBookingAsync(1, Request.Create()));
        Assert.Contains("e-poçt ünvanınızı təsdiqləyin", error.Message);
        Assert.Empty(await _context.Bookings.GetAllAsync());
        Assert.Equal(SeatStatus.Available, _context.Data.Seat(103).Status);

        user.EmailVerifiedAt = TestData.Now;
        var booking = await _context.CreateBookingService().CreateBookingAsync(1, Request.Create());
        Assert.Equal(BookingStatus.Pending, booking.Status);
    }

    [Fact]
    public async Task GuestsAndAdmins_AreNotAskedToConfirmAnAddress()
    {
        _context.AuthSettings.RequireVerifiedEmailForBooking = true;
        await AddUserAsync(UserRole.Admin);

        await _context.CreateBookingService().CreateBookingAsync(null, Request.Create());

        var second = BookingTestContext.OneWay(BookingTestContext.Passenger("Orkhan", 1988, (1, 104)));
        await _context.CreateBookingService().CreateBookingAsync(1, second);
    }

    [Fact]
    public async Task WithoutSmtp_NobodyCouldConfirm_SoTheRuleDoesNotLockCustomersOut()
    {
        _context.AuthSettings.RequireVerifiedEmailForBooking = true;
        _context.EmailSender.IsConfigured = false;
        await AddUserAsync();

        var booking = await _context.CreateBookingService().CreateBookingAsync(1, Request.Create());

        Assert.Equal(BookingStatus.Pending, booking.Status);
    }

    [Fact]
    public async Task TheRule_CanBeSwitchedOff()
    {
        _context.AuthSettings.RequireVerifiedEmailForBooking = false;
        await AddUserAsync();

        await _context.CreateBookingService().CreateBookingAsync(1, Request.Create());
    }
}
