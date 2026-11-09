using Service.Helpers.DTOs.Account;

namespace Service.Services.Interfaces;

/// <summary>
/// The Account settings of the signed-in user: password, e-mail address, phone, sessions and e-mail preferences. Every method works on the account
/// the token belongs to (the user id comes from the token, never from the request), so nobody can change another account from here.
/// </summary>
public interface IAccountSettingsService
{
    Task<AccountOverviewDto> GetOverviewAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks the current password, sets the new one (hashed like every password), ends every other session and returns a fresh token for this
    /// device. A wrong current password gives 422 wrong_password and counts towards the same pause as a sign-in.
    /// </summary>
    Task<AuthResponseDto> ChangePasswordAsync(int userId, ChangePasswordRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Checks the password and e-mails a six-digit code to the NEW address. The account's address does not change yet.</summary>
    Task<EmailChangeRequestedDto> RequestEmailChangeAsync(int userId, RequestEmailChangeDto request, CancellationToken cancellationToken = default);

    /// <summary>Switches the address when the code from the new address is right; ends the other sessions and returns a fresh token. A wrong or old code gives 422 code_invalid.</summary>
    Task<AuthResponseDto> ConfirmEmailChangeAsync(int userId, ConfirmEmailChangeDto request, CancellationToken cancellationToken = default);

    Task<ProfileDto> UpdatePhoneAsync(int userId, UpdatePhoneRequestDto request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SessionDto>> GetSessionsAsync(int userId, string? currentSessionKey, CancellationToken cancellationToken = default);

    /// <summary>Ends one session of the account. The session of this device is refused (422 current_session): use the sign-out. Another account's session is "not found".</summary>
    Task RevokeSessionAsync(int userId, int sessionId, string? currentSessionKey, CancellationToken cancellationToken = default);

    /// <summary>Ends the session of this device (the real sign-out). Quiet when there is no session (an older token).</summary>
    Task SignOutAsync(int userId, string? currentSessionKey, CancellationToken cancellationToken = default);

    /// <summary>Ends every session of the account, this one included, and returns a fresh token for this device.</summary>
    Task<AuthResponseDto> RevokeOtherSessionsAsync(int userId, CancellationToken cancellationToken = default);

    Task<NotificationPreferencesDto> GetPreferencesAsync(int userId, CancellationToken cancellationToken = default);

    Task<NotificationPreferencesDto> UpdatePreferencesAsync(int userId, UpdateNotificationPreferencesDto request, CancellationToken cancellationToken = default);
}
