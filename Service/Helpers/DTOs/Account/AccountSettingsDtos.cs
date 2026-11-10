using Domain.Enums;

namespace Service.Helpers.DTOs.Account;

/// <summary>The role, the active flag and the sign-out cut-off of an account: what a token is checked against on every request.</summary>
public record UserStateDto(UserRole Role, bool IsActive, DateTime? SessionsValidFrom);

/// <summary>The security and contact side of the signed-in account, for the Account settings page. Nothing here can be used to read another account.</summary>
public record AccountOverviewDto(
    string Email,
    bool EmailVerified,
    string? Phone,
    string Role,
    DateTime MemberSince,
    DateTime? PasswordChangedAt,
    DateTime? LastSignInAt,
    int OpenSessions,
    bool CanUseAdminPanel,
    bool TwoFactorAvailable);

/// <summary>Changing the password: the current one is checked on the server; the new one is hashed like every other password.</summary>
public class ChangePasswordRequestDto
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>Asks to switch the e-mail address. The current password is required; nothing changes until the code sent to the NEW address is typed in.</summary>
public class RequestEmailChangeDto
{
    public string NewEmail { get; set; } = string.Empty;
    public string CurrentPassword { get; set; } = string.Empty;

    /// <summary>Language of the e-mail: az (the site is Azerbaijani only; anything else gives az).</summary>
    public string? Language { get; set; }
}

/// <summary>The code was sent to <see cref="MaskedNewEmail"/> and works for <see cref="ExpiresInMinutes"/> minutes.</summary>
public record EmailChangeRequestedDto(string MaskedNewEmail, int ExpiresInMinutes);

public class ConfirmEmailChangeDto
{
    public string Code { get; set; } = string.Empty;
}

public class UpdatePhoneRequestDto
{
    /// <summary>The new number, or empty to remove it.</summary>
    public string? Phone { get; set; }
}

/// <summary>One sign-in of the account: where it was made (a short device description and a masked address) and when it ends.</summary>
public record SessionDto(int Id, string? Device, string? MaskedIp, DateTime CreatedAt, DateTime ExpiresAt, bool IsCurrent);

/// <summary>
/// What the account is e-mailed about. Booking, payment and security messages are always sent and cannot be turned off (they are listed so the
/// page can show them as fixed); the other three can be switched. The in-app notifications are not affected.
/// </summary>
public record NotificationPreferencesDto(
    bool BookingEmails,
    bool PaymentEmails,
    bool SecurityEmails,
    bool FlightUpdates,
    bool SystemMessages,
    bool Marketing);

public class UpdateNotificationPreferencesDto
{
    public bool FlightUpdates { get; set; } = true;
    public bool SystemMessages { get; set; } = true;
    public bool Marketing { get; set; } = true;
}
