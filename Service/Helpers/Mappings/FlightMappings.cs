using Domain.Entities;
using Service.Helpers.DTOs.Flight;

namespace Service.Helpers.Mappings;

public static class FlightMappings
{
    /// <summary>Expects route airports, aircraft, segments (with airports) and fares to be loaded.</summary>
    public static FlightResultDto ToResultDto(this Flight flight)
    {
        var segments = flight.Segments.OrderBy(s => s.SequenceNo).ToList();

        // A stop is the arrival airport of every segment except the last; the layover is the gap to the next departure.
        var via = segments
            .Zip(segments.Skip(1), (current, next) => new FlightStopDto(
                current.Destination.Code,
                (int)(next.DepartureAt - current.ArrivalAt).TotalMinutes))
            .ToList();

        return new FlightResultDto(
            flight.Id,
            flight.FlightNumber,
            flight.Route.Origin.Code,
            flight.Route.Origin.City,
            flight.Route.Destination.Code,
            flight.Route.Destination.City,
            flight.DepartureAt,
            flight.ArrivalAt,
            (int)(flight.ArrivalAt - flight.DepartureAt).TotalMinutes,
            via.Count,
            via,
            flight.AircraftId,
            flight.Aircraft.Model,
            flight.Status,
            flight.Fares.ToDictionary(f => f.CabinClass, f => f.BasePrice),
            segments.Select(s => new FlightSegmentDto(s.SequenceNo, s.Origin.Code, s.Destination.Code, s.DepartureAt, s.ArrivalAt)).ToList());
    }
}
