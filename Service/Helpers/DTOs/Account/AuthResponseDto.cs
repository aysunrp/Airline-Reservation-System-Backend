namespace Service.Helpers.DTOs.Account;

public record UserDto(int Id, string Email, string FirstName, string LastName, string Role, bool EmailVerified);

public record AuthResponseDto(string AccessToken, DateTime ExpiresAt, UserDto User);

/// <summary>
/// The answer to a registration: the new account, not signed in. The person confirms the e-mail address with the link and signs in.
/// <see cref="VerificationEmailSent"/> is false when the API cannot send e-mail (no SMTP settings) or sending failed; the address then stays unverified.
/// </summary>
public record RegisterResponseDto(UserDto User, bool VerificationEmailSent);
