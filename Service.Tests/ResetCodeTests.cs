using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using Service.Helpers.DTOs.Account;
using Service.Helpers.Exceptions;
using Service.Services;

namespace Service.Tests;

/// <summary>
/// The password reset by e-mailed code: the six digits, their short life, single use and the few wrong guesses they allow, the ticket a
/// correct code is traded for, and a log and a database that never hold a code. Also the three states of a verification link.
/// A fake SMTP sender stands in for the mail server: nothing is ever sent for real.
/// </summary>
public class ResetCodeTests
{
    private const string Address = "aysun@example.com";

    private static ForgotPasswordRequestDto Forgot(string email = Address, string? language = null) => new() { Email = email, Language = language };

    private static VerifyResetCodeRequestDto Code(string code, string email = Address) => new() { Email = email, Code = code };

    private static ResetPasswordRequestDto Reset(string ticket, string password = "new-secret2") => new() { Token = ticket, NewPassword = password, ConfirmPassword = password };

    private static string WrongCodeFor(string code) => code == "000000" ? "000001" : "000000";

    private static async Task<(AccountTestContext Context, AccountEmailService Service, User User)> CustomerAsync()
    {
        var user = AccountTestContext.Customer("secret1");
        var context = new AccountTestContext(user);
        return (context, context.CreateAccountEmail(), user);
    }

    // ---- sending the code -------------------------------------------------------------------------------------------

    [Fact]
    public async Task ForgotPassword_MailsASixDigitCode_NotALink_AndKeepsOnlyAKeyedHash()
    {
        var (context, service, _) = await CustomerAsync();

        await service.ForgotPasswordAsync(Forgot(" AYSUN@example.com "));

        var message = Assert.Single(context.Sender.Sent);
        Assert.Equal(Address, message.To);
        var code = context.Sender.LatestCode();
        Assert.Matches("^[0-9]{6}$", code);
        Assert.DoesNotContain("http", message.TextBody);
        Assert.Contains(code, message.HtmlBody);

        var stored = Assert.Single(context.Tokens.All);
        Assert.Equal(EmailTokenPurpose.ResetCode, stored.Purpose);
        Assert.Equal(64, stored.TokenHash.Length);
        Assert.DoesNotContain(code, stored.TokenHash);
        Assert.Equal(AccountTestContext.Start.AddMinutes(10), stored.ExpiresAt);
        Assert.Equal(0, stored.Attempts);
    }

    [Fact]
    public async Task ForgotPassword_AnswersTheSameForAKnownAndAnUnknownAddress_AndMailsOnlyTheKnownOne()
    {
        var (context, service, _) = await CustomerAsync();

        await service.ForgotPasswordAsync(Forgot("nobody@example.com"));
        Assert.Empty(context.Sender.Sent);
        Assert.Empty(context.Tokens.All);

        await service.ForgotPasswordAsync(Forgot());
        Assert.Single(context.Sender.Sent);
    }

    [Theory]
    [InlineData(null, "Your AEROVA password reset code")]
    [InlineData("az", "AEROVA şifrə sıfırlama kodu")]
    [InlineData("ru", "Код для сброса пароля AEROVA")]
    [InlineData("fr", "Your AEROVA password reset code")]
    public async Task TheCodeEmail_IsInTheChosenLanguage(string? language, string subject)
    {
        var (context, service, _) = await CustomerAsync();

        await service.ForgotPasswordAsync(Forgot(language: language));

        Assert.Equal(subject, Assert.Single(context.Sender.Sent).Subject);
    }

    [Fact]
    public async Task AFailedDelivery_StillAnswersLikeASentCode_AndClosesTheUnreceivedCode()
    {
        var (context, service, _) = await CustomerAsync();
        context.Sender.Fail = true;

        await service.ForgotPasswordAsync(Forgot());

        Assert.Empty(context.Sender.Sent);
        Assert.NotNull(Assert.Single(context.Tokens.All).UsedAt);
    }

    [Fact]
    public async Task ACodeIsNotMailedTwiceInAMinute_AndNotMoreThanFiveTimesAnHour()
    {
        var (context, service, _) = await CustomerAsync();

        await service.ForgotPasswordAsync(Forgot());
        await service.ForgotPasswordAsync(Forgot());
        Assert.Single(context.Sender.Sent);

        for (var i = 0; i < 6; i++)
        {
            context.Clock.Advance(TimeSpan.FromSeconds(61));
            await service.ForgotPasswordAsync(Forgot());
        }

        Assert.Equal(5, context.Sender.Sent.Count);
    }

