using Domain.Entities;
using Service.Helpers.DTOs.Flight;

namespace Service.Helpers.Mappings;

public static class ScheduleMappings
{
    /// <summary>Expects the route airports, default aircraft, fares and stop airports to be loaded.</summary>
    public static ScheduleDto ToDto(this FlightSchedule schedule)
        => new(
            schedule.Id,
            schedule.FlightNumber,
            schedule.RouteId,
            $"{schedule.Route.Origin.Code}-{schedule.Route.Destination.Code}",
            schedule.Route.Origin.City,
            schedule.Route.Destination.City,
            schedule.DefaultAircraftId,
            schedule.DefaultAircraft?.Model,
            schedule.DepartureTime,
            schedule.ArrivalTime,
            schedule.ArrivalDayOffset,
            schedule.DaysOfWeek,
            schedule.ValidFrom,
            schedule.ValidTo,
            schedule.IsActive,
            schedule.Fares.OrderBy(f => f.CabinClass).ToDictionary(f => f.CabinClass, f => f.BasePrice),
            schedule.Stops.OrderBy(s => s.SequenceNo).Select(s => s.ToDto()).ToList());

    /// <summary>Expects the stop airport to be loaded.</summary>
    public static ScheduleStopDto ToDto(this FlightScheduleStop stop)
        => new(stop.SequenceNo, stop.Airport.Code, stop.Airport.City, stop.ArrivalOffsetMinutes, stop.LayoverMinutes);
}
