using Domain.Common;

namespace Domain.Entities;

public class BaggageOption : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int WeightKg { get; set; }
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
}
