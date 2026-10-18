using System.ComponentModel.DataAnnotations;

namespace Service.Helpers.DTOs.Reservation;

/// <summary>Admin "Cancel Booking": the booking comes from the route; the reason is optional.</summary>
public class AdminCancelBookingRequestDto
{
    [MaxLength(500)]
    public string? Reason { get; set; }
}
