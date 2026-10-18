using System.Net;

namespace AirlineReservationSystem.Tests;

public class InfrastructureTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public InfrastructureTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
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
