using AirlineReservationSystem.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Helpers.DTOs.Account;
using Service.Helpers.DTOs.Passenger;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Controllers.Client;

/// <summary>Profile page of the signed-in user: personal details and saved passengers.</summary>
[ApiController]
[Authorize]
[Route("api/profile")]
[Produces("application/json")]
public class ProfileController : ControllerBase
{
    private readonly IProfileService _profileService;

    public ProfileController(IProfileService profileService)
    {
        _profileService = profileService;
    }

    [HttpGet]
    [ProducesResponseType<ProfileDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ProfileDto>> Get(CancellationToken cancellationToken)
        => Ok(await _profileService.GetProfileAsync(User.GetUserId(), cancellationToken));

    [HttpPut]
    [ProducesResponseType<ProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProfileDto>> Update(UpdateProfileRequestDto request, CancellationToken cancellationToken)
        => Ok(await _profileService.UpdateProfileAsync(User.GetUserId(), request, cancellationToken));

    [HttpGet("saved-passengers")]
    [ProducesResponseType<IReadOnlyList<SavedPassengerDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SavedPassengerDto>>> GetSavedPassengers(CancellationToken cancellationToken)
        => Ok(await _profileService.GetSavedPassengersAsync(User.GetUserId(), cancellationToken));

    [HttpPost("saved-passengers")]
    [ProducesResponseType<SavedPassengerDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SavedPassengerDto>> AddSavedPassenger(SavedPassengerRequestDto request, CancellationToken cancellationToken)
        => StatusCode(StatusCodes.Status201Created, await _profileService.AddSavedPassengerAsync(User.GetUserId(), request, cancellationToken));

    [HttpPut("saved-passengers/{id:int}")]
    [ProducesResponseType<SavedPassengerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SavedPassengerDto>> UpdateSavedPassenger(int id, SavedPassengerRequestDto request, CancellationToken cancellationToken)
        => Ok(await _profileService.UpdateSavedPassengerAsync(User.GetUserId(), id, request, cancellationToken));

    [HttpDelete("saved-passengers/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveSavedPassenger(int id, CancellationToken cancellationToken)
    {
        await _profileService.RemoveSavedPassengerAsync(User.GetUserId(), id, cancellationToken);
        return NoContent();
    }
}
