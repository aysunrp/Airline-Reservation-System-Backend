using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class Ticket : BaseEntity
{
    public int BookingPassengerId { get; set; }
    public int BookingFlightId { get; set; }
    public int? FlightSeatId { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public decimal FareAmount { get; set; }
    public int? BaggageOptionId { get; set; }
    public decimal BaggagePrice { get; set; }
    public int? MealOptionId { get; set; }
    public decimal MealPrice { get; set; }
    public TicketStatus Status { get; set; } = TicketStatus.Issued;

    public BookingPassenger BookingPassenger { get; set; } = null!;
    public BookingFlight BookingFlight { get; set; } = null!;
    public FlightSeat? FlightSeat { get; set; }
    public BaggageOption? BaggageOption { get; set; }
    public MealOption? MealOption { get; set; }
}
