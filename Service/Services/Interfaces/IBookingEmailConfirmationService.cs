using Domain.Entities;

namespace Service.Services.Interfaces;

/// <summary>
/// The link that confirms one booking by e-mail before it can be paid. Every booking of an account gets its own link, sent to the
/// contact address typed on the booking. Opening it needs no sign-in: the token itself is the proof of reaching that mailbox.
/// </summary>
public interface IBookingEmailConfirmationService
{
    /// <summary>True when new bookings must be confirmed by e-mail: the rule is on and the API can send e-mail at all.</summary>
    bool IsRequired { get; }

    /// <summary>Sends the link of a new booking (best effort). False, without an error, when sending failed.</summary>
    Task<bool> SendAsync(Booking booking, string? language, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a new link for the signed-in account's own booking. 404 for anybody else's booking, 409 when it is already confirmed,
    /// 429 within a minute of the last message, 503 when e-mail cannot be sent. The older link stops working.
    /// </summary>
    Task ResendAsync(int userId, string pnr, string? language, CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirms the booking behind the token. A wrong, a used and an expired link give a 422 with their own code
    /// (link_invalid, link_used, link_expired, link_confirmed when the booking is already confirmed, booking_closed).
    /// </summary>
    Task ConfirmAsync(string token, CancellationToken cancellationToken = default);
}
