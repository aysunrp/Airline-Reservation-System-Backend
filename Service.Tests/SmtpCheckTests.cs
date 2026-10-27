using Microsoft.Extensions.Options;
using Service.Helpers.Settings;
using Service.Services;

namespace Service.Tests;

/// <summary>The operator's e-mail check: it says what is wrong in plain words and never shows the login or the password.</summary>
public class SmtpCheckTests
{
    private static SmtpEmailSender Sender(SmtpSettings settings) => new(Options.Create(settings), new ListLogger<SmtpEmailSender>());

    [Fact]
    public async Task WithoutSettings_ItSaysEmailIsNotSetUp()
    {
        var (ok, message) = await Sender(new SmtpSettings()).CheckAsync();

        Assert.False(ok);
        Assert.Contains("not set up", message);
    }

    [Fact]
    public async Task AServerThatCannotBeReached_IsReportedWithoutTheLoginOrThePassword()
    {
        var settings = new SmtpSettings
        {
            Host = "127.0.0.1",
            Port = 1,
            UserName = "login.name@example.com",
            Password = "secret-app-password",
            SenderEmail = "login.name@example.com",
            TimeoutSeconds = 5
        };

        var (ok, message) = await Sender(settings).CheckAsync();

        Assert.False(ok);
        Assert.DoesNotContain("login.name", message);
        Assert.DoesNotContain("secret-app-password", message);
        Assert.False(string.IsNullOrWhiteSpace(message));
    }
}
