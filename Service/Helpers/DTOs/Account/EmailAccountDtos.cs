namespace Service.Helpers.DTOs.Account;

public class VerifyEmailRequestDto
{
    /// <summary>The token from the link in the e-mail.</summary>
    public string Token { get; set; } = string.Empty;
}

/// <summary>Sends a new verification e-mail to the signed-in account.</summary>
public class ResendVerificationRequestDto
{
    /// <summary>Language of the e-mail: az (the site is Azerbaijani only; anything else gives az).</summary>
    public string? Language { get; set; }
}

public class ForgotPasswordRequestDto
{
    public string Email { get; set; } = string.Empty;

    /// <summary>Language of the e-mail: az (the site is Azerbaijani only; anything else gives az).</summary>
    public string? Language { get; set; }
}

/// <summary>The e-mail address and the six-digit code that was sent to it.</summary>
public class VerifyResetCodeRequestDto
{
    public string Email { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;
}

/// <summary>What a correct code is traded for: a one-time ticket that lets the new password be set, for a few minutes.</summary>
public record ResetTicketDto(string Ticket, int ExpiresInMinutes);

public class ResetPasswordRequestDto
{
    /// <summary>The ticket given for a correct reset code (or the token of a reset link sent before codes were used).</summary>
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}
