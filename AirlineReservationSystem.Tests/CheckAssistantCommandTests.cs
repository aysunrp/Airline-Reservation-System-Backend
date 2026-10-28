using AirlineReservationSystem.Helpers;
using Service.Helpers.Exceptions;

namespace AirlineReservationSystem.Tests;

/// <summary>The check-assistant command an operator runs to see whether Ask AEROVA works. The provider is a stand-in: nothing is asked of a real one.</summary>
public class CheckAssistantCommandTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public CheckAssistantCommandTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.Assistant.Reset();
    }

    private async Task<(int Code, string Output)> RunAsync()
    {
        var output = new StringWriter();
        var code = await ServerCommands.RunAsync(new[] { "check-assistant" }, _factory.Services, new StringReader(string.Empty), output);
        return (code, output.ToString());
    }

    [Fact]
    public void TheCommandIsRecognised()
    {
        Assert.True(ServerCommands.IsCommand(new[] { "check-assistant" }));
        Assert.True(ServerCommands.IsCommand(new[] { "CHECK-ASSISTANT" }));
    }

    [Fact]
    public async Task WhenTheProviderAnswers_ItSaysTheAssistantWorks()
    {
        var (code, output) = await RunAsync();

        Assert.Equal(0, code);
        Assert.Contains("Ask AEROVA works", output);
        Assert.Single(_factory.Assistant.Requests);
    }

    [Fact]
    public async Task WithoutAKey_ItSaysWhichSettingToSet_AndAsksNothing()
    {
        _factory.Assistant.IsConfigured = false;

        var (code, output) = await RunAsync();

        Assert.Equal(3, code);
        Assert.Contains("TravelAssistant:ApiKey", output);
        Assert.Contains("user-secrets", output);
        Assert.Empty(_factory.Assistant.Requests);
    }

    [Theory]
    [InlineData("assistant_key_rejected", "refused the key")]
    [InlineData("assistant_model_unavailable", "TravelAssistant:Model")]
    [InlineData("assistant_billing", "credit")]
    [InlineData("assistant_unreachable", "could not be reached")]
    public async Task WhenTheProviderFails_ItNamesTheStepToFix_WithoutTheKey(string code, string expected)
    {
        _factory.Assistant.Throw = new ServiceUnavailableException("The assistant is not available right now. Please try again later.", code);

        var (exitCode, output) = await RunAsync();

        Assert.Equal(1, exitCode);
        Assert.Contains(expected, output);
        Assert.Contains(code, output);
        Assert.DoesNotContain("sk-api-tests-only", output);
    }

    [Fact]
    public async Task ASlowProvider_IsReportedAsATimeout()
    {
        _factory.Assistant.Throw = new GatewayTimeoutException("The assistant took too long to answer. Please try again.");

        var (exitCode, output) = await RunAsync();

        Assert.Equal(1, exitCode);
        Assert.Contains("did not answer within", output);
    }
}