    [Fact]
    public async Task ForgotPassword_WithoutSmtp_SaysEmailIsUnavailable_ForEveryAddress()
    {
        var (context, service, _) = await CustomerAsync();
        context.Sender.IsConfigured = false;

        var known = await Assert.ThrowsAsync<ServiceUnavailableException>(() => service.ForgotPasswordAsync(Forgot()));
        var unknown = await Assert.ThrowsAsync<ServiceUnavailableException>(() => service.ForgotPasswordAsync(Forgot("nobody@example.com")));

        Assert.Equal(known.Message, unknown.Message);
    }

    // ---- checking the code ------------------------------------------------------------------------------------------

    [Fact]
    public async Task ACorrectCode_IsTradedForATicket_AndWorksOnce()
    {
        var (context, service, _) = await CustomerAsync();
        await service.ForgotPasswordAsync(Forgot());
        var code = context.Sender.LatestCode();

        var ticket = await service.VerifyResetCodeAsync(Code(code));

        Assert.True(ticket.Ticket.Length >= 40);
        Assert.Equal(10, ticket.ExpiresInMinutes);
        Assert.Contains(context.Tokens.All, t => t.Purpose == EmailTokenPurpose.ResetCode && t.UsedAt is not null);

        // The same code again gives nothing.
        var again = await Assert.ThrowsAsync<BusinessRuleException>(() => service.VerifyResetCodeAsync(Code(code)));
        Assert.Equal("code_invalid", again.Code);
    }

    [Fact]
    public async Task EveryKindOfFailure_GivesTheSameAnswer_SoNothingIsLearnedAboutTheAccount()
    {
        var (context, service, _) = await CustomerAsync();
        await service.ForgotPasswordAsync(Forgot());
        var code = context.Sender.LatestCode();

        var noAccount = await Assert.ThrowsAsync<BusinessRuleException>(() => service.VerifyResetCodeAsync(Code("123456", "nobody@example.com")));
        var wrong = await Assert.ThrowsAsync<BusinessRuleException>(() => service.VerifyResetCodeAsync(Code(WrongCodeFor(code))));
        context.Clock.Advance(TimeSpan.FromMinutes(10.1));
        var expired = await Assert.ThrowsAsync<BusinessRuleException>(() => service.VerifyResetCodeAsync(Code(code)));

        Assert.All(new[] { noAccount, wrong, expired }, e => Assert.Equal(("code_invalid", noAccount.Message), (e.Code, e.Message)));
    }

