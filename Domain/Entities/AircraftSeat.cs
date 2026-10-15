using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class AircraftSeat : BaseEntity
{
    public int AircraftId { get; set; }
    public int RowNumber { get; set; }
    public string Letter { get; set; } = string.Empty;
    public CabinClass CabinClass { get; set; }
    public bool IsExtraLegroom { get; set; }

    public Aircraft Aircraft { get; set; } = null!;
}
