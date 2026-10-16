using Domain.Enums;
using Service.Helpers.DTOs.Seat;

namespace Service.Services.Interfaces;

public interface ISeatService
{
    /// <summary>Seat map of a flight, optionally for one cabin, with live availability.</summary>
    Task<SeatMapDto> GetSeatMapAsync(int flightId, CabinClass? cabinClass = null, CancellationToken cancellationToken = default);
}
