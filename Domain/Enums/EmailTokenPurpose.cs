namespace Domain.Enums;

public enum EmailTokenPurpose
{
    VerifyEmail,
    ResetPassword,

    /// <summary>The six-digit code of a password reset: the person types it in, and a correct one is traded for a <see cref="ResetPassword"/> ticket.</summary>
    ResetCode
}
