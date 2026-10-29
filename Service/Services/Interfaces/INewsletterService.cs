using Service.Helpers.DTOs.Newsletter;

namespace Service.Services.Interfaces;

public interface INewsletterService
{
    /// <summary>Asks the owner of the address to confirm. Same answer for every state of the address; throws when the e-mail cannot be sent.</summary>
    Task<NewsletterSubscribeResponseDto> SubscribeAsync(NewsletterSubscribeRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Activates the subscription of the confirmation link. A used, unknown or expired link is refused.</summary>
    Task<NewsletterResultDto> ConfirmAsync(NewsletterTokenRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Opts the owner of the unsubscribe link out; opening it again changes nothing.</summary>
    Task<NewsletterResultDto> UnsubscribeAsync(NewsletterTokenRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Everyone a marketing newsletter may go to: confirmed, active subscribers only, each with an unsubscribe link. A newsletter sender
    /// must take its recipients from here and nowhere else.
    /// </summary>
    Task<IReadOnlyList<NewsletterRecipient>> GetRecipientsAsync(CancellationToken cancellationToken = default);
}
