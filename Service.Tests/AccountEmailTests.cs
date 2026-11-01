using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Service.Helpers.DTOs.Account;
using Service.Helpers.Exceptions;
using Service.Helpers.Settings;
using Service.Services;
using Service.Services.Interfaces;

namespace Service.Tests;

/// <summary>
/// E-mail verification and password reset with a fake SMTP sender (nothing is ever sent for real): the link, its token, its expiry,
/// single use, the answers that tell nobody which addresses exist, and a log without tokens, addresses or secrets.
/// </summary>
public class AccountEmailTests
{
    private static RegisterRequestDto Registration(string email = " Aysun@Example.com ", string? language = null) => new()
    {
        FirstName = "Aysun", LastName = "Panahli", Email = email, Password = "secret1", ConfirmPassword = "secret1", Language = language
    };

    private static VerifyEmailRequestDto Verify(string token) => new() { Token = token };

    private static ResetPasswordRequestDto Reset(string token, string password = "new-secret2") => new() { Token = token, NewPassword = password, ConfirmPassword = password };

    private static async Task<(AccountTestContext Context, User User)> RegisteredAsync(string? language = null)
    {
        var context = new AccountTestContext();
        await context.CreateAuth().RegisterAsync(Registration(language: language));
        return (context, context.Users.Find(1)!);
    }

    // ---- registration and verification ------------------------------------------------------------------------------

    [Fact]
    public async Task Register_EmailsAVerificationLink_ToTheSite_AndKeepsOnlyAHashOfTheToken()
    {
        var context = new AccountTestContext();

        var result = await context.CreateAuth().RegisterAsync(Registration());

        Assert.True(result.VerificationEmailSent);
        var message = Assert.Single(context.Sender.Sent);
        Assert.Equal("aysun@example.com", message.To);
        Assert.Contains("https://aerova.test/verify-email.html?token=", message.TextBody);
        Assert.Contains("https://aerova.test/verify-email.html?token=", message.HtmlBody);

        var token = context.Sender.LatestToken();
        Assert.True(token.Length >= 40);
        var stored = Assert.Single(context.Tokens.All);
        Assert.Equal(EmailTokenPurpose.VerifyEmail, stored.Purpose);
        Assert.Equal(64, stored.TokenHash.Length);
        Assert.DoesNotContain(token, stored.TokenHash);
        Assert.Equal(AccountTestContext.Start.AddHours(24), stored.ExpiresAt);
        Assert.Null(context.Users.Find(1)!.EmailVerifiedAt);
    }

    [Theory]
    [InlineData("az", "təsdiqləyin")]
    [InlineData("ru", "Подтвердите")]
    [InlineData("en", "Confirm your AEROVA")]
    [InlineData("xx", "AEROVA e-poçt ünvanınızı")]
    [InlineData(null, "AEROVA e-poçt ünvanınızı")]
    public async Task TheVerificationEmail_IsInTheChosenLanguage(string? language, string expectedInSubject)
    {
        var (context, _) = await RegisteredAsync(language);

        Assert.Contains(expectedInSubject, context.Sender.Sent.Single().Subject);
    }

    [Fact]
    public async Task WithoutSmtpSettings_RegistrationStillWorks_ButTheAddressStaysUnverified()
    {
        var context = new AccountTestContext();
        context.Sender.IsConfigured = false;

        var result = await context.CreateAuth().RegisterAsync(Registration());

        Assert.False(result.VerificationEmailSent);
        Assert.False(result.User.EmailVerified);
        Assert.Empty(context.Sender.Sent);
        Assert.Empty(context.Tokens.All);
        Assert.Null(context.Users.Find(1)!.EmailVerifiedAt);
    }

    [Fact]
    public async Task AFailedDelivery_DoesNotFailRegistration_NorVerifyTheAddress()
    {
        var context = new AccountTestContext();
        context.Sender.Fail = true;

        var result = await context.CreateAuth().RegisterAsync(Registration());

        Assert.False(result.VerificationEmailSent);
        Assert.Null(context.Users.Find(1)!.EmailVerifiedAt);
    }

