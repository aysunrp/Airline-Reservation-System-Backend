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
    [InlineData("xx", "Confirm your AEROVA")]
    [InlineData(null, "Confirm your AEROVA")]
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
    public async Task AnExpiredOrUnknownLink_IsRefusedWithTheSameMessage()
    {
        var (context, user) = await RegisteredAsync();
        var token = context.Sender.LatestToken();
        var service = context.CreateAccountEmail();

        var unknown = await Assert.ThrowsAsync<BusinessRuleException>(() => service.VerifyEmailAsync(Verify("not-a-real-token")));
        context.Clock.Advance(TimeSpan.FromHours(24.1));
        var expired = await Assert.ThrowsAsync<BusinessRuleException>(() => service.VerifyEmailAsync(Verify(token)));

        Assert.Equal(unknown.Message, expired.Message);
        Assert.Null(user.EmailVerifiedAt);
    }

    [Fact]
    public async Task AResetToken_CannotVerifyAnAddress()
    {
        var (context, user) = await RegisteredAsync();
        var service = context.CreateAccountEmail();
        await service.ForgotPasswordAsync(new ForgotPasswordRequestDto { Email = "aysun@example.com" });
        var resetToken = context.Sender.LatestToken();

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.VerifyEmailAsync(Verify(resetToken)));

        Assert.Null(user.EmailVerifiedAt);
    }

    [Fact]
    public async Task Resend_IsLimitedToOnePerMinute_AndTheOldLinkStopsWorking()
    {
        var (context, user) = await RegisteredAsync();
        var firstToken = context.Sender.LatestToken();
        var service = context.CreateAccountEmail();

        var wait = await Assert.ThrowsAsync<TooManyAttemptsException>(() => service.ResendVerificationAsync(1, null));
        Assert.True(wait.RetryAfter > TimeSpan.Zero && wait.RetryAfter <= TimeSpan.FromSeconds(60));

        context.Clock.Advance(TimeSpan.FromSeconds(61));
        await service.ResendVerificationAsync(1, null);

        Assert.Equal(2, context.Sender.Sent.Count);
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.VerifyEmailAsync(Verify(firstToken)));
        await service.VerifyEmailAsync(Verify(context.Sender.LatestToken()));
        Assert.NotNull(user.EmailVerifiedAt);

        await Assert.ThrowsAsync<ConflictException>(() => service.ResendVerificationAsync(1, null));
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
    public async Task ForgotPassword_AnswersTheSameForAKnownAndAnUnknownAddress_AndMailsOnlyTheKnownOne()
    {
        var context = new AccountTestContext(AccountTestContext.Customer("secret1"));
        var service = context.CreateAccountEmail();

        await service.ForgotPasswordAsync(new ForgotPasswordRequestDto { Email = "nobody@example.com" });
        Assert.Empty(context.Sender.Sent);

        await service.ForgotPasswordAsync(new ForgotPasswordRequestDto { Email = " AYSUN@example.com " });
        var message = Assert.Single(context.Sender.Sent);
        Assert.Equal("aysun@example.com", message.To);
        Assert.Contains("https://aerova.test/reset-password.html?token=", message.TextBody);
        Assert.Equal(AccountTestContext.Start.AddMinutes(60), context.Tokens.All.Single().ExpiresAt);
    }

    [Fact]
    public async Task ForgotPassword_SendsNothingForADisabledAccount_AndNotTwiceInAMinute()
    {
        var disabled = AccountTestContext.Customer("secret1");
        disabled.IsActive = false;
        var context = new AccountTestContext(disabled);
        var service = context.CreateAccountEmail();

        await service.ForgotPasswordAsync(new ForgotPasswordRequestDto { Email = "aysun@example.com" });
        Assert.Empty(context.Sender.Sent);

        disabled.IsActive = true;
        await service.ForgotPasswordAsync(new ForgotPasswordRequestDto { Email = "aysun@example.com" });
        await service.ForgotPasswordAsync(new ForgotPasswordRequestDto { Email = "aysun@example.com" });
        Assert.Single(context.Sender.Sent);

        context.Clock.Advance(TimeSpan.FromSeconds(61));
        await service.ForgotPasswordAsync(new ForgotPasswordRequestDto { Email = "aysun@example.com" });
        Assert.Equal(2, context.Sender.Sent.Count);
    }

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
    public async Task ForgotPassword_WithAFailedDelivery_StillAnswersLikeASentLink()
    {
        var context = new AccountTestContext(AccountTestContext.Customer("secret1"));
        context.Sender.Fail = true;

        await context.CreateAccountEmail().ForgotPasswordAsync(new ForgotPasswordRequestDto { Email = "aysun@example.com" });

        Assert.Empty(context.Sender.Sent);
    }

    [Fact]
    public async Task ForgotPassword_WithABadAddress_IsAValidationError()
    {
        var service = new AccountTestContext().CreateAccountEmail();

        await Assert.ThrowsAsync<ValidationException>(() => service.ForgotPasswordAsync(new ForgotPasswordRequestDto { Email = "not-an-email" }));
    }

    [Fact]
    public async Task ResetPassword_SetsTheNewPassword_Once()
    {
        var user = AccountTestContext.Customer("secret1");
        var context = new AccountTestContext(user);
        var service = context.CreateAccountEmail();
        await service.ForgotPasswordAsync(new ForgotPasswordRequestDto { Email = "aysun@example.com" });
        var token = context.Sender.LatestToken();

        await service.ResetPasswordAsync(Reset(token));

        var auth = context.CreateAuth();
        Assert.Equal(5, (await auth.LoginAsync(new LoginRequestDto { Email = "aysun@example.com", Password = "new-secret2" })).User.Id);
        await Assert.ThrowsAsync<UnauthorizedException>(() => auth.LoginAsync(new LoginRequestDto { Email = "aysun@example.com", Password = "secret1" }));
        Assert.NotEqual("new-secret2", user.PasswordHash);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ResetPasswordAsync(Reset(token, "another-pass3")));
    }

    [Fact]
    public async Task ResetPassword_RefusesAnExpiredAnUnknownAndAnUnsuitableLink()
    {
        var context = new AccountTestContext(AccountTestContext.Customer("secret1"));
        var service = context.CreateAccountEmail();
        await service.ForgotPasswordAsync(new ForgotPasswordRequestDto { Email = "aysun@example.com" });
        var token = context.Sender.LatestToken();

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ResetPasswordAsync(Reset("not-a-real-token")));

        // A password that is too short is refused before the token is looked at, and does not use the token up.
        await Assert.ThrowsAsync<ValidationException>(() => service.ResetPasswordAsync(Reset(token, "123")));
        await Assert.ThrowsAsync<ValidationException>(() => service.ResetPasswordAsync(new ResetPasswordRequestDto { Token = token, NewPassword = "secret3", ConfirmPassword = "secret4" }));

        context.Clock.Advance(TimeSpan.FromMinutes(61));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ResetPasswordAsync(Reset(token)));
    }

    [Fact]
    public async Task ResetPassword_UsesUpEveryOpenResetLink_AndEndsASignInPause()
    {
        var context = new AccountTestContext(AccountTestContext.Customer("secret1"));
        var service = context.CreateAccountEmail();
        var auth = context.CreateAuth();

        await service.ForgotPasswordAsync(new ForgotPasswordRequestDto { Email = "aysun@example.com" });
        var first = context.Sender.LatestToken();
        context.Clock.Advance(TimeSpan.FromSeconds(61));
        await service.ForgotPasswordAsync(new ForgotPasswordRequestDto { Email = "aysun@example.com" });
        var second = context.Sender.LatestToken();

        // The newer link replaced the older one.
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ResetPasswordAsync(Reset(first)));

        for (var i = 0; i < 5; i++)
        {
            await Assert.ThrowsAsync<UnauthorizedException>(() => auth.LoginAsync(new LoginRequestDto { Email = "aysun@example.com", Password = "wrong-password" }));
        }

        await service.ResetPasswordAsync(Reset(second));

        Assert.NotNull(await auth.LoginAsync(new LoginRequestDto { Email = "aysun@example.com", Password = "new-secret2" }));
        Assert.All(context.Tokens.All, token => Assert.NotNull(token.UsedAt));
    }

    [Fact]
    public async Task OpeningTheResetLink_ShowsTheMailboxIsTheirs_SoTheAddressCountsAsVerified()
    {
        var user = AccountTestContext.Customer("secret1");
        var context = new AccountTestContext(user);
        var service = context.CreateAccountEmail();
        await service.ForgotPasswordAsync(new ForgotPasswordRequestDto { Email = "aysun@example.com" });

        await service.ResetPasswordAsync(Reset(context.Sender.LatestToken()));

        Assert.Equal(AccountTestContext.Start, user.EmailVerifiedAt);
    }

    // ---- logs --------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task TheLog_NeverHoldsATokenAnAddressOrAPassword()
    {
        var context = new AccountTestContext();
        context.Sender.Fail = true;
        var auth = context.CreateAuth();
        await auth.RegisterAsync(Registration());
        var service = context.CreateAccountEmail();

        context.Sender.Fail = false;
        context.Clock.Advance(TimeSpan.FromSeconds(61));
        await service.ResendVerificationAsync(1, null);
        var token = context.Sender.LatestToken();
        await service.ForgotPasswordAsync(new ForgotPasswordRequestDto { Email = "aysun@example.com" });
        var resetToken = context.Sender.LatestToken();
        await service.ResetPasswordAsync(Reset(resetToken, "my-new-password9"));

        var lines = context.EmailLog.Messages.Concat(context.AuthLog.Messages).ToList();
        Assert.All(lines, line =>
        {
            Assert.DoesNotContain("aysun", line, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(token, line);
            Assert.DoesNotContain(resetToken, line);
            Assert.DoesNotContain("secret1", line);
            Assert.DoesNotContain("my-new-password9", line);
            Assert.DoesNotContain("token=", line);
        });
    }

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
