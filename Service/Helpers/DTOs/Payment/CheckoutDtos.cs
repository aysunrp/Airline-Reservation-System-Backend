using Domain.Enums;

namespace Service.Helpers.DTOs.Payment;

/// <summary>
/// Opens Stripe's payment page for a booking. There is no amount here on purpose: the server charges the booking's own total.
/// A booking made while signed in needs that account's token; a guest booking needs the last name of one of its passengers.
/// </summary>
public class CreateCheckoutSessionRequestDto
{
    public string Pnr { get; set; } = string.Empty;
    public string? LastName { get; set; }
}

/// <summary>Where to send the customer, and until when that page can be paid.</summary>
public record CheckoutSessionDto(int PaymentId, string CheckoutUrl, DateTime ExpiresAt);

/// <summary>What the success page asks for. Only the webhook ever changes these values.</summary>
public record PaymentStatusDto(
    int PaymentId,
    PaymentStatus Status,
    string Pnr,
    BookingStatus BookingStatus,
    decimal Amount,
    string Currency,
    DateTime? PaidAt);
