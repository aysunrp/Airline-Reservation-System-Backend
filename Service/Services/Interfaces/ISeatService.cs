using Domain.Enums;
using Service.Helpers.DTOs.Seat;

namespace Service.Services.Interfaces;

public interface ISeatService
{
    /// <summary>Seat map of a flight, optionally for one cabin, with live availability.</summary>
    Task<SeatMapDto> GetSeatMapAsync(int flightId, CabinClass? cabinClass = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Makes held seats available again, and booked ones too when <paramref name="includeBooked"/> is set (a paid booking is cancelled).
    /// A booking that was never paid owns no seat, so releasing it must not touch a seat another traveller has booked.
    /// Does not save; the caller commits the unit of work.
    /// </summary>
    Task ReleaseSeatsAsync(IReadOnlyCollection<int> flightSeatIds, bool includeBooked = true, CancellationToken cancellationToken = default);
}
