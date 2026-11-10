using AirlineReservationSystem.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Service.Helpers.DTOs.Account;
using Service.Services;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Controllers.Client;

/// <summary>
/// Account settings of the signed-in user (User, Admin and SuperAdmin alike): password, e-mail address, phone, sessions and e-mail preferences.
/// The account is always the one in the token; there is no id in any request, so another account cannot be reached from here.
/// Refusals carry a short code: wrong_password (422), code_invalid (422), same_email (422), current_session (422).
/// </summary>
[ApiController]
[Authorize]
[Route("api/account")]
[Produces("application/json")]
public class AccountSettingsController : ControllerBase
{
    private readonly IAccountSettingsService _settings;

    public AccountSettingsController(IAccountSettingsService settings)
    {
        _settings = settings;
    }

    private string? SessionKey => User.FindFirst(TokenService.SessionClaim)?.Value;

    /// <summary>The security and contact overview of the account (sign-ins, last password change, role).</summary>
    [HttpGet("overview")]
    [ProducesResponseType<AccountOverviewDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AccountOverviewDto>> GetOverview(CancellationToken cancellationToken)
        => Ok(await _settings.GetOverviewAsync(User.GetUserId(), cancellationToken));

    /// <summary>Changes the password. The current one is checked here; every other device is signed out; the answer is a fresh token for this device.</summary>
    [HttpPut("password")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<AuthResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthResponseDto>> ChangePassword(ChangePasswordRequestDto request, CancellationToken cancellationToken)
        => Ok(await _settings.ChangePasswordAsync(User.GetUserId(), request, cancellationToken));

    /// <summary>Starts an e-mail change: a six-digit code goes to the NEW address (202). The address of the account does not change yet.</summary>
    [HttpPost("email-change/request")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<EmailChangeRequestedDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> RequestEmailChange(RequestEmailChangeDto request, CancellationToken cancellationToken)
        => StatusCode(StatusCodes.Status202Accepted, await _settings.RequestEmailChangeAsync(User.GetUserId(), request, cancellationToken));

    /// <summary>Switches the address when the code from the new address is right; every other device is signed out; the answer is a fresh token.</summary>
    [HttpPost("email-change/confirm")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<AuthResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AuthResponseDto>> ConfirmEmailChange(ConfirmEmailChangeDto request, CancellationToken cancellationToken)
        => Ok(await _settings.ConfirmEmailChangeAsync(User.GetUserId(), request, cancellationToken));

    [HttpPut("phone")]
    [ProducesResponseType<ProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProfileDto>> UpdatePhone(UpdatePhoneRequestDto request, CancellationToken cancellationToken)
        => Ok(await _settings.UpdatePhoneAsync(User.GetUserId(), request, cancellationToken));

    /// <summary>The open sign-ins of the account, this device marked.</summary>
    [HttpGet("sessions")]
    [ProducesResponseType<IReadOnlyList<SessionDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SessionDto>>> GetSessions(CancellationToken cancellationToken)
        => Ok(await _settings.GetSessionsAsync(User.GetUserId(), SessionKey, cancellationToken));

    /// <summary>Signs out of one other device. This device's own session is refused (422 current_session).</summary>
    [HttpDelete("sessions/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RevokeSession(int id, CancellationToken cancellationToken)
    {
        await _settings.RevokeSessionAsync(User.GetUserId(), id, SessionKey, cancellationToken);
        return NoContent();
    }

    /// <summary>The real sign-out of this device: its session is ended on the server. Quiet when the token has no session.</summary>
    [HttpDelete("sessions/current")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SignOut(CancellationToken cancellationToken)
    {
        await _settings.SignOutAsync(User.GetUserId(), SessionKey, cancellationToken);
        return NoContent();
    }

    /// <summary>Ends every session of the account and returns a fresh token for this device, so only this device stays signed in.</summary>
    [HttpPost("sessions/revoke-others")]
    [ProducesResponseType<AuthResponseDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthResponseDto>> RevokeOthers(CancellationToken cancellationToken)
        => Ok(await _settings.RevokeOtherSessionsAsync(User.GetUserId(), cancellationToken));

    [HttpGet("notification-preferences")]
    [ProducesResponseType<NotificationPreferencesDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationPreferencesDto>> GetPreferences(CancellationToken cancellationToken)
        => Ok(await _settings.GetPreferencesAsync(User.GetUserId(), cancellationToken));

    /// <summary>Saves which optional e-mails the account wants. Booking, payment and security e-mails cannot be turned off.</summary>
    [HttpPut("notification-preferences")]
    [ProducesResponseType<NotificationPreferencesDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationPreferencesDto>> UpdatePreferences(UpdateNotificationPreferencesDto request, CancellationToken cancellationToken)
        => Ok(await _settings.UpdatePreferencesAsync(User.GetUserId(), request, cancellationToken));
}
