namespace Service.Helpers.DTOs.Seed;

public enum SuperAdminBootstrapOutcome
{
    /// <summary>A new SuperAdmin account was stored.</summary>
    Created,

    /// <summary>An active SuperAdmin already exists, so nothing was stored.</summary>
    AlreadyExists,

    /// <summary>An account already uses the email; it was not changed and not promoted.</summary>
    EmailTaken,

    /// <summary>The configured email or password is missing or invalid; nothing was stored.</summary>
    InvalidConfiguration
}

/// <summary>Outcome of the bootstrap. <see cref="Problems"/> name configuration keys, never their values.</summary>
public record SuperAdminBootstrapResultDto(SuperAdminBootstrapOutcome Outcome, IReadOnlyList<string> Problems);

public enum SuperAdminPromotionOutcome
{
    Promoted,
    AlreadySuperAdmin,
    InvalidEmail,
    NotFound,

    /// <summary>Only an existing Admin can be promoted; a User has to become an Admin first.</summary>
    NotAnAdmin,

    Inactive,

    /// <summary>The address was never confirmed, so nobody has shown they own the mailbox.</summary>
    EmailNotVerified
}

public record SuperAdminPromotionResultDto(SuperAdminPromotionOutcome Outcome);
