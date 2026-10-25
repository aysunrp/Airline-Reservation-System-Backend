using AirlineReservationSystem.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Helpers;
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
    private readonly IFlightGenerationService _generationService;
    private readonly IAuditLogService _audit;

    public AdminSchedulesController(IAdminScheduleService scheduleService, IFlightGenerationService generationService, IAuditLogService audit)
    {
        _scheduleService = scheduleService;
        _generationService = generationService;
        _audit = audit;
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
        await _audit.RecordAsync(User.ToAuditActor(), AuditActions.ScheduleCreated, "Schedule", schedule.Id.ToString(), cancellationToken: cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = schedule.Id }, schedule);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<ScheduleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ScheduleDto>> Update(int id, ScheduleRequestDto request, CancellationToken cancellationToken)
    {
        var schedule = await _scheduleService.UpdateAsync(id, request, cancellationToken);
        await _audit.RecordAsync(User.ToAuditActor(), AuditActions.ScheduleUpdated, "Schedule", id.ToString(), cancellationToken: cancellationToken);
        return Ok(schedule);
    }

    /// <summary>
    /// Creates the missing flights of every active schedule. <paramref name="from"/> and <paramref name="to"/>
    /// (yyyy-MM-dd, departure-airport dates, both included, at most 366 days) default to today and the configured
    /// days ahead. Running it again only reports the flights as already existing; schedules or days that cannot
    /// fly are listed under "skipped" and inserts lost to a parallel run under "conflicts".
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
    [HttpPost("generate")]
    [ProducesResponseType<FlightGenerationResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FlightGenerationResultDto>> GenerateAll([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken)
    {
        var result = await _generationService.GenerateAsync(null, from, to, cancellationToken);
        await _audit.RecordAsync(User.ToAuditActor(), AuditActions.FlightsGenerated, "Schedule", "all", $"{result.Created.Count} flights created", cancellationToken);
        return Ok(result);
    }

    /// <summary>Same as generating for all schedules, limited to this one; an inactive schedule is reported as skipped.</summary>
    [Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
    [HttpPost("{id:int}/generate")]
    [ProducesResponseType<FlightGenerationResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FlightGenerationResultDto>> Generate(int id, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken)
    {
        var result = await _generationService.GenerateAsync(id, from, to, cancellationToken);
        await _audit.RecordAsync(User.ToAuditActor(), AuditActions.FlightsGenerated, "Schedule", id.ToString(), $"{result.Created.Count} flights created", cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _scheduleService.DeleteAsync(id, cancellationToken);
        await _audit.RecordAsync(User.ToAuditActor(), AuditActions.ScheduleDeleted, "Schedule", id.ToString(), cancellationToken: cancellationToken);
        return NoContent();
    }
}
