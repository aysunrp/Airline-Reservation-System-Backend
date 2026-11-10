using AirlineReservationSystem.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Helpers.DTOs.Reservation;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Controllers.Admin;

/// <summary>Admin dashboard: newest bookings and next departures.</summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/admin/dashboard")]
[Produces("application/json")]
public class AdminDashboardController : ControllerBase
{
    private readonly IAdminDashboardService _dashboardService;

    public AdminDashboardController(IAdminDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet]
    [ProducesResponseType<AdminDashboardDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminDashboardDto>> Get(CancellationToken cancellationToken)
        => Ok(await _dashboardService.GetAsync(cancellationToken));
}
