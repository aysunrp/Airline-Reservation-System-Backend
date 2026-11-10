using AirlineReservationSystem.Helpers;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Helpers.DTOs.Account;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Controllers.Admin;

/// <summary>SuperAdmin "Users" page: accounts, the Admin role and deactivation. Admins cannot reach it (403).</summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
[Route("api/admin/users")]
[Produces("application/json")]
public class AdminUsersController : ControllerBase
{
    private readonly IAdminUserService _userService;

    public AdminUsersController(IAdminUserService userService)
    {
        _userService = userService;
    }

    /// <summary>Newest first; search matches the e-mail address or the name. <c>take</c> is 1-500 (100 by default).</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AdminUserDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminUserDto>>> GetAll(
        [FromQuery] string? search, [FromQuery] UserRole? role, [FromQuery] bool? isActive, [FromQuery] int take = 100, CancellationToken cancellationToken = default)
        => Ok(await _userService.GetAllAsync(search, role, isActive, take, cancellationToken));

    [HttpGet("{id:int}")]
    [ProducesResponseType<AdminUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminUserDto>> Get(int id, CancellationToken cancellationToken)
        => Ok(await _userService.GetAsync(id, cancellationToken));

    /// <summary>Creates an Admin account (its address counts as confirmed). The address must be new (409).</summary>
    [HttpPost]
    [ProducesResponseType<AdminUserDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminUserDto>> CreateAdmin(CreateAdminRequestDto request, CancellationToken cancellationToken)
    {
        var user = await _userService.CreateAdminAsync(User.ToAuditActor(), request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = user.Id }, user);
    }

    /// <summary>Gives or takes back the Admin role (User or Admin). A SuperAdmin's role cannot be changed (422).</summary>
    [HttpPut("{id:int}/role")]
    [ProducesResponseType<AdminUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AdminUserDto>> ChangeRole(int id, ChangeRoleRequestDto request, CancellationToken cancellationToken)
        => Ok(await _userService.ChangeRoleAsync(User.ToAuditActor(), id, request.Role, cancellationToken));

    /// <summary>Deactivated accounts cannot sign in and their tokens stop working at once. Not your own account, not the last SuperAdmin (422).</summary>
    [HttpPut("{id:int}/active")]
    [ProducesResponseType<AdminUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AdminUserDto>> SetActive(int id, SetActiveRequestDto request, CancellationToken cancellationToken)
        => Ok(await _userService.SetActiveAsync(User.ToAuditActor(), id, request.IsActive, cancellationToken));

    /// <summary>Only accounts without bookings; deactivate the others instead (422).</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _userService.DeleteAsync(User.ToAuditActor(), id, cancellationToken);
        return NoContent();
    }
}
