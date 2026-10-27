using Domain.Entities;
using Domain.Enums;
using Repository.Repositories.Interfaces;
using Service.Services.Interfaces;

namespace Service.Services;

public class BookingConfirmationService : IBookingConfirmationService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IFlightSeatRepository _flightSeatRepository;
    private readonly INotificationService _notificationService;

    public BookingConfirmationService(IBookingRepository bookingRepository, IFlightSeatRepository flightSeatRepository, INotificationService notificationService)
    {
        _bookingRepository = bookingRepository;
        _flightSeatRepository = flightSeatRepository;
        _notificationService = notificationService;
    }

    public async Task ConfirmAsync(Booking booking, decimal amountPaid, CancellationToken cancellationToken = default)
    {
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
                "Payment Successful", $"We received your payment of {amountPaid:0.00} for booking {booking.Pnr}.", cancellationToken);
        }
    }
}
