namespace Service.Helpers.Settings;

public class BookingSettings
{
    public const string SectionName = "Booking";

    /// <summary>How long selected seats stay held while the booking waits for payment.</summary>
    public int SeatHoldMinutes { get; set; } = 15;

    /// <summary>How often the background job releases seats of unpaid bookings whose hold has run out.</summary>
    public int ExpiryCheckIntervalSeconds { get; set; } = 60;
}
