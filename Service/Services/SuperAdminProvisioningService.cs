using System.Net.Mail;
using Domain.Entities;
using Domain.Enums;
using Repository.Repositories.Interfaces;
using Service.Helpers;
using Service.Helpers.DTOs.Seed;
using Service.Services.Interfaces;

namespace Service.Services;

public class SuperAdminProvisioningService : ISuperAdminProvisioningService
{
    public const int MinPasswordLength = 12;
    private const int MaxPasswordLength = 100;
    private const int MaxEmailLength = 256;

    /// <summary>The "who" of an entry made by the server itself rather than by a signed-in person.</summary>
    private static readonly AuditActor ServerActor = new(null, "Server");

    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuditLogService _audit;
    private readonly TimeProvider _timeProvider;

    public SuperAdminProvisioningService(IUserRepository users, IPasswordHasher passwordHasher, IAuditLogService audit, TimeProvider timeProvider)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _audit = audit;
        _timeProvider = timeProvider;
    }

    public async Task<SuperAdminBootstrapResultDto> BootstrapAsync(string? email, string? password, CancellationToken cancellationToken = default)
    {
        var problems = Validate(email, password);
        if (problems.Count > 0)
        {
            return new SuperAdminBootstrapResultDto(SuperAdminBootstrapOutcome.InvalidConfiguration, problems);
        }

        // With a SuperAdmin in place the settings do nothing, so a value left behind in the configuration cannot add accounts.
        if (await _users.CountActiveInRoleAsync(UserRole.SuperAdmin, cancellationToken) > 0)
        {
            return new SuperAdminBootstrapResultDto(SuperAdminBootstrapOutcome.AlreadyExists, Array.Empty<string>());
        }

        var normalized = AuthService.NormalizeEmail(email!);
        if (await _users.EmailExistsAsync(normalized, cancellationToken))
        {
            return new SuperAdminBootstrapResultDto(SuperAdminBootstrapOutcome.EmailTaken, Array.Empty<string>());
        }

        var user = new User
        {
            Email = normalized,
            PasswordHash = _passwordHasher.Hash(password!),
            FirstName = "Aerova",
            LastName = "SuperAdmin",
            Role = UserRole.SuperAdmin,
            // Made from the server's own configuration, not through a sign-up that needs an e-mail link.
            EmailVerifiedAt = _timeProvider.GetUtcNow().UtcDateTime
        };
        await _users.AddAsync(user, cancellationToken);
        await _users.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(ServerActor, AuditActions.SuperAdminBootstrapped, "User", user.Id.ToString(), "Created from the Bootstrap settings", cancellationToken);
        return new SuperAdminBootstrapResultDto(SuperAdminBootstrapOutcome.Created, Array.Empty<string>());
    }

    public async Task<SuperAdminPromotionResultDto> PromoteAsync(string? email, CancellationToken cancellationToken = default)
    {
        if (!IsValidEmail(email))
        {
            return new SuperAdminPromotionResultDto(SuperAdminPromotionOutcome.InvalidEmail);
        }

        var user = await _users.GetByEmailAsync(AuthService.NormalizeEmail(email!), cancellationToken);
        var refusal = user switch
        {
            null => SuperAdminPromotionOutcome.NotFound,
            { Role: UserRole.SuperAdmin } => SuperAdminPromotionOutcome.AlreadySuperAdmin,
            { Role: UserRole.User } => SuperAdminPromotionOutcome.NotAnAdmin,
            { IsActive: false } => SuperAdminPromotionOutcome.Inactive,
            { EmailVerifiedAt: null } => SuperAdminPromotionOutcome.EmailNotVerified,
            _ => (SuperAdminPromotionOutcome?)null
        };

        if (refusal is { } outcome)
        {
            return new SuperAdminPromotionResultDto(outcome);
        }

        user!.Role = UserRole.SuperAdmin;
        _users.Update(user);
        await _audit.StageAsync(ServerActor, AuditActions.UserRoleChanged, "User", user.Id.ToString(), "Admin -> SuperAdmin (server command)", cancellationToken);
        await _users.SaveChangesAsync(cancellationToken);

        return new SuperAdminPromotionResultDto(SuperAdminPromotionOutcome.Promoted);
    }

    private static bool IsValidEmail(string? email)
        => !string.IsNullOrWhiteSpace(email)
           && email.Trim().Length <= MaxEmailLength
           && MailAddress.TryCreate(email.Trim(), out var address)
           && address.Address == email.Trim();

    // Messages name the setting, never its value.
    private static List<string> Validate(string? email, string? password)
    {
        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(email))
        {
            problems.Add("Bootstrap:SuperAdminEmail is not set.");
        }
        else if (!IsValidEmail(email))
        {
            problems.Add("Bootstrap:SuperAdminEmail is not a valid email address.");
        }

        if (string.IsNullOrEmpty(password))
        {
            problems.Add("Bootstrap:SuperAdminPassword is not set.");
        }
        else if (password.Length < MinPasswordLength || password.Length > MaxPasswordLength
                 || !password.Any(char.IsLetter) || !password.Any(char.IsDigit))
        {
            problems.Add($"Bootstrap:SuperAdminPassword must be {MinPasswordLength}-{MaxPasswordLength} characters long and contain letters and digits.");
        }

        return problems;
    }
}
