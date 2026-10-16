using Domain.Entities;

namespace Service.Services.Interfaces;

public interface ITokenService
{
    /// <summary>Creates a signed JWT for the user and returns it with its UTC expiry time.</summary>
    (string AccessToken, DateTime ExpiresAt) CreateToken(User user);
}
