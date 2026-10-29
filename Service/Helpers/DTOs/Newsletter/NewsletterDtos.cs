namespace Service.Helpers.DTOs.Newsletter;

public class NewsletterSubscribeRequestDto
{
    public string Email { get; set; } = string.Empty;

    /// <summary>"az", "en" or "ru": the language of the page, used for the e-mails.</summary>
    public string? Language { get; set; }
}

/// <summary>The token of a confirmation or unsubscribe link, as it came from the e-mail.</summary>
public class NewsletterTokenRequestDto
{
    public string Token { get; set; } = string.Empty;
}

/// <summary>
/// The same answer whether the address is new, waiting, already confirmed or was unsubscribed, so the form cannot be used to find out
/// who is on the list.
/// </summary>
public record NewsletterSubscribeResponseDto(string Status)
{
    public const string ConfirmationSent = "confirmation_sent";
}

public record NewsletterResultDto(string Status);

/// <summary>One person a newsletter may be sent to, with the link that opts them out (every newsletter must carry it).</summary>
public record NewsletterRecipient(string Email, string Language, string UnsubscribeLink);
