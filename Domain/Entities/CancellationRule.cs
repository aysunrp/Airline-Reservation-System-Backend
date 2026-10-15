using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class CancellationRule : BaseEntity
{
    public CabinClass CabinClass { get; set; }
    public int MinDaysBeforeDeparture { get; set; }
    public decimal? FeePercent { get; set; }
}
