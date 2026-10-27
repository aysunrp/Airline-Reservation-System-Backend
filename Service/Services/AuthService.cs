using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Account;
using Service.Helpers.Exceptions;
using Service.Helpers.Mappings;
using Service.Helpers.Settings;
using Service.Services.Interfaces;

namespace Service.Services;

public class AuthService : IAuthService
{
    private const string InvalidCredentialsMessage = "Invalid email or password.";

    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IValidator<RegisterRequestDto> _registerValidator;
    private readonly IValidator<LoginRequestDto> _loginValidator;
    private readonly ILoginAttemptTracker _attempts;
    private readonly IOptions<AuthSettings> _settings;
    private readonly ILogger<AuthService> _logger;
    private readonly IAccountEmailService _accountEmail;

    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IValidator<RegisterRequestDto> registerValidator,
        IValidator<LoginRequestDto> loginValidator,
        ILoginAttemptTracker attempts,
        IOptions<AuthSettings> settings,
        ILogger<AuthService> logger,
        IAccountEmailService accountEmail)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _attempts = attempts;
        _settings = settings;
        _logger = logger;
        _accountEmail = accountEmail;
    }

    public async Task<RegisterResponseDto> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default)
    {
        await _registerValidator.ValidateAndThrowAsync(request, cancellationToken);

        var email = NormalizeEmail(request.Email);
        if (await _userRepository.EmailExistsAsync(email, cancellationToken))
        {
            throw new ConflictException("An account with this email already exists.");
        }

        var user = new User
        {
            Email = email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Role = UserRole.User
        };

        await _userRepository.AddAsync(user, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        // The address stays unverified until its owner opens the link; a missing SMTP configuration or a failed delivery
        // changes nothing about that (the account exists, the answer says no mail went out).
        var sent = await _accountEmail.SendVerificationAsync(user, request.Language, cancellationToken);

        return new RegisterResponseDto(user.ToUserDto(), sent);
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        await _loginValidator.ValidateAndThrowAsync(request, cancellationToken);

        var email = NormalizeEmail(request.Email);

        // Paused sign-in follows the typed address, known or not, and refuses even the right password until the pause ends.
        if (_attempts.IsLockedOut(email, out var retryAfter))
        {
            throw new TooManyAttemptsException(LockedMessage(retryAfter), retryAfter);
        }

        var user = await _userRepository.GetByEmailAsync(email, cancellationToken);

        // Same message for unknown email, wrong password and disabled account, so accounts cannot be probed.
        if (user is null || !user.IsActive || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            if (_attempts.RecordFailure(email))
            {
                // Neither the address nor the password is written to the log.
                _logger.LogWarning("Sign-in was paused for {Minutes} minutes after repeated failures.", _settings.Value.LockoutMinutes);
            }

            throw new UnauthorizedException(InvalidCredentialsMessage);
        }

        _attempts.Reset(email);

        // A hash made with fewer iterations than today's is replaced now that the password is known to be right.
        if (_passwordHasher.NeedsRehash(user.PasswordHash))
        {
            user.PasswordHash = _passwordHasher.Hash(request.Password);
            _userRepository.Update(user);
            await _userRepository.SaveChangesAsync(cancellationToken);
        }

        return CreateResponse(user);
    }

    internal static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string LockedMessage(TimeSpan retryAfter)
        => $"Too many failed sign-in attempts. Try again in {Math.Max(1, (int)Math.Ceiling(retryAfter.TotalMinutes))} minutes.";

    private AuthResponseDto CreateResponse(User user)
    {
        var (accessToken, expiresAt) = _tokenService.CreateToken(user);
        return new AuthResponseDto(accessToken, expiresAt, user.ToUserDto());
    }
}
