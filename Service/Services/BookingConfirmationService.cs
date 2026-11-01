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

    public async Task<bool> ConfirmAsync(Booking booking, decimal amountPaid, CancellationToken cancellationToken = default)
    {
        var tickets = booking.Passengers.SelectMany(p => p.Tickets).ToList();
        var seatIds = tickets.Select(t => t.FlightSeatId).OfType<int>().ToList();
        var seats = await _flightSeatRepository.GetTrackedByIdsAsync(seatIds, cancellationToken);

        // Every seat is checked before any is claimed: a seat sold to someone else leaves the whole booking unconfirmed.
        if (seats.Count != seatIds.Distinct().Count() || seats.Any(s => s.Status is SeatStatus.Booked or SeatStatus.Blocked))
        {
            return false;
        }

        booking.Status = BookingStatus.Confirmed;
        booking.ExpiresAt = null;
        _bookingRepository.Update(booking);

        foreach (var ticket in tickets)
        {
            ticket.Status = TicketStatus.Issued;
        }

        foreach (var seat in seats)
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

        return true;
    }
}
