using Domain.Enums;

namespace Service.Helpers.DTOs.Seat;

/// <summary>
/// Admin seat inventory. Status is the effective state: a hold that has run out is shown as Available.
/// Held = reserved (awaiting payment), Booked = occupied.
/// </summary>
public record InventorySeatDto(int Id, int RowNumber, string Letter, string Label, CabinClass CabinClass, SeatStatus Status);

public record SeatInventoryDto(
    int FlightId,
    string FlightNumber,
    int Available,
    int Held,
    int Booked,
    int Blocked,
    IReadOnlyList<InventorySeatDto> Seats);
