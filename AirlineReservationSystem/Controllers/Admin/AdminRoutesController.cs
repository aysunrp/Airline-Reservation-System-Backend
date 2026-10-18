using AirlineReservationSystem.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Helpers.DTOs.Flight;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Controllers.Admin;

/// <summary>Admin "Routes" page.</summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/admin/routes")]
[Produces("application/json")]
public class AdminRoutesController : ControllerBase
{
    private readonly IAdminRouteService _routeService;

    public AdminRoutesController(IAdminRouteService routeService)
    {
        _routeService = routeService;
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RouteDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RouteDto>>> GetAll([FromQuery] string? search, CancellationToken cancellationToken)
        => Ok(await _routeService.GetAllAsync(search, cancellationToken));

    [HttpGet("{id:int}")]
    [ProducesResponseType<RouteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RouteDto>> Get(int id, CancellationToken cancellationToken)
        => Ok(await _routeService.GetAsync(id, cancellationToken));

    [HttpPost]
    [ProducesResponseType<RouteDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RouteDto>> Create(RouteRequestDto request, CancellationToken cancellationToken)
    {
        var route = await _routeService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = route.Id }, route);
    }

    /// <summary>The airports of a route used by flights or schedules cannot change (422).</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<RouteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<RouteDto>> Update(int id, RouteRequestDto request, CancellationToken cancellationToken)
        => Ok(await _routeService.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _routeService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
