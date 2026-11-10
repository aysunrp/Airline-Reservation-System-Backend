using Service.Helpers.DTOs.Account;

namespace Service.Services.Interfaces;

public interface IAuthService
{
    /// <summary>Creates a Client account (never an Admin) and sends the e-mail that confirms the address. The person is not signed in.</summary>
    Task<RegisterResponseDto> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default);

    Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
}
