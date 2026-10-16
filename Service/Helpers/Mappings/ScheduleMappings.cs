using Domain.Entities;
using Service.Helpers.DTOs.Flight;

namespace Service.Helpers.Mappings;

public static class ScheduleMappings
{
    /// <summary>Expects the route airports and default aircraft to be loaded.</summary>
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
            schedule.IsActive);
}
