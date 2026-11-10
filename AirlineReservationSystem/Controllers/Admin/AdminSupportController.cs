using AirlineReservationSystem.Helpers;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Helpers.DTOs.Support;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Controllers.Admin;

/// <summary>Support staff: every request of every customer. Admin and SuperAdmin accounts only (the existing roles; no new role).</summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/admin/support/tickets")]
[Produces("application/json")]
public class AdminSupportController : ControllerBase
{
    private readonly IAdminSupportService _support;

    public AdminSupportController(IAdminSupportService support)
    {
        _support = support;
    }

    /// <summary>Search by ticket number, customer e-mail, subject or booking reference; filter by status and category; paged.</summary>
    [HttpGet]
    [ProducesResponseType<AdminSupportPageDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminSupportPageDto>> GetAll(
        [FromQuery] string? search, [FromQuery] SupportStatus? status, [FromQuery] SupportCategory? category,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        => Ok(await _support.GetAsync(search, status, category, page, pageSize, cancellationToken));

    [HttpGet("{id:int}")]
    [ProducesResponseType<AdminSupportTicketDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminSupportTicketDetailDto>> Get(int id, CancellationToken cancellationToken)
        => Ok(await _support.GetAsync(id, cancellationToken));

    /// <summary>Replies to the customer and sets the status (default: waiting for the customer). The customer is told by e-mail.</summary>
    [HttpPost("{id:int}/replies")]
    [ProducesResponseType<AdminSupportTicketDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminSupportTicketDetailDto>> Reply(int id, AdminSupportReplyRequestDto request, CancellationToken cancellationToken)
        => Ok(await _support.ReplyAsync(User.GetUserId(), id, request, cancellationToken));

    /// <summary>Changes the status. The same status again changes nothing and sends nothing.</summary>
    [HttpPut("{id:int}/status")]
    [ProducesResponseType<AdminSupportTicketDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminSupportTicketDetailDto>> SetStatus(int id, SupportStatusRequestDto request, CancellationToken cancellationToken)
        => Ok(await _support.SetStatusAsync(id, request, cancellationToken));
}
