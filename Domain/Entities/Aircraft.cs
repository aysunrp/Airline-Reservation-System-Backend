using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class Aircraft : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public AircraftStatus Status { get; set; } = AircraftStatus.Active;

    public ICollection<AircraftSeat> Seats { get; set; } = new List<AircraftSeat>();
}
