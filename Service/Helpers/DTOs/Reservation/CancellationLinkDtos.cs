namespace Service.Helpers.DTOs.Reservation;

/// <summary>Where the confirmation link went (the address partly hidden) and how long it works. Nothing is cancelled yet.</summary>
public record CancellationLinkDto(bool ConfirmationSent, string Email, int ExpiresInMinutes);

/// <summary>Either the booking, cancelled at once, or the link that still has to be opened.</summary>
public record CancelRequestResult(BookingDto? Booking, CancellationLinkDto? Link);

public class ConfirmCancellationRequestDto
{
    /// <summary>The token from the link in the e-mail.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>The page language (en, az, ru) for the cancellation and refund e-mails that follow.</summary>
    public string? Language { get; set; }
}
