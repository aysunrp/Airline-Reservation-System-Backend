using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using Repository.Repositories.Interfaces;
using Service.Helpers;
using Service.Helpers.DTOs.Account;
using Service.Helpers.Exceptions;
using Service.Services.Interfaces;

namespace Service.Services;

public class AdminUserService : IAdminUserService
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuditLogService _audit;
    private readonly IUserStateCache _stateCache;
    private readonly IValidator<CreateAdminRequestDto> _createValidator;
    private readonly TimeProvider _timeProvider;

    public AdminUserService(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        IAuditLogService audit,
        IUserStateCache stateCache,
        IValidator<CreateAdminRequestDto> createValidator,
        TimeProvider timeProvider)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _audit = audit;
        _stateCache = stateCache;
        _createValidator = createValidator;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<AdminUserDto>> GetAllAsync(string? search, UserRole? role, bool? isActive, int take, CancellationToken cancellationToken = default)
        => (await _users.SearchAsync(search, role, isActive, Math.Clamp(take, 1, 500), cancellationToken)).Select(ToDto).ToList();

    public async Task<AdminUserDto> GetAsync(int id, CancellationToken cancellationToken = default)
        => ToDto(await FindAsync(id, cancellationToken));

    public async Task<AdminUserDto> CreateAdminAsync(AuditActor actor, CreateAdminRequestDto request, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var email = AuthService.NormalizeEmail(request.Email);
        if (await _users.EmailExistsAsync(email, cancellationToken))
        {
            throw new ConflictException("An account with this email already exists.");
        }

        var user = new User
        {
            Email = email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Role = UserRole.Admin,
            // A SuperAdmin vouches for the address, so no confirmation link is needed.
            EmailVerifiedAt = _timeProvider.GetUtcNow().UtcDateTime
        };
        await _users.AddAsync(user, cancellationToken);
        await _users.SaveChangesAsync(cancellationToken);

        // The id exists only now, so this entry is saved right after the account (its own failure is logged, not thrown).
        await _audit.RecordAsync(actor, AuditActions.UserCreated, "User", user.Id.ToString(), "Role: Admin", cancellationToken);
        return ToDto(user);
    }

    public async Task<AdminUserDto> ChangeRoleAsync(AuditActor actor, int id, UserRole role, CancellationToken cancellationToken = default)
    {
        if (role is not (UserRole.User or UserRole.Admin))
        {
            throw new BusinessRuleException("Only the User and Admin roles can be given here. A SuperAdmin is created on the server.");
        }

        var user = await FindAsync(id, cancellationToken);
        if (user.Role == UserRole.SuperAdmin)
        {
            throw new BusinessRuleException("The role of a SuperAdmin cannot be changed here.");
        }

        if (user.Role == role)
        {
            return ToDto(user);
        }

        var before = user.Role;
        user.Role = role;
        _users.Update(user);
        await _audit.StageAsync(actor, AuditActions.UserRoleChanged, "User", user.Id.ToString(), $"{before} -> {role}", cancellationToken);
        await _users.SaveChangesAsync(cancellationToken);

        _stateCache.Invalidate(user.Id);
        return ToDto(user);
    }

    public async Task<AdminUserDto> SetActiveAsync(AuditActor actor, int id, bool isActive, CancellationToken cancellationToken = default)
    {
        var user = await FindAsync(id, cancellationToken);
        if (user.IsActive == isActive)
        {
            return ToDto(user);
        }

        if (!isActive)
        {
            EnsureNotYourself(actor, user, "deactivate");
            await EnsureNotLastActiveSuperAdminAsync(user, cancellationToken);
        }

        user.IsActive = isActive;
        _users.Update(user);
        await _audit.StageAsync(actor, isActive ? AuditActions.UserActivated : AuditActions.UserDeactivated, "User", user.Id.ToString(), $"Role: {user.Role}", cancellationToken);
        await _users.SaveChangesAsync(cancellationToken);

        _stateCache.Invalidate(user.Id);
        return ToDto(user);
    }

    public async Task DeleteAsync(AuditActor actor, int id, CancellationToken cancellationToken = default)
    {
        var user = await FindAsync(id, cancellationToken);

        EnsureNotYourself(actor, user, "delete");
        await EnsureNotLastActiveSuperAdminAsync(user, cancellationToken);

        if (await _users.HasBookingsAsync(user.Id, cancellationToken))
        {
            throw new BusinessRuleException("This account has bookings and cannot be deleted. Deactivate it instead.");
        }

        _users.Delete(user);
        await _audit.StageAsync(actor, AuditActions.UserDeleted, "User", user.Id.ToString(), $"Role: {user.Role}", cancellationToken);
        await _users.SaveChangesAsync(cancellationToken);

        _stateCache.Invalidate(user.Id);
    }

    private async Task<User> FindAsync(int id, CancellationToken cancellationToken)
        => await _users.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("User not found.");

    private static void EnsureNotYourself(AuditActor actor, User user, string what)
    {
        if (actor.UserId == user.Id)
        {
            throw new BusinessRuleException($"You cannot {what} your own account.");
        }
    }

    /// <summary>Taking the last active SuperAdmin away would leave nobody who can manage accounts.</summary>
    private async Task EnsureNotLastActiveSuperAdminAsync(User user, CancellationToken cancellationToken)
    {
        if (user.Role == UserRole.SuperAdmin && user.IsActive && await _users.CountActiveInRoleAsync(UserRole.SuperAdmin, cancellationToken) <= 1)
        {
            throw new BusinessRuleException("The last active SuperAdmin cannot be deactivated or deleted.");
        }
    }

    private static AdminUserDto ToDto(User user)
        => new(user.Id, user.Email, user.FirstName, user.LastName, user.Role.ToString(), user.IsActive, user.EmailVerifiedAt is not null, user.CreatedAt);
}
