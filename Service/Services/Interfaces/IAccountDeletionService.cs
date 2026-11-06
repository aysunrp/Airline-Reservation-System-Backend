using Service.Helpers.DTOs.Account;

namespace Service.Services.Interfaces;

public interface IAccountDeletionService
{
    /// <summary>Checks the password and the account, then e-mails the confirmation link. Deletes nothing.</summary>
    Task<AccountDeletionLinkDto> RequestAsync(int userId, AccountDeletionRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Reads the link without using it (the page asks before it shows the confirm button). Changes nothing.</summary>
    Task<AccountDeletionCheckDto> CheckAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>Uses the link once: erases the personal data, closes the account and e-mails a notice to the old address.</summary>
    Task<AccountDeletedDto> ConfirmAsync(ConfirmAccountDeletionRequestDto request, CancellationToken cancellationToken = default);
}
