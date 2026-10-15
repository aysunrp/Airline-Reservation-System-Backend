using Domain.Common;

namespace Domain.Entities;

public class Airport : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
