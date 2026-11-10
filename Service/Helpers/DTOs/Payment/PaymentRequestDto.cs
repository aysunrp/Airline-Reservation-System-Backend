using Domain.Enums;

namespace Service.Helpers.DTOs.Payment;

/// <summary>
/// Payment page form. Card details are only checked for format; nothing but the last four digits and the brand is stored.
/// </summary>
public class PaymentRequestDto
{
    public string Pnr { get; set; } = string.Empty;
    public PaymentMethod Method { get; set; }

    public string? CardholderName { get; set; }
    public string? CardNumber { get; set; }
    public int? ExpiryMonth { get; set; }
    public int? ExpiryYear { get; set; }
    public string? Cvv { get; set; }
}