    [Fact]
    public async Task OpeningTheLink_VerifiesTheAddress_Once()
    {
        var (context, user) = await RegisteredAsync();
        var token = context.Sender.LatestToken();
        var service = context.CreateAccountEmail();

        await service.VerifyEmailAsync(Verify(token));

        Assert.Equal(AccountTestContext.Start, user.EmailVerifiedAt);
        Assert.NotNull(context.Tokens.All.Single().UsedAt);
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.VerifyEmailAsync(Verify(token)));
    }

    [Fact]
    public async Task Resend_IsLimitedToOnePerMinute_AndTheFirstLinkStillConfirmsTheAddress()
    {
        var (context, user) = await RegisteredAsync();
        var firstToken = context.Sender.LatestToken();
        var service = context.CreateAccountEmail();

        var wait = await Assert.ThrowsAsync<TooManyAttemptsException>(() => service.ResendVerificationAsync(1, null));
        Assert.True(wait.RetryAfter > TimeSpan.Zero && wait.RetryAfter <= TimeSpan.FromSeconds(60));

        context.Clock.Advance(TimeSpan.FromSeconds(61));
        await service.ResendVerificationAsync(1, null);

        Assert.Equal(2, context.Sender.Sent.Count);
        var secondToken = context.Sender.LatestToken();

        // Mail can arrive in any order, so the message sent first confirms the address as well as the newer one does.
        await service.VerifyEmailAsync(Verify(firstToken));
        Assert.NotNull(user.EmailVerifiedAt);

        // Confirmed once: the other link is spent, and says that the address is already confirmed.
        var spent = await Assert.ThrowsAsync<BusinessRuleException>(() => service.VerifyEmailAsync(Verify(secondToken)));
        Assert.Equal("link_confirmed", spent.Code);
        await Assert.ThrowsAsync<ConflictException>(() => service.ResendVerificationAsync(1, null));
    }

    [Fact]
    public async Task ChangingTheEmailAddress_StillRevokesEveryOpenConfirmationLink()
    {
        var (context, user) = await RegisteredAsync();
        var firstToken = context.Sender.LatestToken();
        var service = context.CreateAccountEmail();
        context.Clock.Advance(TimeSpan.FromSeconds(61));
        await service.ResendVerificationAsync(1, null);
        var secondToken = context.Sender.LatestToken();

        await service.EmailChangedAsync(user, null);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.VerifyEmailAsync(Verify(firstToken)));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.VerifyEmailAsync(Verify(secondToken)));
        Assert.Null(user.EmailVerifiedAt);
    }

    [Fact]
    public async Task Resend_SaysSoWhenEmailCannotBeSent()
    {
        var (context, _) = await RegisteredAsync();
        var service = context.CreateAccountEmail();
        context.Clock.Advance(TimeSpan.FromMinutes(2));

        context.Sender.Fail = true;
        await Assert.ThrowsAsync<ServiceUnavailableException>(() => service.ResendVerificationAsync(1, null));

        context.Sender.Fail = false;
        context.Sender.IsConfigured = false;
        await Assert.ThrowsAsync<ServiceUnavailableException>(() => service.ResendVerificationAsync(1, null));
    }

    // ---- password reset ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task ForgotPassword_WithoutSmtp_SaysEmailIsUnavailable_ForEveryAddress()
    {
        var context = new AccountTestContext(AccountTestContext.Customer("secret1"));
        context.Sender.IsConfigured = false;
        var service = context.CreateAccountEmail();

        var known = await Assert.ThrowsAsync<ServiceUnavailableException>(() => service.ForgotPasswordAsync(new ForgotPasswordRequestDto { Email = "aysun@example.com" }));
        var unknown = await Assert.ThrowsAsync<ServiceUnavailableException>(() => service.ForgotPasswordAsync(new ForgotPasswordRequestDto { Email = "nobody@example.com" }));

        Assert.Equal(known.Message, unknown.Message);
    }

    [Fact]
    public async Task ForgotPassword_WithABadAddress_IsAValidationError()
    {
        var service = new AccountTestContext().CreateAccountEmail();

        await Assert.ThrowsAsync<ValidationException>(() => service.ForgotPasswordAsync(new ForgotPasswordRequestDto { Email = "not-an-email" }));
    }

    // ---- logs --------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task ASmtpFailure_IsLoggedWithoutTheLoginThePasswordOrTheRecipient()
    {
        // A port nothing listens on: the connection is refused at once, like a wrong host would be.
        var log = new ListLogger<SmtpEmailSender>();
        var sender = new SmtpEmailSender(Options.Create(new SmtpSettings
        {
            Host = "127.0.0.1",
            Port = 1,
            Security = "None",
            UserName = "sender.account@gmail.com",
            Password = "super-secret-app-password",
            SenderEmail = "sender.account@gmail.com",
            TimeoutSeconds = 5
        }), log);

        Assert.True(sender.IsConfigured);
        var error = await Assert.ThrowsAsync<EmailDeliveryException>(() =>
            sender.SendAsync(new EmailMessage("customer@example.com", "Subject", "text link?token=abc123", "<p>html</p>")));

        Assert.DoesNotContain("super-secret", error.Message);
        Assert.NotEmpty(log.Messages);
        Assert.All(log.Messages, line =>
        {
            Assert.DoesNotContain("super-secret-app-password", line);
            Assert.DoesNotContain("sender.account", line);
            Assert.DoesNotContain("customer@example.com", line);
            Assert.DoesNotContain("abc123", line);
        });
    }

    [Theory]
    [InlineData("", "user@gmail.com", "app-password", "user@gmail.com")]
    [InlineData("smtp.gmail.com", "", "app-password", "user@gmail.com")]
    [InlineData("smtp.gmail.com", "user@gmail.com", "", "user@gmail.com")]
    [InlineData("smtp.gmail.com", "user@gmail.com", "app-password", "")]
    public async Task SmtpWithAMissingSetting_IsNotConfigured_AndSendsNothing(string host, string userName, string password, string senderEmail)
    {
        var sender = new SmtpEmailSender(Options.Create(new SmtpSettings { Host = host, UserName = userName, Password = password, SenderEmail = senderEmail }), NullLogger<SmtpEmailSender>.Instance);

        Assert.False(sender.IsConfigured);
        await Assert.ThrowsAsync<EmailDeliveryException>(() => sender.SendAsync(new EmailMessage("customer@example.com", "s", "t", "h")));
    }

    [Fact]
    public void TheDefaults_AreGmailOnPort587WithStartTls_AndNoSecrets()
    {
        var settings = new SmtpSettings();

        Assert.Equal("smtp.gmail.com", settings.Host);
        Assert.Equal(587, settings.Port);
        Assert.Equal("StartTls", settings.Security);
        Assert.Equal(string.Empty, settings.UserName);
        Assert.Equal(string.Empty, settings.Password);
        Assert.False(settings.IsComplete);
    }
}
