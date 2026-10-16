using Domain.Enums;

namespace Service.Helpers.DTOs.Seat;

public record SeatDto(
    int Id,
    int RowNumber,
    string Letter,
    string Label,
    CabinClass CabinClass,
    bool IsExtraLegroom,
    bool IsAvailable);

public record SeatMapDto(int FlightId, IReadOnlyList<SeatDto> Seats);
