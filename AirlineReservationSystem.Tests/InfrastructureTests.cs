using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Service.Helpers.Settings;
using Service.Services;

namespace AirlineReservationSystem.Tests;

public class InfrastructureTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public InfrastructureTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public void FlightGenerationWorker_IsRegistered_AndReadsItsSettings()
    {
        Assert.Single(_factory.Services.GetServices<IHostedService>().OfType<FlightGenerationWorker>());

        // appsettings.json values; the test host turns the worker off so it cannot change test data.
        var settings = _factory.Services.GetRequiredService<IOptions<FlightGenerationSettings>>().Value;
        Assert.Equal((90, new TimeOnly(2, 0), false, false), (settings.DaysAhead, settings.DailyRunTimeUtc, settings.RunOnStartup, settings.WorkerEnabled));
    }

    [Fact]
    public void FlightGenerationSettings_BindFromConfiguration()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FlightGeneration:DaysAhead"] = "30",
            ["FlightGeneration:WorkerEnabled"] = "false",
            ["FlightGeneration:DailyRunTimeUtc"] = "03:15",
            ["FlightGeneration:RunOnStartup"] = "true"
        }).Build();

        var settings = configuration.GetSection(FlightGenerationSettings.SectionName).Get<FlightGenerationSettings>()!;

        Assert.Equal((30, false, new TimeOnly(3, 15), true), (settings.DaysAhead, settings.WorkerEnabled, settings.DailyRunTimeUtc, settings.RunOnStartup));
    }

    [Fact]
    public async Task Swagger_DocumentIsServed()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("http://localhost:5500")]
    [InlineData("http://127.0.0.1:5500")]
    public async Task Cors_AllowsTheFrontendOrigins(string origin)
    {
        var response = await _client.SendAsync(Preflight(origin));

        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values));
        Assert.Equal(origin, Assert.Single(values));
    }

    [Fact]
    public async Task Cors_RejectsOtherOrigins()
    {
        var response = await _client.SendAsync(Preflight("http://evil.example"));

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private static HttpRequestMessage Preflight(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/airports");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type,authorization");
        return request;
    }
}