    [Fact]
    public async Task AFiveTimesWrongCode_IsDead_EvenWhenTheRightCodeIsTypedNext()
    {
        var (context, service, _) = await CustomerAsync();
        await service.ForgotPasswordAsync(Forgot());
        var code = context.Sender.LatestCode();

        for (var i = 0; i < 5; i++)
        {
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.VerifyResetCodeAsync(Code(WrongCodeFor(code))));
        }

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.VerifyResetCodeAsync(Code(code)));
        Assert.Equal(5, context.Tokens.All.Single().Attempts);

        // A new code is the way on, after the minute that has to pass between two mails.
        context.Clock.Advance(TimeSpan.FromSeconds(61));
        await service.ForgotPasswordAsync(Forgot());
        Assert.NotNull(await service.VerifyResetCodeAsync(Code(context.Sender.LatestCode())));
    }

    [Fact]
    public async Task ANewCode_ReplacesTheOldOne_AndACodeOfAnotherAccountDoesNotFit()
    {
        var first = AccountTestContext.Customer("secret1");
        var other = AccountTestContext.Customer("secret2", id: 6, email: "other@example.com");
        var context = new AccountTestContext(first, other);
        var service = context.CreateAccountEmail();

        await service.ForgotPasswordAsync(Forgot());
        var oldCode = context.Sender.LatestCode();
        context.Clock.Advance(TimeSpan.FromSeconds(61));
        await service.ForgotPasswordAsync(Forgot());
        var newCode = context.Sender.LatestCode();
        await service.ForgotPasswordAsync(Forgot("other@example.com"));
        var otherCode = context.Sender.LatestCode();

        if (oldCode != newCode)
        {
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.VerifyResetCodeAsync(Code(oldCode)));
        }

        // The other account's code is not good for this one (and trying it counts as a wrong guess).
        if (otherCode != newCode)
        {
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.VerifyResetCodeAsync(Code(otherCode)));
        }

        Assert.NotNull(await service.VerifyResetCodeAsync(Code(newCode)));
    }

    [Fact]
    public async Task TheCodeCheck_RefusesBadInput_BeforeLookingAtAnything()
    {
        var (_, service, _) = await CustomerAsync();

        await Assert.ThrowsAsync<ValidationException>(() => service.VerifyResetCodeAsync(Code("12345")));
        await Assert.ThrowsAsync<ValidationException>(() => service.VerifyResetCodeAsync(Code("12345a")));
        await Assert.ThrowsAsync<ValidationException>(() => service.VerifyResetCodeAsync(Code("123456", "not-an-email")));
    }

    // ---- the new password -------------------------------------------------------------------------------------------

    [Fact]
    public async Task ThePasswordChanges_OnlyWithATicket_NeverWithTheCodeItself()
    {
        var (context, service, user) = await CustomerAsync();
        await service.ForgotPasswordAsync(Forgot());
        var code = context.Sender.LatestCode();
        var oldHash = user.PasswordHash;

        // Neither the code nor a made-up ticket is a ticket.
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ResetPasswordAsync(Reset(code)));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ResetPasswordAsync(Reset("not-a-real-ticket")));
        Assert.Equal(oldHash, user.PasswordHash);

        var ticket = (await service.VerifyResetCodeAsync(Code(code))).Ticket;
        await service.ResetPasswordAsync(Reset(ticket));

        Assert.NotEqual(oldHash, user.PasswordHash);
    }

    [Fact]
    public async Task AfterTheReset_TheNewPasswordSignsIn_TheOldOneDoesNot_AndNobodyIsSignedInAutomatically()
    {
        var (context, service, _) = await CustomerAsync();
        await service.ForgotPasswordAsync(Forgot());
        var ticket = (await service.VerifyResetCodeAsync(Code(context.Sender.LatestCode()))).Ticket;

        await service.ResetPasswordAsync(Reset(ticket));

        var auth = context.CreateAuth();
        Assert.Equal(5, (await auth.LoginAsync(new LoginRequestDto { Email = Address, Password = "new-secret2" })).User.Id);
        await Assert.ThrowsAsync<UnauthorizedException>(() => auth.LoginAsync(new LoginRequestDto { Email = Address, Password = "secret1" }));

        // The ticket works once.
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ResetPasswordAsync(Reset(ticket, "another-pass3")));
    }

    [Fact]
    public async Task TheTicket_ExpiresAfterTenMinutes_AndTooShortAPasswordDoesNotUseItUp()
    {
        var (context, service, _) = await CustomerAsync();
        await service.ForgotPasswordAsync(Forgot());
        var ticket = (await service.VerifyResetCodeAsync(Code(context.Sender.LatestCode()))).Ticket;

        await Assert.ThrowsAsync<ValidationException>(() => service.ResetPasswordAsync(Reset(ticket, "123")));
        await Assert.ThrowsAsync<ValidationException>(() => service.ResetPasswordAsync(new ResetPasswordRequestDto { Token = ticket, NewPassword = "secret3", ConfirmPassword = "secret4" }));

        context.Clock.Advance(TimeSpan.FromMinutes(10.1));
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => service.ResetPasswordAsync(Reset(ticket)));
        Assert.Equal("ticket_invalid", error.Code);
    }

    [Fact]
    public async Task TheReset_UsesUpEveryOpenCodeAndTicket_AndEndsASignInPause()
    {
        var (context, service, _) = await CustomerAsync();
        var auth = context.CreateAuth();
        await service.ForgotPasswordAsync(Forgot());
        var code = context.Sender.LatestCode();
        var ticket = (await service.VerifyResetCodeAsync(Code(code))).Ticket;
        context.Clock.Advance(TimeSpan.FromSeconds(61));
        await service.ForgotPasswordAsync(Forgot());
        var laterCode = context.Sender.LatestCode();

        for (var i = 0; i < 5; i++)
        {
            await Assert.ThrowsAsync<UnauthorizedException>(() => auth.LoginAsync(new LoginRequestDto { Email = Address, Password = "wrong-password" }));
        }

        await service.ResetPasswordAsync(Reset(ticket));

        Assert.NotNull(await auth.LoginAsync(new LoginRequestDto { Email = Address, Password = "new-secret2" }));
        Assert.All(context.Tokens.All, token => Assert.NotNull(token.UsedAt));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.VerifyResetCodeAsync(Code(laterCode)));
    }

    [Fact]
    public async Task AnAddressThatReceivedAndTypedTheCode_CountsAsVerified()
    {
        var (context, service, user) = await CustomerAsync();
        await service.ForgotPasswordAsync(Forgot());
        var ticket = (await service.VerifyResetCodeAsync(Code(context.Sender.LatestCode()))).Ticket;

        await service.ResetPasswordAsync(Reset(ticket));

        Assert.Equal(AccountTestContext.Start, user.EmailVerifiedAt);
    }

    [Fact]
    public async Task TheLogAndTheDatabase_NeverHoldACodeATicketAnAddressOrAPassword()
    {
        var (context, service, _) = await CustomerAsync();
        context.Sender.Fail = true;
        await service.ForgotPasswordAsync(Forgot());
        context.Sender.Fail = false;
        context.Clock.Advance(TimeSpan.FromSeconds(61));
        await service.ForgotPasswordAsync(Forgot());
        var code = context.Sender.LatestCode();
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.VerifyResetCodeAsync(Code(WrongCodeFor(code))));
        var ticket = (await service.VerifyResetCodeAsync(Code(code))).Ticket;
        await service.ResetPasswordAsync(Reset(ticket, "my-new-password9"));

        Assert.All(context.EmailLog.Messages, line =>
        {
            Assert.DoesNotContain("aysun", line, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(code, line);
            Assert.DoesNotContain(ticket, line);
            Assert.DoesNotContain("secret1", line);
            Assert.DoesNotContain("my-new-password9", line);
        });
        Assert.DoesNotContain(context.Tokens.All, token => token.TokenHash.Contains(code) || token.TokenHash == ticket);
    }

    // ---- the verification link --------------------------------------------------------------------------------------

    [Fact]
    public async Task AVerificationLink_IsToldApart_WhenUnknownExpiredOrAlreadyUsed()
    {
        var context = new AccountTestContext();
        await context.CreateAuth().RegisterAsync(new RegisterRequestDto
        {
            FirstName = "Aysun", LastName = "Panahli", Email = Address, Password = "secret1", ConfirmPassword = "secret1"
        });
        var user = context.Users.Find(1)!;
        var token = context.Sender.LatestToken();
        var service = context.CreateAccountEmail();

        var unknown = await Assert.ThrowsAsync<BusinessRuleException>(() => service.VerifyEmailAsync(new VerifyEmailRequestDto { Token = "not-a-real-token" }));
        Assert.Equal("link_invalid", unknown.Code);

        context.Clock.Advance(TimeSpan.FromHours(24.1));
        var expired = await Assert.ThrowsAsync<BusinessRuleException>(() => service.VerifyEmailAsync(new VerifyEmailRequestDto { Token = token }));
        Assert.Equal("link_expired", expired.Code);
        Assert.Null(user.EmailVerifiedAt);

        // A fresh link works; opening it again says it was used, and that the address is confirmed.
        context.Clock.Advance(TimeSpan.FromSeconds(61));
        await service.ResendVerificationAsync(1, null);
        var fresh = context.Sender.LatestToken();
        await service.VerifyEmailAsync(new VerifyEmailRequestDto { Token = fresh });
        Assert.NotNull(user.EmailVerifiedAt);

        var used = await Assert.ThrowsAsync<BusinessRuleException>(() => service.VerifyEmailAsync(new VerifyEmailRequestDto { Token = fresh }));
        Assert.Equal("link_confirmed", used.Code);
        Assert.Contains("confirmed", used.Message);
    }

    [Fact]
    public async Task ATicket_CannotVerifyAnAddress()
    {
        var (context, service, user) = await CustomerAsync();
        await service.ForgotPasswordAsync(Forgot());
        var ticket = (await service.VerifyResetCodeAsync(Code(context.Sender.LatestCode()))).Ticket;

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => service.VerifyEmailAsync(new VerifyEmailRequestDto { Token = ticket }));

        Assert.Equal("link_invalid", error.Code);
        Assert.Null(user.EmailVerifiedAt);
    }
}
