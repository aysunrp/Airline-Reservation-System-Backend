using Domain.Enums;
using Repository.Repositories.Interfaces;
using Service.Services.Interfaces;

namespace Service.Services;

public class BookingExpiryService : IBookingExpiryService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly ISeatService _seatService;
    private readonly TimeProvider _timeProvider;

    public BookingExpiryService(IBookingRepository bookingRepository, ISeatService seatService, TimeProvider timeProvider)
    {
        _bookingRepository = bookingRepository;
        _seatService = seatService;
        _timeProvider = timeProvider;
    }

    public async Task<int> ReleaseExpiredAsync(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var expired = await _bookingRepository.GetExpiredPendingAsync(now, cancellationToken);
        if (expired.Count == 0)
        {
            return 0;
        }

        var seatIds = new List<int>();
        foreach (var booking in expired)
        {
            booking.Status = BookingStatus.Expired;
            _bookingRepository.Update(booking);

            // Its Stripe page closed before the seats were let go (the hold outlasts it), so nothing can be paid on it any more.
            foreach (var payment in booking.Payments.Where(p => p.Status == PaymentStatus.Pending))
            {
                payment.Status = PaymentStatus.Cancelled;
            }

            foreach (var ticket in booking.Passengers.SelectMany(p => p.Tickets))
            {
                // A cancelled ticket no longer counts for the one-active-ticket-per-seat index.
                ticket.Status = TicketStatus.Cancelled;
                if (ticket.FlightSeatId is int seatId)
                {
                    seatIds.Add(seatId);
                }
            }
        }

        await _seatService.ReleaseSeatsAsync(seatIds, cancellationToken);
        await _bookingRepository.SaveChangesAsync(cancellationToken);

        return expired.Count;
    }
}
