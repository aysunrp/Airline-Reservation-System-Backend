using Domain.Entities;
using Service.Helpers.DTOs.Flight;

namespace Service.Helpers.Mappings;

public static class RouteMappings
{
    /// <summary>Expects the route airports to be loaded.</summary>
    public static RouteDto ToDto(this Route route)
        => new(
            route.Id,
            $"{route.Origin.Code}-{route.Destination.Code}",
            route.Origin.Code,
            route.Origin.City,
            route.Destination.Code,
            route.Destination.City,
            route.DistanceKm,
            route.DurationMinutes,
            route.IsActive);
}
