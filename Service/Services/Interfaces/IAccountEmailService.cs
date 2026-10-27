using Domain.Entities;
using Service.Helpers.DTOs.Account;

namespace Service.Services.Interfaces;

/// <summary>E-mail verification and password reset: the links sent by e-mail and what happens when they are opened.</summary>
public interface IAccountEmailService
{
    /// <summary>True when the API can send e-mail (the SMTP settings are complete).</summary>
    bool IsEmailConfigured { get; }

    /// <summary>
    /// Sends the verification e-mail of a new account (best effort). Returns false, without an error, when e-mail is not configured,
    /// a mail was sent a moment ago or sending failed: the account then simply stays unverified.
    /// </summary>
    Task<bool> SendVerificationAsync(User user, string? language, CancellationToken cancellationToken = default);

    /// <summary>
    /// After the address of an account changed: every link that was sent to the old address stops working, and a new verification
    /// e-mail goes to the new one (best effort). The caller has already made the account unverified.
    /// </summary>
    Task EmailChangedAsync(User user, string? language, CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirms the address behind the token. A wrong, a used and an expired link give a 422 with their own code
    /// (link_invalid, link_expired, link_used, and link_confirmed when the address is already confirmed), so the page can say what happened.
    /// </summary>
    Task VerifyEmailAsync(VerifyEmailRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Sends a new verification e-mail to the signed-in account.</summary>
    Task ResendVerificationAsync(int userId, string? language, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a six-digit reset code to the address of an active account. No account gives a 404, a code asked for again within a
    /// minute (or too often in an hour) a 429, and a code that could not be mailed a 503: it is never reported as sent when it was not.
    /// </summary>
    Task ForgotPasswordAsync(ForgotPasswordRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Trades a correct, unexpired, unused code for a one-time ticket. A wrong code counts against the code's few allowed attempts;
    /// every kind of failure (no account, wrong, expired, used, out of attempts) gives the same error.
    /// </summary>
    Task<ResetTicketDto> VerifyResetCodeAsync(VerifyResetCodeRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Sets a new password for the ticket's account. The ticket works once.</summary>
    Task ResetPasswordAsync(ResetPasswordRequestDto request, CancellationToken cancellationToken = default);
}
