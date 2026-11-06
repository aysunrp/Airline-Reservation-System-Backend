namespace Service.Helpers.DTOs.Account;

/// <summary>Starts the deletion of the signed-in account: nothing is deleted until the link e-mailed to the account is confirmed.</summary>
public class AccountDeletionRequestDto
{
    /// <summary>The current password, so a stolen sign-in cannot start the deletion.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Language of the e-mail: az (the site is Azerbaijani only; anything else gives az).</summary>
    public string? Language { get; set; }
}

/// <summary>The link was e-mailed to the masked address; it works for <see cref="Minutes"/> minutes.</summary>
public record AccountDeletionLinkDto(bool EmailSent, string MaskedEmail, int Minutes);

/// <summary>What the confirmation page shows before anything is deleted.</summary>
public record AccountDeletionCheckDto(string MaskedEmail, DateTime ExpiresAt);

public class ConfirmAccountDeletionRequestDto
{
    /// <summary>The token from the link in the e-mail.</summary>
    public string Token { get; set; } = string.Empty;

    public string? Language { get; set; }
}

public record AccountDeletedDto(bool Deleted);
