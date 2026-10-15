using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class FlightSeat : BaseEntity
{
    public int FlightId { get; set; }
    public int AircraftSeatId { get; set; }
    public SeatStatus Status { get; set; } = SeatStatus.Available;
    public DateTime? HeldUntil { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Flight Flight { get; set; } = null!;
    public AircraftSeat AircraftSeat { get; set; } = null!;
}
