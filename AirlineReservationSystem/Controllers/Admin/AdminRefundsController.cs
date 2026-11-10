using AirlineReservationSystem.Helpers;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Helpers.DTOs.Reservation;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Controllers.Admin;

/// <summary>Admin "Refunds" page: every refund with its status, and sending a failed one again.</summary>
[ApiController]
[Route("api/admin/refunds")]
[Produces("application/json")]
public class AdminRefundsController : ControllerBase
{
    private readonly IRefundService _refunds;

    public AdminRefundsController(IRefundService refunds)
    {
        _refunds = refunds;
    }

    /// <summary>Newest first. <c>take</c> is 1-500 (200 by default); <c>status</c> is optional.</summary>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [ProducesResponseType<IReadOnlyList<AdminRefundDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminRefundDto>>> GetAll(
        [FromQuery] RefundStatus? status, [FromQuery] int take = 200, CancellationToken cancellationToken = default)
        => Ok(await _refunds.GetAllAsync(status, take, cancellationToken));

    /// <summary>
    /// Asks Stripe again for a refund that failed or was cancelled. SuperAdmin only, because it moves money. The amount is never
    /// taken from the request: it stays what the cancellation worked out.
    /// </summary>
    [HttpPost("{id:int}/retry")]
    [Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
    [ProducesResponseType<AdminRefundDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AdminRefundDto>> Retry(int id, CancellationToken cancellationToken)
        => Ok(await _refunds.RetryAsync(id, User.ToAuditActor(), cancellationToken));
}
