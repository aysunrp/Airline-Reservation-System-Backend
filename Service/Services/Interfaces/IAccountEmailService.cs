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

    /// <summary>Confirms the address behind the token. A wrong, used or expired token gives the same error.</summary>
    Task VerifyEmailAsync(VerifyEmailRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Sends a new verification e-mail to the signed-in account.</summary>
    Task ResendVerificationAsync(int userId, string? language, CancellationToken cancellationToken = default);

    /// <summary>Sends the reset link when the account exists; says nothing either way, so addresses cannot be probed.</summary>
    Task ForgotPasswordAsync(ForgotPasswordRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Sets a new password for the token's account. The token works once.</summary>
    Task ResetPasswordAsync(ResetPasswordRequestDto request, CancellationToken cancellationToken = default);
}
