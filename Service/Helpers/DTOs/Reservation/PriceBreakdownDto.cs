namespace Service.Helpers.DTOs.Reservation;

public record PriceBreakdownDto(
    decimal BaseFare,
    decimal Baggage,
    decimal Meals,
    decimal Taxes,
    decimal Subtotal,
    string? PromoCode,
    int PromoPercent,
    decimal Discount,
    decimal Total);
