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

    /// <summary>The claim that holds the key of the session a token belongs to.</summary>
    public const string SessionClaim = "sid";

    private readonly JwtSettings _settings;
    private readonly TimeProvider _timeProvider;

    public TokenService(IOptions<JwtSettings> settings, TimeProvider timeProvider)
    {
        _settings = settings.Value;
        _timeProvider = timeProvider;
    }

    public (string AccessToken, DateTime ExpiresAt) CreateToken(User user, string? sessionKey = null)
    {
        var key = Encoding.UTF8.GetBytes(_settings.Secret ?? string.Empty);
        if (key.Length < MinimumSecretBytes)
        {
            throw new InvalidOperationException("Jwt:Secret is missing or shorter than 32 bytes. Set it with dotnet user-secrets.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var expiresAt = now.AddMinutes(_settings.ExpiryMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.GivenName, user.FirstName),
            new(JwtRegisteredClaimNames.FamilyName, user.LastName),
            new(ClaimTypes.Role, user.Role.ToString())
        };
        if (!string.IsNullOrEmpty(sessionKey))
        {
            claims.Add(new Claim(SessionClaim, sessionKey));
        }

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
