using AirlineReservationSystem.Helpers;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Helpers.DTOs.Reservation;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Controllers.Admin;

/// <summary>
/// Admin management of the cancellation fare rules. A cancellation uses the band with the largest
/// "minDaysBeforeDeparture" that the days left still reach; without a band, or without a fee percent, no refund is due.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/admin/cancellation-rules")]
[Produces("application/json")]
public class AdminCancellationRulesController : ControllerBase
{
    private readonly IAdminCancellationRuleService _ruleService;

    public AdminCancellationRulesController(IAdminCancellationRuleService ruleService)
    {
        _ruleService = ruleService;
    }

    /// <summary>Rules per cabin, from the earliest band to the latest.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CancellationRuleDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CancellationRuleDto>>> GetAll([FromQuery] CabinClass? cabinClass, CancellationToken cancellationToken)
        => Ok(await _ruleService.GetAllAsync(cabinClass, cancellationToken));

    [HttpGet("{id:int}")]
    [ProducesResponseType<CancellationRuleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CancellationRuleDto>> Get(int id, CancellationToken cancellationToken)
        => Ok(await _ruleService.GetAsync(id, cancellationToken));

    /// <summary>One rule per cabin and starting day (409).</summary>
    [Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
    [HttpPost]
    [ProducesResponseType<CancellationRuleDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CancellationRuleDto>> Create(CancellationRuleRequestDto request, CancellationToken cancellationToken)
    {
        var rule = await _ruleService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = rule.Id }, rule);
    }

    /// <summary>Only quotes and cancellations made afterwards use the new values.</summary>
    [Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
    [HttpPut("{id:int}")]
    [ProducesResponseType<CancellationRuleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CancellationRuleDto>> Update(int id, CancellationRuleRequestDto request, CancellationToken cancellationToken)
        => Ok(await _ruleService.UpdateAsync(id, request, cancellationToken));

    [Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _ruleService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
