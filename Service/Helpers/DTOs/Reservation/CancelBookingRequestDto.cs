namespace Service.Helpers.DTOs.Reservation;

public class CancelBookingRequestDto
{
    public string Pnr { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Reason { get; set; }

    /// <summary>The page language (en, az, ru) for the cancellation and refund e-mails.</summary>
    public string? Language { get; set; }
}
