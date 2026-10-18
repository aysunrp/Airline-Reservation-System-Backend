using AirlineReservationSystem.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Helpers.DTOs.Flight;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Controllers.Admin;

/// <summary>Admin "Flight Schedules" page.</summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/admin/schedules")]
[Produces("application/json")]
public class AdminSchedulesController : ControllerBase
{
    private readonly IAdminScheduleService _scheduleService;

    public AdminSchedulesController(IAdminScheduleService scheduleService)
    {
        _scheduleService = scheduleService;
    }

    /// <summary>All schedules, or the active ones operating on the given date (yyyy-MM-dd).</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ScheduleDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ScheduleDto>>> GetAll([FromQuery] DateOnly? date, CancellationToken cancellationToken)
        => Ok(await _scheduleService.GetAllAsync(date, cancellationToken));

    [HttpGet("{id:int}")]
    [ProducesResponseType<ScheduleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScheduleDto>> Get(int id, CancellationToken cancellationToken)
        => Ok(await _scheduleService.GetAsync(id, cancellationToken));

    [HttpPost]
    [ProducesResponseType<ScheduleDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ScheduleDto>> Create(ScheduleRequestDto request, CancellationToken cancellationToken)
    {
        var schedule = await _scheduleService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = schedule.Id }, schedule);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<ScheduleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ScheduleDto>> Update(int id, ScheduleRequestDto request, CancellationToken cancellationToken)
        => Ok(await _scheduleService.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _scheduleService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
