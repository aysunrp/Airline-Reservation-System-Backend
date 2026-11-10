namespace Service.Helpers.DTOs.Seat;

/// <summary>Admin seat inventory: block a free seat (true) or open a blocked one again (false).</summary>
public class SeatBlockRequestDto
{
    public bool Blocked { get; set; }
}
