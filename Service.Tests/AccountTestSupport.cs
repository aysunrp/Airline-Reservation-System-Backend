using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Repository.Repositories.Interfaces;
using Service.Helpers.Exceptions;
using Service.Helpers.Settings;
using Service.Helpers.Validators;
using Service.Services;
using Service.Services.Interfaces;

namespace Service.Tests;

/// <summary>A clock the test can move forward.</summary>
internal sealed class AdjustableTimeProvider : TimeProvider
{
    private DateTimeOffset _now;

    public AdjustableTimeProvider(DateTime utcNow) => _now = new DateTimeOffset(utcNow, TimeSpan.Zero);

    public void Advance(TimeSpan by) => _now += by;

    public override DateTimeOffset GetUtcNow() => _now;
}

/// <summary>Keeps every formatted log line so a test can check what was (not) written.</summary>
internal sealed class ListLogger<T> : ILogger<T>
{
    public List<string> Messages { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Messages.Add(formatter(state, exception));
        if (exception is not null)
        {
            Messages.Add(exception.ToString());
        }
    }
}

/// <summary>Stands in for SMTP: keeps what would have been sent, can be switched off or made to fail.</summary>
internal sealed class FakeEmailSender : IEmailSender
{
    public bool IsConfigured { get; set; } = true;
    public bool Fail { get; set; }
    public List<EmailMessage> Sent { get; } = new();

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured || Fail)
        {
            throw new EmailDeliveryException("The e-mail could not be sent.");
        }

        Sent.Add(message);
        return Task.CompletedTask;
    }

    /// <summary>The token inside the link of the latest message (what the person would click).</summary>
    public string LatestToken() => TokenOf(Sent[^1]);

    public static string TokenOf(EmailMessage message)
    {
        var start = message.TextBody.IndexOf("?token=", StringComparison.Ordinal) + "?token=".Length;
        var end = message.TextBody.IndexOfAny(new[] { '\n', ' ', '\r' }, start);
        return Uri.UnescapeDataString(message.TextBody[start..(end < 0 ? message.TextBody.Length : end)]);
    }
}

internal sealed class FakeEmailTokenRepository : FakeRepository<EmailToken>, IEmailTokenRepository
{
    private readonly FakeUserRepository _users;

    public FakeEmailTokenRepository(FakeUserRepository users) => _users = users;

    public IReadOnlyList<EmailToken> All => Items;

    public Task<EmailToken?> GetByHashAsync(string tokenHash, EmailTokenPurpose purpose, CancellationToken cancellationToken = default)
    {
        var token = Items.FirstOrDefault(t => t.TokenHash == tokenHash && t.Purpose == purpose);
        if (token is not null)
        {
            token.User = _users.Find(token.UserId)!;
        }

        return Task.FromResult(token);
    }

    public Task<IReadOnlyList<EmailToken>> GetUnusedAsync(int userId, EmailTokenPurpose purpose, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<EmailToken>>(Items.Where(t => t.UserId == userId && t.Purpose == purpose && t.UsedAt is null).ToList());

    public Task<DateTime?> GetLatestCreatedAtAsync(int userId, EmailTokenPurpose purpose, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Where(t => t.UserId == userId && t.Purpose == purpose).Select(t => (DateTime?)t.CreatedAt).OrderByDescending(d => d).FirstOrDefault());

    public Task<int> DeleteExpiredAsync(DateTime before, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.RemoveAll(t => t.ExpiresAt < before));
}

/// <summary>Everything the sign-in, sign-up and e-mail tests share: fakes for the database and for SMTP, a movable clock and a log.</summary>
internal sealed class AccountTestContext
{
    public static readonly DateTime Start = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

    public static readonly JwtSettings Jwt = new()
    {
        Issuer = "AEROVA",
        Audience = "AEROVA.Client",
        Secret = "test-secret-that-is-long-enough-for-hmac-sha256!!",
        ExpiryMinutes = 60
    };

    public AccountTestContext(params User[] users) : this(new FakeUserRepository(users))
    {
    }

    public AccountTestContext(FakeUserRepository users)
    {
        Users = users;
        Tokens = new FakeEmailTokenRepository(Users);
    }

    public AdjustableTimeProvider Clock { get; } = new(Start);
    public FakeUserRepository Users { get; }
    public FakeEmailTokenRepository Tokens { get; }
    public FakeEmailSender Sender { get; } = new();
    public ListLogger<AccountEmailService> EmailLog { get; } = new();
    public ListLogger<AuthService> AuthLog { get; } = new();
    public AuthSettings Settings { get; } = new() { PasswordHashIterations = 1_000, FrontendBaseUrl = "https://aerova.test" };

    public PasswordHasher Hasher => new(Options.Create(Settings));

    public LoginAttemptTracker Attempts => _attempts ??= new LoginAttemptTracker(Options.Create(Settings), Clock);
    private LoginAttemptTracker? _attempts;

    public AccountEmailService CreateAccountEmail() => new(
        Users, Tokens, Sender, Hasher, Attempts,
        new VerifyEmailRequestValidator(), new ForgotPasswordRequestValidator(), new ResetPasswordRequestValidator(),
        Options.Create(Settings), Clock, EmailLog);

    public AuthService CreateAuth(JwtSettings? jwt = null) => new(
        Users, Hasher, new TokenService(Options.Create(jwt ?? Jwt), Clock),
        new RegisterRequestValidator(), new LoginRequestValidator(),
        Attempts, Options.Create(Settings), AuthLog, CreateAccountEmail());

    public static User Customer(string password, AuthSettings? settings = null, int id = 5, string email = "aysun@example.com") => new()
    {
        Id = id,
        Email = email,
        FirstName = "Aysun",
        LastName = "Panahli",
        PasswordHash = new PasswordHasher(Options.Create(settings ?? new AuthSettings { PasswordHashIterations = 1_000 })).Hash(password)
    };
}
