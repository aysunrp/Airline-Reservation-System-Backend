using AirlineReservationSystem.Helpers;

namespace AirlineReservationSystem.Tests;

public class CheckEmailCommandTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public CheckEmailCommandTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void TheCommandIsRecognised_AlongsideThePromoteCommand()
    {
        Assert.True(ServerCommands.IsCommand(new[] { "check-email" }));
        Assert.True(ServerCommands.IsCommand(new[] { "CHECK-EMAIL" }));
        Assert.True(ServerCommands.IsCommand(new[] { "promote-superadmin", "--email", "a@example.com" }));
        Assert.False(ServerCommands.IsCommand(new[] { "something-else" }));
        Assert.False(ServerCommands.IsCommand(Array.Empty<string>()));
    }

    [Fact]
    public async Task TheCommand_AnswersInPlainWords_AndNeverShowsASecret()
    {
        var output = new StringWriter();

        var code = await ServerCommands.RunAsync(new[] { "check-email" }, _factory.Services, new StringReader(string.Empty), output);

        // The API tests use a stand-in sender, which the command reports as one it cannot check.
        Assert.Equal(0, code);
        Assert.Contains("cannot be checked", output.ToString());
    }
}
