using Domain.Enums;
using Repository.Repositories.Interfaces;
using Service.Services.Interfaces;

namespace Service.Services;

/// <summary>
/// Closes what has flown. Cancelled flights, cancelled or expired bookings and cancelled tickets are left as they are;
/// only confirmed bookings move to Completed, so My Trips and the admin pages can tell past trips apart.
/// </summary>
public class TripCompletionService : ITripCompletionService
{
    private readonly IFlightRepository _flightRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly TimeProvider _timeProvider;

    public TripCompletionService(IFlightRepository flightRepository, IBookingRepository bookingRepository, TimeProvider timeProvider)
    {
        _flightRepository = flightRepository;
        _bookingRepository = bookingRepository;
        _timeProvider = timeProvider;
    }

    public async Task<TripCompletionResult> CompleteFinishedAsync(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var flights = await _flightRepository.GetLandedOpenAsync(now, cancellationToken);
        foreach (var flight in flights)
        {
            flight.Status = FlightStatus.Completed;
        }

        int tickets = 0, bookings = 0;
        foreach (var booking in await _bookingRepository.GetConfirmedWithLandedFlightsAsync(now, cancellationToken))
        {
            var landedLegs = booking.Flights.Where(f => f.Flight.ArrivalAt <= now).Select(f => f.Id).ToHashSet();
            foreach (var ticket in booking.Passengers.SelectMany(p => p.Tickets)
                         .Where(t => landedLegs.Contains(t.BookingFlightId) && t.Status is TicketStatus.Issued or TicketStatus.CheckedIn))
            {
                ticket.Status = TicketStatus.Used;
                tickets++;
            }

            if (landedLegs.Count == booking.Flights.Count)
            {
                booking.Status = BookingStatus.Completed;
                bookings++;
            }
        }

        if (flights.Count + tickets + bookings > 0)
        {
            // Flights and bookings share one unit of work, so one save stores both.
            await _bookingRepository.SaveChangesAsync(cancellationToken);
        }

        return new TripCompletionResult(flights.Count, tickets, bookings);
    }
}
