using Service.Helpers.DTOs.Reservation;

namespace Service.Services.Interfaces;

/// <summary>
/// Cancelling a paid booking of an account is confirmed by e-mail: asking only sends a link to the account's address, and the booking
/// is cancelled when that link is opened and confirmed. Holds that were never paid are released at once, as before.
/// </summary>
public interface ICancellationLinkService
{
    /// <summary>
    /// The customer's "Cancel booking". For a paid booking of an account (while e-mail can be sent) it e-mails a confirmation link and
    /// returns where it went; otherwise it cancels at once and returns the booking. The usual checks come first: the account's own
    /// booking only (404/401), not cancelled already (409), not started (422). One link a minute per booking (429).
    /// </summary>
    Task<CancelRequestResult> RequestAsync(CancelBookingRequestDto request, int? userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opening the link: cancels the booking with the reason given when it was asked for. A wrong, used or expired link gives a 422
    /// with its code (link_invalid, link_used, link_expired); a booking that can no longer be cancelled answers as the cancel itself does.
    /// </summary>
    Task<BookingDto> ConfirmAsync(ConfirmCancellationRequestDto request, CancellationToken cancellationToken = default);
}
