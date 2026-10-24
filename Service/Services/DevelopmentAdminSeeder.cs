using System.Net.Mail;
using Domain.Entities;
using Domain.Enums;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Seed;
using Service.Services.Interfaces;

namespace Service.Services;

/// <summary>
/// Creates the development Admin account with the same password hashing as registration. It only ever adds a new
/// account: an existing account with the email keeps its password, name and role.
/// </summary>
public class DevelopmentAdminSeeder : IDevelopmentAdminSeeder
{
    public const int MinPasswordLength = 8;
    private const int MaxPasswordLength = 100;
    private const int MaxEmailLength = 256;

    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    public DevelopmentAdminSeeder(IUserRepository userRepository, IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<DevelopmentAdminResultDto> EnsureAdminAsync(string? email, string? password, CancellationToken cancellationToken = default)
    {
        var problems = Validate(email, password);
        if (problems.Count > 0)
        {
            return new DevelopmentAdminResultDto(DevelopmentAdminOutcome.InvalidConfiguration, problems);
        }

        var normalized = AuthService.NormalizeEmail(email!);
        var existing = await _userRepository.GetByEmailAsync(normalized, cancellationToken);
        if (existing is not null)
        {
            return new DevelopmentAdminResultDto(
                existing.Role == UserRole.Admin ? DevelopmentAdminOutcome.AlreadyAdmin : DevelopmentAdminOutcome.ExistingNonAdmin,
                Array.Empty<string>());
        }

        await _userRepository.AddAsync(new User
        {
            Email = normalized,
            PasswordHash = _passwordHasher.Hash(password!),
            FirstName = "Aerova",
            LastName = "Admin",
            Role = UserRole.Admin,
            // Created by the developer's own configuration, not through a sign-up that needs an e-mail link.
            EmailVerifiedAt = DateTime.UtcNow
        }, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        return new DevelopmentAdminResultDto(DevelopmentAdminOutcome.Created, Array.Empty<string>());
    }

    // Messages name the setting, never its value.
    private static List<string> Validate(string? email, string? password)
    {
        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(email))
        {
            problems.Add("Seed:AdminEmail is not set.");
        }
        else if (email.Trim().Length > MaxEmailLength || !MailAddress.TryCreate(email.Trim(), out var address) || address.Address != email.Trim())
        {
            problems.Add("Seed:AdminEmail is not a valid email address.");
        }

        if (string.IsNullOrEmpty(password))
        {
            problems.Add("Seed:AdminPassword is not set.");
        }
        else if (password.Length < MinPasswordLength || password.Length > MaxPasswordLength
                 || !password.Any(char.IsLetter) || !password.Any(char.IsDigit))
        {
            problems.Add($"Seed:AdminPassword must be {MinPasswordLength}-{MaxPasswordLength} characters long and contain letters and digits.");
        }

        return problems;
    }
}
