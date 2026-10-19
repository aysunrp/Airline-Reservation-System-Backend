namespace Service.Helpers.DTOs.Reservation;

/// <summary>A promo code as the admin sees it, including its validity window and how often it was used.</summary>
public record AdminPromoCodeDto(
    int Id,
    string Code,
    int DiscountPercent,
    DateTime? ValidFrom,
    DateTime? ValidTo,
    int? MaxUses,
    int UsedCount,
    bool IsActive);

public class PromoCodeRequestDto
{
    /// <summary>Letters and digits; stored in upper case, customers can type it in any case.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Discount on the booking subtotal, 1-100.</summary>
    public int DiscountPercent { get; set; }

    /// <summary>Optional validity window (UTC); open on a side that is left empty.</summary>
    public DateTime? ValidFrom { get; set; }

    public DateTime? ValidTo { get; set; }

    /// <summary>Optional limit on paid bookings using the code.</summary>
    public int? MaxUses { get; set; }

    public bool IsActive { get; set; } = true;
}
