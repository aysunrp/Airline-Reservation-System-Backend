using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Service.Helpers.Settings;
using Service.Services.Interfaces;

namespace Service.Services;

/// <summary>
/// PBKDF2 (SHA-256) password hashing. Stored format: v1.{iterations}.{salt}.{hash} (Base64). The iteration count lives in the hash,
/// so raising Auth:PasswordHashIterations never breaks older hashes; <see cref="NeedsRehash"/> says when one should be upgraded.
/// </summary>
public class PasswordHasher : IPasswordHasher
{
    private const string Version = "v1";
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int MinimumIterations = 1_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    private readonly int _iterations;

    public PasswordHasher() : this(Options.Create(new AuthSettings()))
    {
    }

    public PasswordHasher(IOptions<AuthSettings> settings)
    {
        _iterations = Math.Max(MinimumIterations, settings.Value.PasswordHashIterations);
    }

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, _iterations, Algorithm, HashSize);

        return $"{Version}.{_iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string passwordHash)
    {
        var parts = passwordHash.Split('.');
        if (parts.Length != 4 || parts[0] != Version || !int.TryParse(parts[1], out var iterations))
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, expected.Length);

            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public bool NeedsRehash(string passwordHash)
    {
        var parts = passwordHash.Split('.');
        return parts.Length == 4 && parts[0] == Version && int.TryParse(parts[1], out var iterations) && iterations < _iterations;
    }
}
