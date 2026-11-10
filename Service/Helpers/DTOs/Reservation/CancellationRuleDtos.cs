using Domain.Enums;

namespace Service.Helpers.DTOs.Reservation;

/// <summary>
/// One fare-rule band: cancelling at least <see cref="MinDaysBeforeDeparture"/> days before the first flight costs
/// <see cref="FeePercent"/> of the amount paid. No fee percent, or no band at all, means no refund.
/// </summary>
public record CancellationRuleDto(int Id, CabinClass CabinClass, int MinDaysBeforeDeparture, decimal? FeePercent);

public class CancellationRuleRequestDto
{
    public CabinClass CabinClass { get; set; }
    public int MinDaysBeforeDeparture { get; set; }

    /// <summary>0-100; empty means cancelling in this band gives no refund.</summary>
    public decimal? FeePercent { get; set; }
}
