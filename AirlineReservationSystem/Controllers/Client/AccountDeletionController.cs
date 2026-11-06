using AirlineReservationSystem.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Service.Helpers.DTOs.Account;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Controllers.Client;

/// <summary>
/// Deleting your own account, in two steps. The first (signed in, with the password) e-mails a link and deletes nothing. The link's
/// page reads it with a POST to check (which changes nothing) and deletes only when its button sends the POST, so a mail program that opens
/// links cannot delete an account. A wrong, used or expired link gives 422 with a code (link_invalid, link_used, link_expired,
/// link_confirmed); other refusals have their own code (wrong_password, open_bookings, account_not_deletable).
/// </summary>
[ApiController]
[Route("api/account-deletion")]
[Produces("application/json")]
public class AccountDeletionController : ControllerBase
{
    private readonly IAccountDeletionService _deletion;

    public AccountDeletionController(IAccountDeletionService deletion)
    {
        _deletion = deletion;
    }

    /// <summary>E-mails the confirmation link to the account's own address (202). The account is not changed.</summary>
    [HttpPost("request")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<AccountDeletionLinkDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Request(AccountDeletionRequestDto request, CancellationToken cancellationToken)
        => StatusCode(StatusCodes.Status202Accepted, await _deletion.RequestAsync(User.GetUserId(), request, cancellationToken));

    /// <summary>Tells the confirmation page whether the link can still be used. Reads only (a POST, so the token stays out of URLs and logs).</summary>
    [HttpPost("check")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<AccountDeletionCheckDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AccountDeletionCheckDto>> Check(ConfirmAccountDeletionRequestDto request, CancellationToken cancellationToken)
        => Ok(await _deletion.CheckAsync(request.Token, cancellationToken));

    /// <summary>Deletes the account for good (no sign-in needed: the link proves the mailbox). The link works once.</summary>
    [HttpPost("confirm")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<AccountDeletedDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AccountDeletedDto>> Confirm(ConfirmAccountDeletionRequestDto request, CancellationToken cancellationToken)
        => Ok(await _deletion.ConfirmAsync(request, cancellationToken));
}
