using AirlineReservationSystem.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Service.Helpers.DTOs.Account;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Controllers.Client;

/// <summary>Register, sign-in, e-mail verification and password reset pages.</summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IAccountEmailService _accountEmailService;

    public AuthController(IAuthService authService, IAccountEmailService accountEmailService)
    {
        _authService = authService;
        _accountEmailService = accountEmailService;
    }

    /// <summary>
    /// Creates a client account and e-mails the link that confirms the address. The person is not signed in: they sign in on the
    /// login page. <c>verificationEmailSent</c> is false when e-mail is not set up or could not be delivered.
    /// </summary>
    [HttpPost("register")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<RegisterResponseDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<RegisterResponseDto>> Register(RegisterRequestDto request, CancellationToken cancellationToken)
        => StatusCode(StatusCodes.Status201Created, await _authService.RegisterAsync(request, cancellationToken));

    /// <summary>Returns an access token for valid credentials.</summary>
    [HttpPost("login")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<AuthResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthResponseDto>> Login(LoginRequestDto request, CancellationToken cancellationToken)
        => Ok(await _authService.LoginAsync(request, cancellationToken));

    /// <summary>Confirms the e-mail address behind the token of the link. The token works once; a wrong, used or expired one gives 422.</summary>
    [HttpPost("verify-email")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequestDto request, CancellationToken cancellationToken)
    {
        await _accountEmailService.VerifyEmailAsync(request, cancellationToken);
        return NoContent();
    }

    /// <summary>Sends a new verification e-mail to the signed-in account (one per minute).</summary>
    [HttpPost("resend-verification")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ResendVerification(ResendVerificationRequestDto request, CancellationToken cancellationToken)
    {
        await _accountEmailService.ResendVerificationAsync(User.GetUserId(), request.Language, cancellationToken);
        return Accepted();
    }

    /// <summary>
    /// Sends a six-digit password reset code when an account has this address. The answer is 202 whether or not it does, so
    /// addresses cannot be probed; 503 only when the API cannot send e-mail at all.
    /// </summary>
    [HttpPost("forgot-password")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequestDto request, CancellationToken cancellationToken)
    {
        await _accountEmailService.ForgotPasswordAsync(request, cancellationToken);
        return Accepted();
    }

    /// <summary>
    /// Trades the six-digit reset code for a one-time ticket that sets the new password. A wrong, used, expired or exhausted code (and an
    /// unknown address) all give the same 422 with code <c>code_invalid</c>; a code dies after a few wrong guesses.
    /// </summary>
    [HttpPost("verify-reset-code")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<ResetTicketDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ResetTicketDto>> VerifyResetCode(VerifyResetCodeRequestDto request, CancellationToken cancellationToken)
        => Ok(await _accountEmailService.VerifyResetCodeAsync(request, cancellationToken));

    /// <summary>Sets a new password with the ticket given for a correct reset code. The ticket works once; a wrong, used or expired one gives 422.</summary>
    [HttpPost("reset-password")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequestDto request, CancellationToken cancellationToken)
    {
        await _accountEmailService.ResetPasswordAsync(request, cancellationToken);
        return NoContent();
    }
}
