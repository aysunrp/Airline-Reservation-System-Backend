using AirlineReservationSystem.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Helpers.DTOs.Account;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Controllers.Admin;

/// <summary>SuperAdmin "Audit log" page: who did what to which object.</summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
[Route("api/admin/audit-logs")]
[Produces("application/json")]
public class AdminAuditController : ControllerBase
{
    private readonly IAuditLogService _audit;

    public AdminAuditController(IAuditLogService audit)
    {
        _audit = audit;
    }

    /// <summary>Newest first. <c>take</c> is 1-500 (100 by default); the filters are optional.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AuditLogDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AuditLogDto>>> GetAll(
        [FromQuery] string? action, [FromQuery] int? actorUserId, [FromQuery] string? targetType, [FromQuery] int take = 100, CancellationToken cancellationToken = default)
        => Ok(await _audit.GetAsync(action, actorUserId, targetType, take, cancellationToken));
}
