using Domain.Entities;
using Domain.Enums;
using Service.Helpers.DTOs.Aircraft;

namespace Service.Helpers.Mappings;

public static class AircraftMappings
{
    /// <summary>Expects the seats to be loaded.</summary>
    public static AircraftDto ToDto(this Aircraft aircraft)
    {
        int Count(CabinClass cabin) => aircraft.Seats.Count(s => s.CabinClass == cabin);

        return new AircraftDto(
            aircraft.Id,
            aircraft.Name,
            aircraft.Model,
            aircraft.RegistrationNumber,
            aircraft.Status,
            Count(CabinClass.Business),
            Count(CabinClass.Comfort),
            Count(CabinClass.Economy),
            aircraft.Seats.Count);
    }

    public static AircraftDetailsDto ToDetailsDto(this Aircraft aircraft)
        => new(aircraft.ToDto(), aircraft.Seats
            .OrderBy(s => s.RowNumber)
            .ThenBy(s => s.Letter)
            .Select(s => new AircraftSeatDto(s.Id, s.RowNumber, s.Letter, $"{s.RowNumber}{s.Letter}", s.CabinClass))
            .ToList());
}
