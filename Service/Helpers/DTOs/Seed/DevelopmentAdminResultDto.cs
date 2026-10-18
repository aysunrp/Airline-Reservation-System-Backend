namespace Service.Helpers.DTOs.Seed;

public enum DevelopmentAdminOutcome
{
    /// <summary>A new Admin account was stored.</summary>
    Created,

    /// <summary>An Admin account with the email already exists; it was left as it is.</summary>
    AlreadyAdmin,

    /// <summary>A non-admin account uses the email; it was not changed and not promoted.</summary>
    ExistingNonAdmin,

    /// <summary>The configured email or password is missing or invalid; nothing was stored.</summary>
    InvalidConfiguration
}

/// <summary>Outcome of the development admin step. <see cref="Problems"/> name configuration keys, never their values.</summary>
public record DevelopmentAdminResultDto(DevelopmentAdminOutcome Outcome, IReadOnlyList<string> Problems);
