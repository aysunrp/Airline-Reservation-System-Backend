namespace Service.Helpers.DTOs.Account;

public class VerifyEmailRequestDto
{
    /// <summary>The token from the link in the e-mail.</summary>
    public string Token { get; set; } = string.Empty;
}

/// <summary>Sends a new verification e-mail to the signed-in account.</summary>
public class ResendVerificationRequestDto
{
    /// <summary>Language of the e-mail: en, az or ru (anything else gives en).</summary>
    public string? Language { get; set; }
}

public class ForgotPasswordRequestDto
{
    public string Email { get; set; } = string.Empty;

    /// <summary>Language of the e-mail: en, az or ru (anything else gives en).</summary>
    public string? Language { get; set; }
}

public class ResetPasswordRequestDto
{
    /// <summary>The token from the link in the e-mail.</summary>
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}
