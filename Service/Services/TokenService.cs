using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Service.Helpers.Settings;
using Service.Services.Interfaces;

namespace Service.Services;

public class TokenService : ITokenService
{
    // HMAC-SHA256 needs a key of at least 256 bits.
    private const int MinimumSecretBytes = 32;

    private readonly JwtSettings _settings;
    private readonly TimeProvider _timeProvider;

    public TokenService(IOptions<JwtSettings> settings, TimeProvider timeProvider)
    {
        _settings = settings.Value;
        _timeProvider = timeProvider;
    }

    public (string AccessToken, DateTime ExpiresAt) CreateToken(User user)
    {
        var key = Encoding.UTF8.GetBytes(_settings.Secret ?? string.Empty);
        if (key.Length < MinimumSecretBytes)
        {
            throw new InvalidOperationException("Jwt:Secret is missing or shorter than 32 bytes. Set it with dotnet user-secrets.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var expiresAt = now.AddMinutes(_settings.ExpiryMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.GivenName, user.FirstName),
            new Claim(JwtRegisteredClaimNames.FamilyName, user.LastName),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: now,
            expires: expiresAt,
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
