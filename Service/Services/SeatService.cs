using Domain.Entities;
using Domain.Enums;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Seat;
using Service.Helpers.Exceptions;
using Service.Services.Interfaces;

namespace Service.Services;

public class SeatService : ISeatService
{
    private readonly IFlightRepository _flightRepository;
    private readonly IFlightSeatRepository _flightSeatRepository;
    private readonly TimeProvider _timeProvider;

    public SeatService(IFlightRepository flightRepository, IFlightSeatRepository flightSeatRepository, TimeProvider timeProvider)
    {
        _flightRepository = flightRepository;
        _flightSeatRepository = flightSeatRepository;
        _timeProvider = timeProvider;
    }

    public async Task<SeatMapDto> GetSeatMapAsync(int flightId, CabinClass? cabinClass = null, CancellationToken cancellationToken = default)
    {
        if (await _flightRepository.GetByIdAsync(flightId, cancellationToken) is null)
        {
            throw new NotFoundException("Flight not found.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var seats = await _flightSeatRepository.GetByFlightIdAsync(flightId, cancellationToken);

        var result = seats
            .Where(s => cabinClass is null || s.AircraftSeat.CabinClass == cabinClass)
            .Select(s => new SeatDto(
                s.Id,
                s.AircraftSeat.RowNumber,
                s.AircraftSeat.Letter,
                Label(s.AircraftSeat),
                s.AircraftSeat.CabinClass,
                s.AircraftSeat.IsExtraLegroom,
                IsFree(s, now)))
            .ToList();

        return new SeatMapDto(flightId, result);
    }

    /// <summary>A seat can be taken when it is available or its hold has run out.</summary>
    internal static bool IsFree(FlightSeat seat, DateTime now)
        => seat.Status == SeatStatus.Available
           || (seat.Status == SeatStatus.Held && seat.HeldUntil is not null && seat.HeldUntil <= now);

    internal static string Label(AircraftSeat seat) => $"{seat.RowNumber}{seat.Letter}";
}
