namespace Service.Helpers.DTOs.Reservation;

public record CancellationQuoteDto(
    int DaysBeforeDeparture,
    decimal AmountPaid,
    decimal? FeePercent,
    decimal FeeAmount,
    decimal RefundAmount,
    bool IsRefundable);
