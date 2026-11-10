using AirlineReservationSystem.Helpers;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Helpers.DTOs.Passenger;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Controllers.Admin;

/// <summary>Admin "Passengers" page: travellers grouped by travel document.</summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/admin/passengers")]
[Produces("application/json")]
public class AdminPassengersController : ControllerBase
{
    private readonly IAdminPassengerService _passengerService;

    public AdminPassengersController(IAdminPassengerService passengerService)
    {
        _passengerService = passengerService;
    }

    /// <summary>Filtered by name, email or document number, and by active/inactive.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AdminPassengerDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminPassengerDto>>> GetAll([FromQuery] string? search, [FromQuery] bool? isActive, CancellationToken cancellationToken)
        => Ok(await _passengerService.GetAllAsync(search, isActive, cancellationToken));

    /// <summary>"View Details": the traveller and their bookings.</summary>
    [HttpGet("{documentType}/{documentNumber}")]
    [ProducesResponseType<AdminPassengerDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminPassengerDetailsDto>> Get(DocumentType documentType, string documentNumber, CancellationToken cancellationToken)
        => Ok(await _passengerService.GetDetailsAsync(documentType, documentNumber, cancellationToken));
}
